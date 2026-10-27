namespace Hhh.Domain;

public sealed record MlrSelection(string Name, int Version, string[] RevenueCategories, string[] ExpenseCategories)
{
    public static MlrSelection Standard { get; } = new("Standard operational MLR", 0, [], []);
    public bool IsImplicit => Version == 0;
}

public sealed record MlrAmounts(decimal Revenue, decimal Expenses, int MemberMonths, bool ComponentsComplete)
{
    public decimal? Mlr => ComponentsComplete && Revenue > 0 ? decimal.Round(Expenses / Revenue * 100, 2) : null;
    public decimal? CostPmpm => ComponentsComplete && MemberMonths > 0 ? decimal.Round(Expenses / MemberMonths, 2) : null;
}

public sealed record MlrWindow(int Months, string StartMonth, string EndMonth, string[] AvailableMonths, string[] MissingMonths,
    bool ComponentsComplete, int SnapshotCount, decimal Revenue, decimal Expenses, int MemberMonths, decimal? Mlr, decimal? CostPmpm)
{
    public bool Complete => MissingMonths.Length == 0 && ComponentsComplete && SnapshotCount > 0;
}

public sealed record MlrDriver(string Category, string Type, decimal PreviousAmount, decimal CurrentAmount, decimal AmountChange, decimal ContributionPercentagePoints);
public sealed record MlrAttribution(bool Available, decimal? ChangePercentagePoints, decimal? RoundingPercentagePoints, MlrDriver[] Drivers);

public static class MlrCalculation
{
    public static MlrAmounts Amounts(IEnumerable<FinancialSnapshot> source, MlrSelection selection)
    {
        var rows = source.ToList();
        var revenue = rows.SelectMany(x => x.Revenue).Where(x => selection.IsImplicit || selection.RevenueCategories.Contains(x.Category, StringComparer.Ordinal)).Sum(x => x.Amount);
        var expenses = rows.SelectMany(x => x.Expenses).Where(x => !x.IsAdministrative && (selection.IsImplicit || selection.ExpenseCategories.Contains(x.Category, StringComparer.Ordinal))).Sum(x => x.Amount);
        var complete = rows.Count > 0 && (selection.IsImplicit || rows.All(row =>
            selection.RevenueCategories.All(category => row.Revenue.Any(x => x.Category == category))
            && selection.ExpenseCategories.All(category => row.Expenses.Any(x => !x.IsAdministrative && x.Category == category))));
        return new(revenue, expenses, rows.Sum(x => x.Membership.MemberMonths), complete);
    }

    public static MlrWindow Window(IEnumerable<FinancialSnapshot> source, DateOnly end, int months, MlrSelection selection)
    {
        if (months is not (1 or 3 or 6 or 12)) throw new ArgumentException("MLR windows must be 1, 3, 6, or 12 months.");
        end = new(end.Year, end.Month, 1);
        var start = end.AddMonths(1 - months);
        var rows = source.Where(x => x.EffectiveMonth >= start && x.EffectiveMonth <= end).ToList();
        if (rows.GroupBy(CohortKey.From).Any(x => x.Count() != 1)) throw new ArgumentException("Multiple reporting versions match a cohort. Select a report month to avoid double counting.");
        var present = rows.Select(x => x.EffectiveMonth).Distinct().Order().ToArray();
        var missing = Enumerable.Range(0, months).Select(start.AddMonths).Except(present).ToArray();
        var amounts = Amounts(rows, selection);
        return new(months, Month(start), Month(end), present.Select(Month).ToArray(), missing.Select(Month).ToArray(), amounts.ComponentsComplete,
            rows.Count, amounts.Revenue, amounts.Expenses, amounts.MemberMonths,
            missing.Length == 0 ? amounts.Mlr : null, missing.Length == 0 ? amounts.CostPmpm : null);
    }

    public static MlrAttribution Attribute(IEnumerable<FinancialSnapshot> current, IEnumerable<FinancialSnapshot> previous,
        MlrWindow currentWindow, MlrWindow previousWindow, MlrSelection selection)
    {
        if (!currentWindow.Complete || !previousWindow.Complete || currentWindow.Revenue <= 0 || previousWindow.Revenue <= 0) return new(false, null, null, []);
        var currentRows = current.ToList(); var priorRows = previous.ToList();
        var expenses = currentRows.Concat(priorRows).SelectMany(x => x.Expenses).Where(x => !x.IsAdministrative
            && (selection.IsImplicit || selection.ExpenseCategories.Contains(x.Category, StringComparer.Ordinal))).Select(x => x.Category).Distinct(StringComparer.Ordinal);
        var revenues = currentRows.Concat(priorRows).SelectMany(x => x.Revenue).Where(x => selection.IsImplicit || selection.RevenueCategories.Contains(x.Category, StringComparer.Ordinal))
            .Select(x => x.Category).Distinct(StringComparer.Ordinal);
        var drivers = new List<MlrDriver>();
        foreach (var category in expenses)
        {
            var a = currentRows.SelectMany(x => x.Expenses).Where(x => !x.IsAdministrative && x.Category == category).Sum(x => x.Amount);
            var b = priorRows.SelectMany(x => x.Expenses).Where(x => !x.IsAdministrative && x.Category == category).Sum(x => x.Amount);
            drivers.Add(new(category, "medical", b, a, a - b, decimal.Round((a - b) / previousWindow.Revenue * 100, 4)));
        }
        foreach (var category in revenues)
        {
            var a = currentRows.SelectMany(x => x.Revenue).Where(x => x.Category == category).Sum(x => x.Amount);
            var b = priorRows.SelectMany(x => x.Revenue).Where(x => x.Category == category).Sum(x => x.Amount);
            drivers.Add(new(category, "revenue", b, a, a - b, decimal.Round(-currentWindow.Expenses * (a - b) / currentWindow.Revenue / previousWindow.Revenue * 100, 4)));
        }
        var change = decimal.Round((currentWindow.Expenses / currentWindow.Revenue - previousWindow.Expenses / previousWindow.Revenue) * 100, 4);
        return new(true, change, decimal.Round(change - drivers.Sum(x => x.ContributionPercentagePoints), 4),
            drivers.OrderByDescending(x => Math.Abs(x.ContributionPercentagePoints)).ThenBy(x => x.Category, StringComparer.Ordinal).ToArray());
    }

    private static string Month(DateOnly month) => month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
