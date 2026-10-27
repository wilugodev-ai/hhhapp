namespace Hhh.Domain;

public sealed record FinancialMetrics(
    decimal Revenue, decimal MedicalExpenses, decimal AdministrativeExpenses,
    decimal NetProfit, decimal CapitationRevenue, int MemberMonths,
    decimal? GrossMargin, decimal? NetMargin, decimal? Mlr,
    decimal? RevenuePmpm, decimal? CostPmpm, decimal MlrRevenue, decimal MlrExpenses)
{
    public static FinancialMetrics Calculate(IEnumerable<FinancialSnapshot> source, MlrSelection? mlrSelection = null)
    {
        var rows = source.ToList();
        var revenue = rows.Sum(x => x.Revenue.Sum(r => r.Amount));
        var medical = rows.Sum(x => x.Expenses.Where(e => !e.IsAdministrative).Sum(e => e.Amount));
        var admin = rows.Sum(x => x.Expenses.Where(e => e.IsAdministrative).Sum(e => e.Amount));
        var members = rows.Sum(x => x.Membership.MemberMonths);
        var mlr = MlrCalculation.Amounts(rows, mlrSelection ?? MlrSelection.Standard);
        return new(revenue, medical, admin, revenue - medical - admin,
            rows.Sum(x => x.Revenue.Where(r => r.Category == "Capitation").Sum(r => r.Amount)), members,
            Ratio(revenue - medical, revenue, 100), Ratio(revenue - medical - admin, revenue, 100),
            mlr.Mlr, Ratio(revenue, members), Ratio(medical, members), mlr.Revenue, mlr.Expenses);
    }

    public static decimal? Ratio(decimal numerator, decimal denominator, decimal scale = 1)
        => denominator == 0 ? null : decimal.Round(numerator / denominator * scale, 2);

    public static decimal? Change(decimal current, decimal previous)
        => previous == 0 ? null : decimal.Round((current - previous) / Math.Abs(previous) * 100, 2);
}
