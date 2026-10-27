using Hhh.Domain;
using Xunit;

namespace Hhh.Tests;

public sealed class FinancialMetricsTests
{
    [Fact]
    public void Weighted_ratios_use_totals_and_membership_is_counted_once()
    {
        var a = Snapshot(1000, 500, 100, 10);
        var b = Snapshot(9000, 8100, 500, 30);
        var m = FinancialMetrics.Calculate([a, b]);
        Assert.Equal(10000m, m.Revenue);
        Assert.Equal(8600m, m.MedicalExpenses);
        Assert.Equal(800m, m.NetProfit);
        Assert.Equal(86m, m.Mlr);
        Assert.Equal(40, m.MemberMonths);
        Assert.Equal(250m, m.RevenuePmpm);
        Assert.Equal(215m, m.CostPmpm);
        Assert.Equal(14m, m.GrossMargin);
        Assert.Equal(8m, m.NetMargin);
    }

    [Fact]
    public void Recoveries_reduce_medical_cost_and_administration_is_excluded_from_mlr()
    {
        var row = Snapshot(1000, 800, 100, 10);
        row.Expenses.Add(new() { Category = "Stop-loss recoveries", Amount = -50 });
        row.Expenses.Add(new() { Category = "IBNR", Amount = 30 });
        var m = FinancialMetrics.Calculate([row]);
        Assert.Equal(780m, m.MedicalExpenses);
        Assert.Equal(78m, m.Mlr);
        Assert.Equal(120m, m.NetProfit);
        Assert.Equal(1000m, m.CapitationRevenue);
    }

    [Fact]
    public void Empty_data_has_no_fabricated_ratios()
    {
        var m = FinancialMetrics.Calculate([]);
        Assert.Equal(0, m.Revenue);
        Assert.Null(m.Mlr); Assert.Null(m.RevenuePmpm); Assert.Null(m.GrossMargin);
        Assert.Null(FinancialMetrics.Change(10, 0));
    }

    [Fact]
    public void Change_uses_absolute_previous_value_for_losses()
    {
        Assert.Equal(50m, FinancialMetrics.Change(-50, -100));
        Assert.Equal(-50m, FinancialMetrics.Change(50, 100));
    }

    private static FinancialSnapshot Snapshot(decimal revenue, decimal medical, decimal admin, int members) => new()
    {
        Membership = new() { MemberMonths = members },
        Revenue = [new() { Category = "Capitation", Amount = revenue }],
        Expenses = [new() { Category = "Medical", Amount = medical }, new() { Category = "Administrative", Amount = admin, IsAdministrative = true }]
    };
}
