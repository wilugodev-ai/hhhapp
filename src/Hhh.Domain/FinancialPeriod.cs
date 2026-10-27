namespace Hhh.Domain;

public sealed record FinancialPeriod(string Mode, DateOnly Start, DateOnly End, IReadOnlyList<DateOnly> Months)
{
    public int PreviousPeriodOffset => Mode switch { "quarter" => -3, "year" => -12, _ => -1 };
    public static FinancialPeriod Create(string mode, DateOnly anchor, int? year = null, int? quarter = null)
    {
        if (mode is not ("month" or "quarter" or "year")) throw new ArgumentException("Period must be month, quarter, or year.");
        var selectedYear = year ?? anchor.Year;
        if (selectedYear is < 2000 or > 2100) throw new ArgumentException("Year must be between 2000 and 2100.");
        var q = quarter ?? (anchor.Month - 1) / 3 + 1;
        if (q is < 1 or > 4) throw new ArgumentException("Quarter must be between 1 and 4.");
        var start = mode switch { "year" => new DateOnly(selectedYear, 1, 1), "quarter" => new DateOnly(selectedYear, (q - 1) * 3 + 1, 1), _ => new DateOnly(anchor.Year, anchor.Month, 1) };
        var count = mode switch { "year" => 12, "quarter" => 3, _ => 1 };
        var months = Enumerable.Range(0, count).Select(start.AddMonths).ToArray();
        return new(mode, start, months[^1], months);
    }
}
