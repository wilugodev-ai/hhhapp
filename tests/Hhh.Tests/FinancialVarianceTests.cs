using Hhh.Domain;
using Xunit;

namespace Hhh.Tests;

public sealed class FinancialVarianceTests
{
    [Theory]
    [InlineData(120, 100, false, "favorable", 20, 20)]
    [InlineData(120, 100, true, "unfavorable", 20, 20)]
    [InlineData(80, 100, true, "favorable", -20, -20)]
    [InlineData(-300, -200, true, "favorable", -100, -50)]
    [InlineData(-50, -100, false, "favorable", 50, 50)]
    [InlineData(100, 100, false, "on-budget", 0, 0)]
    public void Variance_direction_respects_revenue_expenses_and_recoveries(decimal actual, decimal budget, bool expense, string status, decimal amount, decimal percent)
    {
        var result = FinancialVariance.Calculate(actual, budget, expense);
        Assert.Equal(status, result.Status); Assert.Equal(amount, result.Amount); Assert.Equal(percent, result.Percent);
    }

    [Fact]
    public void Missing_comparisons_are_not_zero_and_zero_budget_has_no_percentage()
    {
        Assert.Equal("unavailable", FinancialVariance.Calculate(10, null, true).Status);
        Assert.Null(FinancialVariance.Calculate(null, 10, true).Amount);
        var zero = FinancialVariance.Calculate(10, 0, true);
        Assert.Equal(10, zero.Amount); Assert.Null(zero.Percent); Assert.Equal("unfavorable", zero.Status);
    }

    [Theory]
    [InlineData("month", 2026, 9, 9, 1, -1)]
    [InlineData("quarter", 2026, 7, 9, 3, -3)]
    [InlineData("year", 2026, 1, 12, 12, -12)]
    public void Financial_periods_have_consistent_month_boundaries(string mode, int year, int startMonth, int endMonth, int count, int offset)
    {
        var period = FinancialPeriod.Create(mode, new(year, 9, 1));
        Assert.Equal(new DateOnly(year, startMonth, 1), period.Start);
        Assert.Equal(new DateOnly(year, endMonth, 1), period.End);
        Assert.Equal(count, period.Months.Count); Assert.Equal(offset, period.PreviousPeriodOffset);
    }

    [Fact]
    public void Explicit_quarters_do_not_depend_on_the_latest_actual_month()
    {
        var q = FinancialPeriod.Create("quarter", new(2026, 9, 1), 2025, 1);
        Assert.Equal(new DateOnly(2025, 1, 1), q.Start);
        Assert.Equal(new DateOnly(2025, 3, 1), q.End);
        Assert.Throws<ArgumentException>(() => FinancialPeriod.Create("quarter", new(2026, 9, 1), 2026, 5));
        Assert.Throws<ArgumentException>(() => FinancialPeriod.Create("invalid", new(2026, 9, 1)));
    }
}
