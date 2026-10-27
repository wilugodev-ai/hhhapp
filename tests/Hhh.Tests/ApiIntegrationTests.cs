using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
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
            ["Authentication:LoginRequestsPerMinute"] = "100",
            ["Demo:Enabled"] = "true",
            ["SampleData:Enabled"] = "false"
        }));
    }
}

public sealed partial class ApiIntegrationTests(HhhFactory factory) : IClassFixture<HhhFactory>
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

    [Fact]
    public async Task Profit_loss_statement_reconciles_actual_budget_and_component_drilldown()
    {
        using var client = await Login("demo@hhh.local");
        var report = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?month=2026-09");
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09");
        Assert.True(report.GetProperty("coverage").GetProperty("budgetAvailable").GetBoolean());
        Assert.Equal(dashboard.GetProperty("totals").GetProperty("revenue").GetDecimal(), Total(report, "revenue").GetProperty("actual").GetDecimal());
        var actualLines = report.GetProperty("lines").EnumerateArray().ToArray();
        foreach (var type in new[] { "revenue", "medical", "administrative" })
        {
            var total = Total(report, type);
            var rows = actualLines.Where(x => x.GetProperty("type").GetString() == type).ToArray();
            Assert.Equal(total.GetProperty("actual").GetDecimal(), rows.Sum(x => x.GetProperty("actual").GetDecimal()));
            Assert.Equal(total.GetProperty("budget").GetDecimal(), rows.Sum(x => x.GetProperty("budget").GetDecimal()));
        }
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss/details?month=2026-09&category=Inpatient&type=medical&groupBy=center&pageSize=5");
        var inpatient = actualLines.Single(x => x.GetProperty("category").GetString() == "Inpatient");
        Assert.Equal(inpatient.GetProperty("actual").GetDecimal(), detail.GetProperty("groups").EnumerateArray().Sum(x => x.GetProperty("values").GetProperty("actual").GetDecimal()));
        Assert.Equal(inpatient.GetProperty("budget").GetDecimal(), detail.GetProperty("groups").EnumerateArray().Sum(x => x.GetProperty("values").GetProperty("budget").GetDecimal()));
        Assert.Equal(12, detail.GetProperty("totalRows").GetInt32()); Assert.Equal(5, detail.GetProperty("rows").GetArrayLength());
        Assert.All(detail.GetProperty("rows").EnumerateArray(), row =>
        {
            Assert.Equal("2026-10", row.GetProperty("reportMonth").GetString());
            Assert.Equal("2026-09", row.GetProperty("generalLedgerMonth").GetString());
            Assert.NotEqual(JsonValueKind.Null, row.GetProperty("sourceBatchId").ValueKind);
        });
        var page2 = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss/details?month=2026-09&category=Inpatient&type=medical&groupBy=provider&pageSize=5&page=2");
        var firstIds = detail.GetProperty("rows").EnumerateArray().Select(x => x.GetProperty("snapshotId").GetInt64()).ToArray();
        Assert.DoesNotContain(page2.GetProperty("rows").EnumerateArray(), row => firstIds.Contains(row.GetProperty("snapshotId").GetInt64()));
    }

    [Fact]
    public async Task Quarterly_and_year_to_date_budget_comparisons_use_matching_months()
    {
        using var client = await Login("demo@hhh.local");
        var quarter = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?period=quarter&year=2026&quarter=3");
        Assert.Equal("2026-07", quarter.GetProperty("startMonth").GetString()); Assert.Equal("2026-09", quarter.GetProperty("endMonth").GetString());
        var monthlyRevenue = 0m; var monthlyBudget = 0m;
        foreach (var month in new[] { "2026-07", "2026-08", "2026-09" })
        {
            var report = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?month=" + month);
            monthlyRevenue += Total(report, "revenue").GetProperty("actual").GetDecimal();
            monthlyBudget += Total(report, "revenue").GetProperty("budget").GetDecimal();
        }
        Assert.Equal(monthlyRevenue, Total(quarter, "revenue").GetProperty("actual").GetDecimal());
        Assert.Equal(monthlyBudget, Total(quarter, "revenue").GetProperty("budget").GetDecimal());
        var year = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?period=year&year=2026");
        var coverage = year.GetProperty("coverage");
        Assert.Equal(9, coverage.GetProperty("actualMonths").GetArrayLength()); Assert.True(coverage.GetProperty("isPartial").GetBoolean());
        Assert.Equal(108, coverage.GetProperty("actualSnapshots").GetInt32()); Assert.Equal(108, coverage.GetProperty("budgetSnapshots").GetInt32());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?year=2026");
        Assert.Equal(dashboard.GetProperty("totals").GetProperty("netProfit").GetDecimal(), Total(year, "profit").GetProperty("actual").GetDecimal());
        var future = year.GetProperty("trends").EnumerateArray().Last();
        Assert.Equal(JsonValueKind.Null, future.GetProperty("actual").ValueKind); Assert.True(future.GetProperty("budget").GetDecimal() > 0);
        var fourth = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?period=quarter&year=2026&quarter=4");
        Assert.Equal(JsonValueKind.Null, Total(fourth, "revenue").GetProperty("actual").ValueKind);
        Assert.True(Total(fourth, "revenue").GetProperty("budget").GetDecimal() > 0);
        Assert.Equal("unavailable", Total(fourth, "revenue").GetProperty("budgetVariance").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Budget_plans_are_isolated_and_profit_loss_csv_uses_the_selected_period()
    {
        using var client = await Login("demo@hhh.local"); using var other = await Login("summit@hhh.local");
        var plan = await other.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?month=2026-09");
        var id = plan.GetProperty("budgetPlan").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/analytics/profit-loss?month=2026-09&budgetPlan=" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/analytics/profit-loss?period=quarter&quarter=5")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/analytics/profit-loss/details?month=2026-09&type=medical&category=Inpatient&groupBy=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/analytics/profit-loss/details?month=2026-09&type=medical&category=Unknown")).StatusCode);
        var csv = await client.GetStringAsync("/api/reports/profit-loss.csv?period=quarter&year=2026&quarter=3");
        Assert.Contains("Actual minus budget", csv); Assert.Contains("\"quarter\",\"2026-07\",\"2026-09\"", csv); Assert.Contains("Baseline v1", csv);
        using var viewer = await Login("viewer@hhh.local");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/analytics/profit-loss?month=2026-09")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/reports/profit-loss.csv?month=2026-09")).StatusCode);
    }

    [Fact]
    public async Task Missing_budget_component_suppresses_variance_instead_of_inventing_zero_budget()
    {
        using var client = await Login("demo@hhh.local");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        var org = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var row = await db.BudgetLines.FirstAsync(x => x.Category == "Inpatient" && db.BudgetSnapshots.Any(s => s.Id == x.BudgetSnapshotId && s.OrganizationId == org && s.Month == new DateOnly(2026, 9, 1)));
        var original = row.Category;
        try
        {
            row.Category = "Missing budget component test"; await db.SaveChangesAsync();
            var report = await client.GetFromJsonAsync<JsonElement>("/api/analytics/profit-loss?month=2026-09");
            Assert.False(report.GetProperty("coverage").GetProperty("budgetAvailable").GetBoolean());
            Assert.All(report.GetProperty("lines").EnumerateArray(), line =>
            {
                Assert.Equal(JsonValueKind.Null, line.GetProperty("budget").ValueKind);
                Assert.Equal("unavailable", line.GetProperty("budgetVariance").GetProperty("status").GetString());
            });
            Assert.Equal(JsonValueKind.Null, report.GetProperty("trends")[0].GetProperty("budget").ValueKind);
        }
        finally { row.Category = original; await db.SaveChangesAsync(); }
    }

    private static JsonElement Total(JsonElement report, string type) => report.GetProperty("totals").EnumerateArray().Single(x => x.GetProperty("type").GetString() == type);

    [Fact]
    public async Task Rolling_mlr_uses_summed_inputs_and_driver_contributions_reconcile()
    {
        using var client = await Login("demo@hhh.local");
        var report = await client.GetFromJsonAsync<JsonElement>("/api/analytics/mlr?month=2026-09&window=3&groupBy=payer");
        var revenue = 0m; var expenses = 0m;
        foreach (var month in new[] { "2026-07", "2026-08", "2026-09" })
        {
            var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=" + month);
            revenue += dashboard.GetProperty("totals").GetProperty("mlrRevenue").GetDecimal();
            expenses += dashboard.GetProperty("totals").GetProperty("mlrExpenses").GetDecimal();
        }
        var current = report.GetProperty("current");
        Assert.Equal(revenue, current.GetProperty("revenue").GetDecimal()); Assert.Equal(expenses, current.GetProperty("expenses").GetDecimal());
        Assert.Equal(decimal.Round(expenses / revenue * 100, 2), current.GetProperty("mlr").GetDecimal());
        Assert.Equal(revenue, report.GetProperty("groups").EnumerateArray().Sum(x => x.GetProperty("values").GetProperty("revenue").GetDecimal()));
        var attribution = report.GetProperty("attribution"); Assert.True(attribution.GetProperty("available").GetBoolean());
        Assert.Equal(attribution.GetProperty("changePercentagePoints").GetDecimal(),
            attribution.GetProperty("drivers").EnumerateArray().Sum(x => x.GetProperty("contributionPercentagePoints").GetDecimal()) + attribution.GetProperty("roundingPercentagePoints").GetDecimal());
        var reportLimited = await client.GetFromJsonAsync<JsonElement>("/api/analytics/mlr?month=2026-09&reportMonth=2026-10&window=3");
        Assert.Equal(JsonValueKind.Null, reportLimited.GetProperty("current").GetProperty("mlr").ValueKind);
        Assert.Equal(2, reportLimited.GetProperty("current").GetProperty("missingMonths").GetArrayLength()); Assert.Equal(0, reportLimited.GetProperty("alerts").GetArrayLength());
        Assert.False(reportLimited.GetProperty("attribution").GetProperty("available").GetBoolean());
        var annual = await client.GetFromJsonAsync<JsonElement>("/api/analytics/mlr?month=2026-09&window=12");
        Assert.True(annual.GetProperty("current").GetProperty("complete").GetBoolean()); Assert.False(annual.GetProperty("previous").GetProperty("complete").GetBoolean());
        Assert.False(annual.GetProperty("attribution").GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task Saved_mlr_definition_is_versioned_audited_and_consistent_with_dashboard_and_export()
    {
        using var client = await Login("demo@hhh.local"); using var other = await Login("summit@hhh.local");
        var original = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
        var otherBefore = await other.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
        var before = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09");
        var revenue = original.GetProperty("revenueCategories").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var expenses = original.GetProperty("expenseCategories").EnumerateArray().Select(x => x.GetString()!).ToArray();
        try
        {
            var body = new { name = "Test definition without pharmacy", expectedVersion = original.GetProperty("version").GetInt32(), revenueCategories = revenue, expenseCategories = expenses.Where(x => x != "Pharmacy").ToArray() };
            var response = await client.PutAsJsonAsync("/api/mlr/definition", body); response.EnsureSuccessStatusCode();
            var saved = (await response.Content.ReadFromJsonAsync<JsonElement>());
            Assert.Equal(body.expectedVersion + 1, saved.GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/mlr/definition", body)).StatusCode);
            var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09");
            var mlr = await client.GetFromJsonAsync<JsonElement>("/api/analytics/mlr?month=2026-09");
            Assert.Equal(mlr.GetProperty("current").GetProperty("mlr").GetDecimal(), dashboard.GetProperty("totals").GetProperty("mlr").GetDecimal());
            Assert.True(dashboard.GetProperty("totals").GetProperty("mlrExpenses").GetDecimal() < before.GetProperty("totals").GetProperty("mlrExpenses").GetDecimal());
            Assert.DoesNotContain(mlr.GetProperty("attribution").GetProperty("drivers").EnumerateArray(), x => x.GetProperty("category").GetString() == "Pharmacy");
            Assert.Equal(before.GetProperty("totals").GetProperty("netProfit").GetDecimal(), dashboard.GetProperty("totals").GetProperty("netProfit").GetDecimal());
            Assert.Equal(before.GetProperty("totals").GetProperty("medicalExpenses").GetDecimal(), dashboard.GetProperty("totals").GetProperty("medicalExpenses").GetDecimal());
            Assert.Contains(body.name, await client.GetStringAsync("/api/reports/financial.csv?month=2026-09"));
            var otherAfter = await other.GetFromJsonAsync<JsonElement>("/api/mlr/configuration"); Assert.Equal(otherBefore.GetProperty("version").GetInt32(), otherAfter.GetProperty("version").GetInt32());
            using var admin = await Login("admin@hhh.local"); var audit = await admin.GetFromJsonAsync<JsonElement>("/api/audit");
            Assert.Contains(audit.EnumerateArray(), x => x.GetProperty("action").GetString() == "mlr.definition.updated" && x.GetProperty("detailsJson").GetString()!.Contains(body.name, StringComparison.Ordinal));
        }
        finally
        {
            var current = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
            (await client.PutAsJsonAsync("/api/mlr/definition", new { name = original.GetProperty("name").GetString(), expectedVersion = current.GetProperty("version").GetInt32(), revenueCategories = revenue, expenseCategories = expenses })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Group_targets_work_for_payers_centers_providers_and_plans_and_can_be_reset()
    {
        using var client = await Login("demo@hhh.local"); var metadata = await client.GetFromJsonAsync<JsonElement>("/api/analytics/filters");
        foreach (var (kind, group) in new[] { ("Payer", "payer"), ("MedicalCenter", "center"), ("Provider", "provider"), ("Plan", "plan") })
        {
            var dimension = metadata.GetProperty("dimensions").EnumerateArray().First(x => x.GetProperty("kind").GetString() == kind).GetProperty("id").GetString()!;
            var original = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
            var prior = original.GetProperty("targets").EnumerateArray().Where(x => x.GetProperty("dimensionId").GetString() == dimension).ToArray();
            try
            {
                var response = await client.PutAsJsonAsync("/api/mlr/targets/" + dimension, new { target = 55m, expectedVersion = prior.Length == 0 ? 0 : prior[0].GetProperty("version").GetInt32() }); response.EnsureSuccessStatusCode();
                var updated = await response.Content.ReadFromJsonAsync<JsonElement>(); var version = updated.GetProperty("targets").EnumerateArray().Single(x => x.GetProperty("dimensionId").GetString() == dimension).GetProperty("version").GetInt32();
                var report = await client.GetFromJsonAsync<JsonElement>("/api/analytics/mlr?month=2026-09&window=3&groupBy=" + group);
                var row = report.GetProperty("groups").EnumerateArray().Single(x => x.GetProperty("id").GetString() == dimension);
                Assert.Equal(55m, row.GetProperty("target").GetDecimal()); Assert.Equal("above-target", row.GetProperty("status").GetString());
                Assert.Contains(report.GetProperty("alerts").EnumerateArray(), x => x.GetProperty("id").GetString() == dimension);
                Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/mlr/targets/" + dimension, new { target = 60m, expectedVersion = version - 1 })).StatusCode);
                if (kind == "Payer")
                {
                    var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/analytics/dashboard?month=2026-09");
                    Assert.Equal(55m, dashboard.GetProperty("payers").EnumerateArray().Single(x => x.GetProperty("id").GetString() == dimension).GetProperty("mlrTarget").GetDecimal());
                }
            }
            finally
            {
                var current = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration"); var rule = current.GetProperty("targets").EnumerateArray().Single(x => x.GetProperty("dimensionId").GetString() == dimension);
                if (prior.Length == 0) (await client.DeleteAsync("/api/mlr/targets/" + dimension + "?version=" + rule.GetProperty("version").GetInt32())).EnsureSuccessStatusCode();
                else (await client.PutAsJsonAsync("/api/mlr/targets/" + dimension, new { target = prior[0].GetProperty("target").GetDecimal(), expectedVersion = rule.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
            }
        }
    }

    [Fact]
    public async Task Mlr_configuration_and_exports_enforce_roles_scope_and_input_validation()
    {
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/analytics/mlr")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/reports/mlr.csv")).StatusCode);
        using var viewer = await Login("viewer@hhh.local");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/analytics/mlr?month=2026-09")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync("/api/mlr/definition", new { name = "Denied", expectedVersion = 0, revenueCategories = new[] { "Capitation" }, expenseCategories = new[] { "Inpatient" } })).StatusCode);
        using var cfo = await Login("demo@hhh.local"); using var other = await Login("summit@hhh.local");
        var metadata = await other.GetFromJsonAsync<JsonElement>("/api/analytics/filters"); var foreign = metadata.GetProperty("dimensions")[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest, (await cfo.PutAsJsonAsync("/api/mlr/targets/" + foreign, new { target = 80, expectedVersion = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync("/api/mlr/targets/" + foreign + "?version=1")).StatusCode);
        var config = await cfo.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
        Assert.Equal(HttpStatusCode.BadRequest, (await cfo.PutAsJsonAsync("/api/mlr/definition", new { name = "Invalid", expectedVersion = config.GetProperty("version").GetInt32(), revenueCategories = new[] { "Capitation" }, expenseCategories = new[] { "Administrative" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cfo.GetAsync("/api/analytics/mlr?window=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cfo.GetAsync("/api/analytics/mlr?groupBy=month")).StatusCode);
        var csv = await viewer.GetStringAsync("/api/reports/mlr.csv?month=2026-09&window=6&groupBy=provider");
        Assert.Contains("Definition version", csv); Assert.Contains("Gap percentage points", csv); Assert.Contains("\"2026-09\",\"6\",\"provider\"", csv);
    }

    [Fact]
    public async Task Concurrent_target_updates_allow_one_winner_and_report_a_version_conflict()
    {
        using var client = await Login("demo@hhh.local"); var metadata = await client.GetFromJsonAsync<JsonElement>("/api/analytics/filters");
        var id = metadata.GetProperty("dimensions").EnumerateArray().First(x => x.GetProperty("kind").GetString() == "Region").GetProperty("id").GetString()!;
        var original = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
        var before = original.GetProperty("targets").EnumerateArray().Where(x => x.GetProperty("dimensionId").GetString() == id).ToArray();
        var version = before.Length == 0 ? 0 : before[0].GetProperty("version").GetInt32();
        try
        {
            var results = await Task.WhenAll(client.PutAsJsonAsync("/api/mlr/targets/" + id, new { target = 81, expectedVersion = version }),
                client.PutAsJsonAsync("/api/mlr/targets/" + id, new { target = 82, expectedVersion = version }));
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
            var current = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration");
            Assert.Equal(version + 1, current.GetProperty("targets").EnumerateArray().Single(x => x.GetProperty("dimensionId").GetString() == id).GetProperty("version").GetInt32());
        }
        finally
        {
            var current = await client.GetFromJsonAsync<JsonElement>("/api/mlr/configuration"); var rule = current.GetProperty("targets").EnumerateArray().Single(x => x.GetProperty("dimensionId").GetString() == id);
            if (before.Length == 0) (await client.DeleteAsync("/api/mlr/targets/" + id + "?version=" + rule.GetProperty("version").GetInt32())).EnsureSuccessStatusCode();
            else (await client.PutAsJsonAsync("/api/mlr/targets/" + id, new { target = before[0].GetProperty("target").GetDecimal(), expectedVersion = rule.GetProperty("version").GetInt32() })).EnsureSuccessStatusCode();
        }
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
