using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hhh.Tests;

public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task Demo_administrator_sign_in_requires_csrf_uses_only_the_fixed_sample_account_and_is_audited()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
        await Token(client);
        (await client.PostAsJsonAsync("/api/auth/demo-admin", new { email = "summit@hhh.local", organizationId = SummitOrg })).EnsureSuccessStatusCode();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("sysadmin@hhh.local", me.GetProperty("email").GetString());
        Assert.Equal(Roles.SystemAdministrator, me.GetProperty("roles").EnumerateArray().Single().GetString());
        Assert.Equal(HorizonOrg, me.GetProperty("organizationId").GetString());
        var audit = await client.GetStringAsync("/api/audit"); Assert.Contains("auth.demo.admin", audit);
        using var finance = factory.CreateClient(); await Token(finance);
        (await finance.PostAsJsonAsync("/api/auth/demo", new { })).EnsureSuccessStatusCode();
        var cfo = await finance.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(Roles.Cfo, cfo.GetProperty("roles").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task Demo_administrator_sign_in_is_disabled_without_both_development_environment_and_demo_flag()
    {
        foreach (var scenario in new[] { (Environment: "Production", Enabled: "true"), (Environment: "Development", Enabled: "false") })
        {
            using var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(scenario.Environment);
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:Enabled"] = scenario.Enabled }));
            });
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            await Token(client);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
        }
    }

    [Fact]
    public async Task Demo_administrator_sign_in_cannot_restore_inactive_pending_locked_or_demoted_accounts()
    {
        using var client = factory.CreateClient(); await Token(client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "sysadmin@hhh.local");
        var org = await db.Organizations.SingleAsync(o => o.Id == user.OrganizationId);
        var roles = await db.UserRoles.Where(r => r.UserId == user.Id).ToListAsync();
        var wasActive = user.IsActive; var wasPending = user.InvitationPending; var oldLock = user.LockoutEnd; var orgActive = org.IsActive;
        try
        {
            user.IsActive = false; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
            user.IsActive = wasActive; user.InvitationPending = true; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
            user.InvitationPending = wasPending; user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15); await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
            user.LockoutEnd = oldLock; org.IsActive = false; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
            org.IsActive = orgActive; db.UserRoles.RemoveRange(roles); await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/demo-admin", new { })).StatusCode);
        }
        finally
        {
            user.IsActive = wasActive; user.InvitationPending = wasPending; user.LockoutEnd = oldLock; org.IsActive = orgActive;
            foreach (var role in roles) if (!await db.UserRoles.AnyAsync(r => r.UserId == role.UserId && r.RoleId == role.RoleId)) db.UserRoles.Add(role);
            await db.SaveChangesAsync();
        }
    }
}
