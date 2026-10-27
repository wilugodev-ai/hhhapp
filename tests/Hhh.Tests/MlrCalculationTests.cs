using Hhh.Domain;
using Xunit;

namespace Hhh.Tests;

public sealed class MlrCalculationTests
{
    [Fact]
    public void Mlr_is_weighted_by_revenue_and_membership_is_counted_once()
    {
        var first = Row(new(2026, 9, 1), 1000, 500, 10); var second = Row(new(2026, 9, 1), 9000, 8100, 30); second.PayerId = Guid.NewGuid();
        var result = MlrCalculation.Window([first, second], new(2026, 9, 1), 1, MlrSelection.Standard);
        Assert.Equal(86m, result.Mlr); Assert.Equal(40, result.MemberMonths); Assert.Equal(8600m, result.Expenses); Assert.True(result.Complete);
    }

    [Fact]
    public void Incomplete_calendar_windows_remain_unavailable()
    {
        var result = MlrCalculation.Window([Row(new(2026, 7, 1), 100, 80), Row(new(2026, 9, 1), 100, 90)], new(2026, 9, 1), 3, MlrSelection.Standard);
        Assert.Null(result.Mlr); Assert.Equal(new[] { "2026-08" }, result.MissingMonths); Assert.False(result.Complete);
        Assert.Equal(170m, result.Expenses); Assert.Equal(200m, result.Revenue);
    }

    [Fact]
    public void Rolling_twelve_months_excludes_the_thirteenth_month()
    {
        var rows = Enumerable.Range(0, 12).Select(i => Row(new DateOnly(2025, 10, 1).AddMonths(i), 100, 80)).ToList();
        rows.Add(Row(new(2025, 9, 1), 10000, 100));
        var result = MlrCalculation.Window(rows, new(2026, 9, 1), 12, MlrSelection.Standard);
        Assert.Equal(80m, result.Mlr); Assert.Equal(1200m, result.Revenue); Assert.Equal(12, result.SnapshotCount);
    }

    [Fact]
    public void Definitions_change_only_mlr_inclusion_not_profit_or_other_financial_totals()
    {
        var row = Row(new(2026, 9, 1), 800, 400);
        row.Revenue.Add(new() { Category = "Risk adjustment", Amount = 200 });
        row.Expenses.Add(new() { Category = "Pharmacy", Amount = 200 });
        row.Expenses.Add(new() { Category = "Administrative", Amount = 100, IsAdministrative = true });
        var definition = new MlrSelection("Selected components", 1, ["Capitation"], ["Inpatient"]);
        var metrics = FinancialMetrics.Calculate([row], definition);
        Assert.Equal(50m, metrics.Mlr); Assert.Equal(800m, metrics.MlrRevenue); Assert.Equal(400m, metrics.MlrExpenses);
        Assert.Equal(1000m, metrics.Revenue); Assert.Equal(600m, metrics.MedicalExpenses); Assert.Equal(300m, metrics.NetProfit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Nonpositive_revenue_does_not_produce_a_performance_ratio(decimal revenue)
        => Assert.Null(MlrCalculation.Window([Row(new(2026, 9, 1), revenue, 80)], new(2026, 9, 1), 1, MlrSelection.Standard).Mlr);

    [Fact]
    public void Missing_selected_categories_are_not_substituted_with_zero()
    {
        var selection = new MlrSelection("Pharmacy", 1, ["Capitation"], ["Pharmacy"]);
        var result = MlrCalculation.Window([Row(new(2026, 9, 1), 100, 80)], new(2026, 9, 1), 1, selection);
        Assert.False(result.ComponentsComplete); Assert.Null(result.Mlr);
    }

    [Fact]
    public void Driver_attribution_reconciles_revenue_growth_and_recovery_changes()
    {
        var previous = Row(new(2026, 8, 1), 1000, 600); previous.Expenses.AddRange([new() { Category = "Pharmacy", Amount = 250 }, new() { Category = "Stop-loss recoveries", Amount = -50 }]);
        var current = Row(new(2026, 9, 1), 1200, 800); current.Expenses.AddRange([new() { Category = "Pharmacy", Amount = 250 }, new() { Category = "Stop-loss recoveries", Amount = -30 }]);
        var before = MlrCalculation.Window([previous], new(2026, 8, 1), 1, MlrSelection.Standard);
        var after = MlrCalculation.Window([current], new(2026, 9, 1), 1, MlrSelection.Standard);
        var result = MlrCalculation.Attribute([current], [previous], after, before, MlrSelection.Standard);
        Assert.True(result.Available); Assert.Equal(5m, result.ChangePercentagePoints);
        Assert.Equal(result.ChangePercentagePoints, result.Drivers.Sum(x => x.ContributionPercentagePoints) + result.RoundingPercentagePoints);
        Assert.Equal(-17m, result.Drivers.Single(x => x.Type == "revenue").ContributionPercentagePoints);
        Assert.Equal(2m, result.Drivers.Single(x => x.Category == "Stop-loss recoveries").ContributionPercentagePoints);
    }

    [Fact]
    public void Duplicate_reporting_versions_cannot_inflate_mlr_inputs()
    {
        var first = Row(new(2026, 9, 1), 100, 80); var second = Row(new(2026, 9, 1), 110, 85);
        Assert.Throws<ArgumentException>(() => MlrCalculation.Window([first, second], new(2026, 9, 1), 1, MlrSelection.Standard));
        Assert.Throws<ArgumentException>(() => MlrCalculation.Window([first], new(2026, 9, 1), 2, MlrSelection.Standard));
    }

    private static FinancialSnapshot Row(DateOnly month, decimal revenue, decimal expense, int members = 10) => new()
    {
        EffectiveMonth = month,
        Membership = new() { MemberMonths = members },
        Revenue = [new() { Category = "Capitation", Amount = revenue }],
        Expenses = [new() { Category = "Inpatient", Amount = expense }]
    };
}
