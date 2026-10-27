using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Hhh.Api;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hhh.Tests;

public sealed class SetupFactory : WebApplicationFactory<Program>
{
    public const string DatabaseName = "HhhFirstAdminSetupTests";
    public string SetupKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HHH_TEST_CONNECTION_STRING")
            ?? "Server=.\\SQLEXPRESS01;Database=HhhIntegrationTests;Trusted_Connection=True;TrustServerCertificate=True") { InitialCatalog = DatabaseName };
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Hhh"] = connection.ConnectionString, ["Database:MigrateOnStartup"] = "true",
            ["Demo:Enabled"] = "false", ["Bootstrap:SetupKey"] = SetupKey,
            ["Authentication:SetupRequestsPerMinute"] = "100", ["Authentication:LoginRequestsPerMinute"] = "100"
        }));
    }
}

public sealed class InitialAdministratorSetupTests(SetupFactory factory) : IClassFixture<SetupFactory>, IAsyncLifetime
{
    private const string Password = "Setup-Test-Password1!";
    private sealed record SetupBody(string SetupKey, string Email, string DisplayName, string OrganizationName, string Password);
    private SetupBody Body(string suffix = "owner") => new(factory.SetupKey, $"setup-test-{suffix}@hhh.local", "Setup Test Owner", "setup-test-organization", Password);
    public Task InitializeAsync() => ResetFixtures();
    public Task DisposeAsync() => ResetFixtures();

    [Fact]
    public async Task Setup_requires_csrf_and_a_valid_operator_key_without_changing_the_database()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/setup", Body())).StatusCode);
        var availability = await Token(client); Assert.True(availability.GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", Body() with { SetupKey = new string('x', 43) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", Body() with { SetupKey = "" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        Assert.False(await db.Users.AnyAsync()); Assert.False(await db.Organizations.AnyAsync());
    }

    [Fact]
    public async Task Setup_is_unavailable_without_a_configured_key_or_with_a_short_key()
    {
        foreach (var key in new[] { "", "too-short" })
        {
            using var disabled = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Bootstrap:SetupKey"] = key })));
            using var client = disabled.CreateClient(); var availability = await Token(client);
            Assert.False(availability.GetProperty("setupAvailable").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/setup", Body())).StatusCode);
        }
    }

    [Fact]
    public async Task Invalid_password_or_profile_rolls_back_user_organization_and_completion_state()
    {
        using var client = factory.CreateClient(); await Token(client);
        foreach (var body in new[] { Body() with { Password = "onlylowercaseletters" }, Body() with { Email = "invalid" }, Body() with { OrganizationName = "" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/setup", body)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        Assert.False(await db.Users.AnyAsync()); Assert.False(await db.Organizations.AnyAsync()); Assert.False(await db.AuditEvents.AnyAsync());
        Assert.Null((await db.InstallationStates.SingleAsync()).AdministratorSetupCompletedAt);
        Assert.True((await Token(client)).GetProperty("setupAvailable").GetBoolean());
    }

    [Fact]
    public async Task First_administrator_can_sign_in_and_setup_remains_permanently_closed()
    {
        using var client = factory.CreateClient(); await Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/setup", Body()); response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(factory.SetupKey, content); Assert.DoesNotContain(Password, content);
        Assert.False((await Token(client)).GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = Body().Email, password = Password })).EnsureSuccessStatusCode();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(Roles.SystemAdministrator, me.GetProperty("roles").EnumerateArray().Single().GetString());
        await Token(client); var audit = await client.GetStringAsync($"/api/admin/organizations/{me.GetProperty("organizationId").GetString()}/audit");
        Assert.Contains("admin.platform.browser-setup.completed", audit); Assert.DoesNotContain(factory.SetupKey, audit); Assert.DoesNotContain(Password, audit);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>(); var user = await db.Users.SingleAsync();
            Assert.False(user.InvitationPending); Assert.Null(user.InvitationHash); Assert.Null(user.InvitationExpiresAt);
            Assert.False(user.EmailConfirmed); Assert.NotEqual(Password, user.PasswordHash); Assert.NotNull(user.PasswordHash);
            Assert.NotNull((await db.InstallationStates.SingleAsync()).AdministratorSetupCompletedAt);
            // An operator changing/deleting roles must not reopen initial setup.
            db.UserRoles.RemoveRange(await db.UserRoles.ToListAsync()); await db.SaveChangesAsync();
        }
        using var anonymous = factory.CreateClient(); Assert.False((await Token(anonymous)).GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsJsonAsync("/api/auth/setup", Body("second"))).StatusCode);
        using var finalScope = factory.Services.CreateScope(); Assert.Equal(1, await finalScope.ServiceProvider.GetRequiredService<HhhDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task Concurrent_setup_requests_create_exactly_one_administrator_and_organization()
    {
        using var first = factory.CreateClient(); using var second = factory.CreateClient(); await Token(first); await Token(second);
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/auth/setup", Body("first")), second.PostAsJsonAsync("/api/auth/setup", Body("second")));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NotFound);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        Assert.Equal(1, await db.Users.CountAsync()); Assert.Equal(1, await db.Organizations.CountAsync()); Assert.Equal(1, await db.UserRoles.CountAsync());
        Assert.NotNull((await db.InstallationStates.SingleAsync()).AdministratorSetupCompletedAt);
    }

    [Fact]
    public async Task Cli_activation_also_closes_browser_setup_and_web_can_finish_matching_pending_cli_setup()
    {
        using var client = factory.CreateClient();
        string path;
        using (var scope = factory.Services.CreateScope())
        {
            var invitation = await scope.ServiceProvider.GetRequiredService<AdministrationService>().Bootstrap(Body().Email, Body().DisplayName, Body().OrganizationName, CancellationToken.None);
            path = invitation.SetupPath;
        }
        Assert.True((await Token(client)).GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/setup", Body("different"))).StatusCode);
        var parts = path.Split('#')[1].Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        (await client.PostAsJsonAsync("/api/auth/activate", new { userId = parts["user"], token = parts["token"], password = Password })).EnsureSuccessStatusCode();
        Assert.False((await Token(client)).GetProperty("setupAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/setup", Body())).StatusCode);
        await ResetFixtures();
        using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AdministrationService>().Bootstrap(Body().Email, Body().DisplayName, Body().OrganizationName, CancellationToken.None);
        await Token(client); (await client.PostAsJsonAsync("/api/auth/setup", Body())).EnsureSuccessStatusCode();
        using var finalScope = factory.Services.CreateScope(); var db = finalScope.ServiceProvider.GetRequiredService<HhhDbContext>();
        Assert.Equal(1, await db.Users.CountAsync()); Assert.False((await db.Users.SingleAsync()).InvitationPending);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/activate", new { userId = parts["user"], token = parts["token"], password = Password })).StatusCode);
    }

    [Fact]
    public async Task Setup_has_a_separate_rate_limit()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:SetupRequestsPerMinute"] = "1" })));
        using var client = limited.CreateClient(); await Token(client);
        var wrong = Body() with { SetupKey = new string('x', 43) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/setup", wrong)).StatusCode);
    }

    private static async Task<JsonElement> Token(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf"); response.EnsureSuccessStatusCode();
        var token = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("XSRF-TOKEN=")).Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task ResetFixtures()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        if (db.Database.GetDbConnection().Database != SetupFactory.DatabaseName) throw new InvalidOperationException("Expected the isolated setup test database.");
        if (await db.Users.AnyAsync(u => u.Email == null || !u.Email.StartsWith("setup-test-"))) throw new InvalidOperationException("Setup tests contain an unexpected account.");
        var organizations = await db.Organizations.Where(o => o.Name.StartsWith("setup-test-")).ToListAsync(); var ids = organizations.Select(o => o.Id).ToArray();
        db.AuditEvents.RemoveRange(await db.AuditEvents.Where(a => ids.Contains(a.OrganizationId)).ToListAsync());
        db.Users.RemoveRange(await db.Users.ToListAsync()); db.Organizations.RemoveRange(organizations);
        (await db.InstallationStates.SingleAsync(s => s.Id == 1)).AdministratorSetupCompletedAt = null;
        await db.SaveChangesAsync();
    }
}
