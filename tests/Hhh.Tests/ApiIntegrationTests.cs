using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Hhh.Tests;

public sealed class HhhFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Hhh"] = Environment.GetEnvironmentVariable("HHH_TEST_CONNECTION_STRING")
                ?? "Server=.\\SQLEXPRESS01;Database=HhhIntegrationTests;Trusted_Connection=True;TrustServerCertificate=True",
            ["Database:MigrateOnStartup"] = "true",
            ["Demo:Enabled"] = "true"
        }));
    }
}

public sealed class ApiIntegrationTests(HhhFactory factory) : IClassFixture<HhhFactory>
{
    [Fact]
    public async Task Unauthenticated_data_is_protected_and_csrf_is_required()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/analytics/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/demo", new { })).StatusCode);
    }

    [Fact]
    public async Task Organizations_are_isolated_and_spoofed_scope_is_rejected()
    {
        using var first = await Login("demo@hhh.local");
        using var second = await Login("summit@hhh.local");
        var a = await first.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard");
        var b = await second.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard");
        Assert.Equal("Horizon Care Partners", a.GetProperty("organization").GetString());
        Assert.Equal("Summit Health Group", b.GetProperty("organization").GetString());
        Assert.NotEqual(a.GetProperty("totals").GetProperty("revenue").GetDecimal(), b.GetProperty("totals").GetProperty("revenue").GetDecimal());
        var metadata = await second.GetFromJsonAsync<JsonElement>("/api/analytics/filters");
        var otherPayer = metadata.GetProperty("dimensions").EnumerateArray().First(x => x.GetProperty("kind").GetString() == "Payer").GetProperty("id").GetString();
        var empty = await first.GetFromJsonAsync<JsonElement>($"/api/analytics/dashboard?payer={otherPayer}");
        Assert.Equal(0, empty.GetProperty("snapshotCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await first.GetAsync("/api/analytics/dashboard?organizationId=22222222-2222-2222-2222-222222222222")).StatusCode);
    }

    [Fact]
    public async Task Filters_reconcile_with_payer_totals_and_csv()
    {
        using var client = await Login("demo@hhh.local");
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09");
        var total = dashboard.GetProperty("totals").GetProperty("revenue").GetDecimal();
        var payers = dashboard.GetProperty("payers").EnumerateArray().ToArray();
        Assert.Equal(total, payers.Sum(p => p.GetProperty("metrics").GetProperty("revenue").GetDecimal()));
        var payer = payers[0];
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09&payer=" + payer.GetProperty("id").GetString());
        Assert.Equal(payer.GetProperty("metrics").GetProperty("revenue").GetDecimal(), filtered.GetProperty("totals").GetProperty("revenue").GetDecimal());
        var csv = await client.GetStringAsync("/api/reports/financial.csv?month=2026-09&payer=" + payer.GetProperty("id").GetString());
        Assert.Contains("Effective month,Report month", csv);
        Assert.Equal(5, csv.Trim().Split('\n').Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/analytics/dashboard?month=broken")).StatusCode);
    }

    [Fact]
    public async Task Year_filter_aligns_trends_and_ending_membership_with_the_selected_year()
    {
        using var client = await Login("demo@hhh.local");
        var year = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?year=2025");
        Assert.Equal("2025-12", year.GetProperty("period").GetString());
        Assert.Equal(144, year.GetProperty("snapshotCount").GetInt32());
        Assert.Equal(12, year.GetProperty("trends").GetArrayLength());
        Assert.All(year.GetProperty("trends").EnumerateArray(), item => Assert.StartsWith("2025-", item.GetProperty("month").GetString()));
        var last = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2025-12");
        Assert.Equal(last.GetProperty("totals").GetProperty("memberMonths").GetInt32(), year.GetProperty("endingMembership").GetInt32());
        Assert.True(year.GetProperty("totals").GetProperty("memberMonths").GetInt32() > year.GetProperty("endingMembership").GetInt32());
    }

    [Fact]
    public async Task Cfo_setting_changes_persist_and_are_audited_for_administrators()
    {
        using var cfo = await Login("demo@hhh.local");
        var original = await cfo.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var target = original.GetProperty("mlrTarget").GetDecimal();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await cfo.PutAsJsonAsync("/api/settings", new { mlrTarget = 84.5m })).StatusCode);
            var dashboard = await cfo.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard");
            Assert.Equal(84.5m, dashboard.GetProperty("mlrTarget").GetDecimal());
            using var admin = await Login("admin@hhh.local");
            var audit = await admin.GetFromJsonAsync<JsonElement>("/api/audit");
            Assert.Contains(audit.EnumerateArray(), e => e.GetProperty("action").GetString() == "settings.mlr-target.updated");
            Assert.Equal(HttpStatusCode.BadRequest, (await cfo.PutAsJsonAsync("/api/settings", new { mlrTarget = 101 })).StatusCode);
        }
        finally { (await cfo.PutAsJsonAsync("/api/settings", new { mlrTarget = target })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Viewer_cannot_change_settings_or_read_audit_and_logout_revokes_session()
    {
        using var client = await Login("viewer@hhh.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/settings", new { mlrTarget = 80 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/analytics/dashboard")).StatusCode);
    }

    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Token(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = SyntheticSeed.DemoPassword });
        response.EnsureSuccessStatusCode();
        await Token(client);
        return client;
    }

    private static async Task Token(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf"); response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("XSRF-TOKEN="));
        var token = cookie.Split(';')[0]["XSRF-TOKEN=".Length..];
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(token));
    }
}
