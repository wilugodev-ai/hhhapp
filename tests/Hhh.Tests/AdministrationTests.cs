using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hhh.Domain;
using Hhh.Api;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hhh.Tests;

public sealed partial class ApiIntegrationTests
{
    private const string HorizonOrg = "11111111-1111-1111-1111-111111111111";
    private const string SummitOrg = "22222222-2222-2222-2222-222222222222";
    private sealed record AccessAccount(string Id, string Email, string Org, JsonElement Invitation);

    [Fact]
    public async Task Administration_is_restricted_to_administrators_and_their_organization()
    {
        foreach (var email in new[] { "viewer@hhh.local", "analyst@hhh.local", "demo@hhh.local" })
        {
            using var client = await Login(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/organizations")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users", new { email = "nobody@example.com", displayName = "Nobody", role = Roles.Viewer })).StatusCode);
        }
        using var admin = await Login("admin@hhh.local");
        var organizations = await admin.GetFromJsonAsync<JsonElement>("/api/admin/organizations");
        Assert.Single(organizations.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/organizations/{SummitOrg}/users")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/organizations/{SummitOrg}/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/admin/organizations", new { name = "Other", adminEmail = "none@example.com", adminName = "None" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users", new { email = "no@example.com", displayName = "Escalation", role = Roles.SystemAdministrator })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/admin/organizations/{SummitOrg}/users", new { email = "no@example.com", displayName = "Other Org", role = Roles.Viewer })).StatusCode);
        using var root = await Login("sysadmin@hhh.local");
        var global = await root.GetFromJsonAsync<JsonElement>("/api/admin/organizations");
        Assert.True(global.GetArrayLength() >= 2);
        var directory = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/organizations/{HorizonOrg}/users");
        var system = directory.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("role").GetString() == Roles.SystemAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users/{system.GetProperty("id").GetString()}/revoke", new { expectedVersion = system.GetProperty("version").GetInt32() })).StatusCode);
    }

    [Fact]
    public async Task Invitation_requires_csrf_is_hashed_single_use_and_does_not_grant_access_before_setup()
    {
        try
        {
            using var admin = await Login("admin@hhh.local");
            var account = await AccessInvite(admin);
            using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await Token(anonymous);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { account.Email, password = SyntheticSeed.DemoPassword })).StatusCode);
            var body = InvitationBody(account);
            using (var noCsrf = factory.CreateClient()) Assert.Equal(HttpStatusCode.BadRequest, (await noCsrf.PostAsJsonAsync("/api/auth/activate", body)).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>(); var user = await db.Users.SingleAsync(u => u.Id == account.Id);
                Assert.Null(user.PasswordHash); Assert.NotEqual(body.Token, user.InvitationHash); Assert.Equal(64, user.InvitationHash!.Length);
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/activate", body with { Token = new string('x', 43) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/activate", body with { Password = "onlylowercaseletters" })).StatusCode);
            var results = await Task.WhenAll(anonymous.PostAsJsonAsync("/api/auth/activate", body), anonymous.PostAsJsonAsync("/api/auth/activate", body));
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.BadRequest);
            using var signedIn = await Login(account.Email);
            var me = await signedIn.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(HorizonOrg, me.GetProperty("organizationId").GetString());
            Assert.Contains(Roles.Viewer, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
            var audit = await admin.GetStringAsync($"/api/admin/organizations/{HorizonOrg}/audit");
            Assert.Contains("admin.user.invited", audit); Assert.Contains("auth.account.setup", audit);
            Assert.DoesNotContain(body.Token, audit); Assert.DoesNotContain(SyntheticSeed.DemoPassword, audit); Assert.DoesNotContain("invitationHash", audit);
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Role_activation_and_revocation_changes_expire_sessions_and_detect_stale_updates()
    {
        try
        {
            using var admin = await Login("admin@hhh.local"); var account = await AccessInvite(admin, activate: true);
            using var viewer = await Login(account.Email); var row = await AccessRow(admin, account);
            var updated = await admin.PutAsJsonAsync(AccessUrl(account), new { displayName = "Updated Person", role = Roles.Analyst, isActive = true, expectedVersion = row.GetProperty("version").GetInt32() });
            updated.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/analytics/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(AccessUrl(account), new { displayName = "Stale", role = Roles.Viewer, isActive = true, expectedVersion = row.GetProperty("version").GetInt32() })).StatusCode);
            using var analyst = await Login(account.Email);
            row = await AccessRow(admin, account);
            (await admin.PostAsJsonAsync(AccessUrl(account) + "/revoke", new { expectedVersion = row.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await analyst.GetAsync("/api/auth/me")).StatusCode);
            row = await AccessRow(admin, account);
            (await admin.PutAsJsonAsync(AccessUrl(account), new { displayName = "Updated Person", role = Roles.Analyst, isActive = false, expectedVersion = row.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            using var blocked = factory.CreateClient(); await Token(blocked);
            Assert.Equal(HttpStatusCode.Unauthorized, (await blocked.PostAsJsonAsync("/api/auth/login", new { account.Email, password = SyntheticSeed.DemoPassword })).StatusCode);
            row = await AccessRow(admin, account);
            (await admin.PutAsJsonAsync(AccessUrl(account), new { displayName = "Updated Person", role = Roles.Analyst, isActive = true, expectedVersion = row.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/auth/me")).StatusCode);
            using var restored = await Login(account.Email);
            var me = await restored.GetFromJsonAsync<JsonElement>("/api/auth/me"); Assert.Contains(Roles.Analyst, me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Expired_replaced_and_revoked_setup_links_are_rejected_and_recovery_resets_password()
    {
        try
        {
            using var admin = await Login("admin@hhh.local"); var pending = await AccessInvite(admin);
            using var client = factory.CreateClient(); await Token(client);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>(); var user = await db.Users.SingleAsync(u => u.Id == pending.Id);
                user.InvitationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(pending))).StatusCode);
            var row = await AccessRow(admin, pending);
            var replacement = await admin.PostAsJsonAsync(AccessUrl(pending) + "/invitation", new { expectedVersion = row.GetProperty("version").GetInt32() }); replacement.EnsureSuccessStatusCode();
            pending = pending with { Invitation = await replacement.Content.ReadFromJsonAsync<JsonElement>() };
            (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(pending))).EnsureSuccessStatusCode();
            using var active = await Login(pending.Email); row = await AccessRow(admin, pending);
            var recovery = await admin.PostAsJsonAsync(AccessUrl(pending) + "/recovery", new { expectedVersion = row.GetProperty("version").GetInt32() }); recovery.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await active.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(pending))).StatusCode);
            var recovered = pending with { Invitation = await recovery.Content.ReadFromJsonAsync<JsonElement>() };
            const string nextPassword = "Recovered-Test-2026!";
            (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(recovered) with { Password = nextPassword })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { pending.Email, password = SyntheticSeed.DemoPassword })).StatusCode);
            (await client.PostAsJsonAsync("/api/auth/login", new { pending.Email, password = nextPassword })).EnsureSuccessStatusCode();
            var pending2 = await AccessInvite(admin); row = await AccessRow(admin, pending2);
            (await admin.PutAsJsonAsync(AccessUrl(pending2), new { displayName = "Revoked Invite", role = Roles.Viewer, isActive = false, expectedVersion = row.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            await Token(client); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(pending2))).StatusCode);
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Organization_provisioning_and_concurrent_last_administrator_protection_are_atomic()
    {
        try
        {
            using var root = await Login("sysadmin@hhh.local");
            var org = await NewAccessOrganization(root); var first = org.Account;
            var row = await AccessRow(root, first);
            Assert.Equal(HttpStatusCode.Conflict, (await root.PutAsJsonAsync(AccessUrl(first), new { displayName = "Only Admin", role = Roles.Viewer, isActive = true, expectedVersion = row.GetProperty("version").GetInt32() })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await root.PostAsJsonAsync(AccessUrl(first) + "/recovery", new { expectedVersion = row.GetProperty("version").GetInt32() })).StatusCode);
            var second = await AccessInvite(root, Roles.OrganizationAdministrator, true, org.Id);
            var other = await AccessRow(root, second);
            var results = await Task.WhenAll(root.PutAsJsonAsync(AccessUrl(first), new { displayName = "First Admin", role = Roles.OrganizationAdministrator, isActive = false, expectedVersion = row.GetProperty("version").GetInt32() }),
                root.PutAsJsonAsync(AccessUrl(second), new { displayName = "Second Admin", role = Roles.OrganizationAdministrator, isActive = false, expectedVersion = other.GetProperty("version").GetInt32() }));
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var directory = await root.GetFromJsonAsync<JsonElement>($"/api/admin/organizations/{org.Id}/users");
            Assert.Single(directory.GetProperty("users").EnumerateArray(), u => u.GetProperty("isActive").GetBoolean());
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Organization_deactivation_blocks_access_and_old_sessions_do_not_revive()
    {
        try
        {
            using var root = await Login("sysadmin@hhh.local"); var org = await NewAccessOrganization(root);
            using var member = await Login(org.Account.Email);
            var response = await root.PutAsJsonAsync($"/api/admin/organizations/{org.Id}", new { name = "access-test-inactive", isActive = false, expectedVersion = 1 }); response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
            (await root.PutAsJsonAsync($"/api/admin/organizations/{org.Id}", new { name = "access-test-reactivated", isActive = true, expectedVersion = 2 })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/auth/me")).StatusCode);
            using var fresh = await Login(org.Account.Email);
            var own = await root.GetFromJsonAsync<JsonElement>("/api/auth/me");
            var organizations = await root.GetFromJsonAsync<JsonElement>("/api/admin/organizations");
            var current = organizations.EnumerateArray().Single(x => x.GetProperty("id").GetString() == own.GetProperty("organizationId").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await root.PutAsJsonAsync($"/api/admin/organizations/{HorizonOrg}", new { name = "Horizon", isActive = false, expectedVersion = current.GetProperty("version").GetInt32() })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await root.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users/{own.GetProperty("id").GetString()}/revoke", new { expectedVersion = 1 })).StatusCode);
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Directory_filters_pages_validation_and_csrf_are_enforced()
    {
        try
        {
            using var admin = await Login("admin@hhh.local");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
                db.Users.AddRange(Enumerable.Range(1, 27).Select(i => new AppUser { OrganizationId = Guid.Parse(HorizonOrg), UserName = $"access-test-page-{i}@hhh.local", Email = $"access-test-page-{i}@hhh.local", DisplayName = $"Directory Fixture {i:D2}", InvitationPending = true }));
                await db.SaveChangesAsync();
            }
            var first = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/organizations/{HorizonOrg}/users?search=Directory%20Fixture&status=invited&page=1");
            var second = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/organizations/{HorizonOrg}/users?search=Directory%20Fixture&status=invited&page=2");
            Assert.Equal(27, first.GetProperty("total").GetInt32()); Assert.Equal(25, first.GetProperty("users").GetArrayLength()); Assert.Equal(2, second.GetProperty("users").GetArrayLength());
            Assert.DoesNotContain(Roles.SystemAdministrator, first.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/admin/organizations/{HorizonOrg}/users?status=invalid")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/admin/organizations/{HorizonOrg}/audit?page=0")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users", new { email = "invalid", displayName = "New User", role = Roles.Viewer })).StatusCode);
            var account = await AccessInvite(admin);
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/organizations/{HorizonOrg}/users", new { account.Email, displayName = "Duplicate", role = Roles.Viewer })).StatusCode);
            admin.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(AccessUrl(account) + "/revoke", new { expectedVersion = 1 })).StatusCode);
        }
        finally { await ClearAccessFixtures(); }
    }

    [Fact]
    public async Task Temporary_lockout_can_be_cleared_and_bootstrap_cannot_create_a_second_system_administrator()
    {
        try
        {
            using var admin = await Login("admin@hhh.local"); var account = await AccessInvite(admin, activate: true);
            using var client = factory.CreateClient(); await Token(client);
            for (var i = 0; i < 5; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = "Incorrect-Test-Password1!" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { account.Email, password = SyntheticSeed.DemoPassword })).StatusCode);
            var row = await AccessRow(admin, account); Assert.NotEqual(JsonValueKind.Null, row.GetProperty("lockoutEnd").ValueKind);
            (await admin.PostAsJsonAsync(AccessUrl(account) + "/unlock", new { expectedVersion = row.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            using var restored = await Login(account.Email);
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AdministrationService>();
            var error = await Assert.ThrowsAsync<AdministrationException>(() => service.Bootstrap("access-test-bootstrap@hhh.local", "Bootstrap Person", "access-test-bootstrap", CancellationToken.None));
            Assert.Equal(409, error.Status);
        }
        finally { await ClearAccessFixtures(); }
    }

    private async Task<AccessAccount> AccessInvite(HttpClient admin, string role = Roles.Viewer, bool activate = false, string org = HorizonOrg)
    {
        var email = $"access-test-{Guid.NewGuid():N}@hhh.local";
        var response = await admin.PostAsJsonAsync($"/api/admin/organizations/{org}/users", new { email, displayName = "Access Test Person", role });
        response.EnsureSuccessStatusCode(); var invitation = await response.Content.ReadFromJsonAsync<JsonElement>();
        var account = new AccessAccount(invitation.GetProperty("user").GetProperty("id").GetString()!, email, org, invitation);
        if (activate) await ActivateFixture(account);
        return account;
    }
    private async Task ActivateFixture(AccessAccount account)
    {
        using var client = factory.CreateClient(); await Token(client);
        (await client.PostAsJsonAsync("/api/auth/activate", InvitationBody(account))).EnsureSuccessStatusCode();
    }
    private sealed record SetupBody(string UserId, string Token, string Password);
    private static SetupBody InvitationBody(AccessAccount account)
    {
        var fragment = account.Invitation.GetProperty("setupPath").GetString()!.Split('#')[1];
        var values = fragment.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        return new(values["user"], values["token"], SyntheticSeed.DemoPassword);
    }
    private static string AccessUrl(AccessAccount account) => $"/api/admin/organizations/{account.Org}/users/{account.Id}";
    private static async Task<JsonElement> AccessRow(HttpClient client, AccessAccount account)
    {
        var directory = await client.GetFromJsonAsync<JsonElement>($"/api/admin/organizations/{account.Org}/users?search={Uri.EscapeDataString(account.Email)}");
        return directory.GetProperty("users").EnumerateArray().Single();
    }
    private async Task<(string Id, AccessAccount Account)> NewAccessOrganization(HttpClient root)
    {
        var email = $"access-test-{Guid.NewGuid():N}@hhh.local";
        var response = await root.PostAsJsonAsync("/api/admin/organizations", new { name = "access-test-organization", adminEmail = email, adminName = "First Administrator" });
        response.EnsureSuccessStatusCode(); var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = result.GetProperty("organizationId").GetString()!; var invitation = result.GetProperty("invitation");
        var account = new AccessAccount(invitation.GetProperty("user").GetProperty("id").GetString()!, email, orgId, invitation);
        await ActivateFixture(account); return (orgId, account);
    }
    private async Task ClearAccessFixtures()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        db.Users.RemoveRange(await db.Users.Where(u => u.Email != null && u.Email.StartsWith("access-test-")).ToListAsync());
        db.Organizations.RemoveRange(await db.Organizations.Where(o => o.Name.StartsWith("access-test-")).ToListAsync());
        await db.SaveChangesAsync();
    }
}
