using System.Globalization;
using Hhh.Domain;

namespace Hhh.Api;

public sealed record ProfitLossOptions(string Period, int? Quarter, Guid? BudgetPlan, string GroupBy, string? Category, string? Type, int Page, int PageSize)
{
    public static readonly string[] Groups = ["payer", "center", "provider", "contract", "plan", "lineOfBusiness", "region", "month"];
    public static ProfitLossOptions Parse(IQueryCollection query)
    {
        string? Text(string key) => query[key].FirstOrDefault() is { Length: > 0 } value ? value : null;
        int Integer(string key, int fallback, int min, int max) => Text(key) is not { } raw ? fallback
            : int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max ? n
            : throw new ArgumentException($"Invalid {key}.");
        var period = Text("period") ?? "month";
        if (period is not ("month" or "quarter" or "year")) throw new ArgumentException("Period must be month, quarter, or year.");
        var groupBy = Text("groupBy") ?? "payer";
        if (!Groups.Contains(groupBy, StringComparer.Ordinal)) throw new ArgumentException("Invalid grouping dimension.");
        Guid? plan = Text("budgetPlan") is not { } rawPlan ? null : Guid.TryParse(rawPlan, out var id) ? id : throw new ArgumentException("Invalid budget plan.");
        var type = Text("type");
        if (type is not (null or "revenue" or "medical" or "administrative")) throw new ArgumentException("Invalid financial component type.");
        var category = Text("category");
        if (category is { Length: > 80 }) throw new ArgumentException("Invalid financial component.");
        return new(period, Text("quarter") is null ? null : Integer("quarter", 1, 1, 4), plan, groupBy, category, type,
            Integer("page", 1, 1, 10000), Integer("pageSize", 25, 1, 100));
    }
}

public sealed record ProfitLossLine(string Category, string Type, decimal? Actual, decimal? Budget, decimal? PriorYear,
    decimal? PreviousPeriod, FinancialVariance BudgetVariance, decimal? PriorYearChangePercent, decimal? PreviousPeriodChangePercent);
public sealed record BudgetPlanInfo(Guid Id, string Name, int Year, string Version, DateTimeOffset ApprovedAt, bool IsSynthetic);
public sealed record ProfitLossCoverage(string[] RequestedMonths, string[] ActualMonths, string[] MissingActualMonths,
    string[] MissingBudgetMonths, bool BudgetMatchesCohorts, bool BudgetAvailable, bool PriorYearAvailable, bool PreviousPeriodAvailable,
    int ActualSnapshots, int BudgetSnapshots, bool IsPartial);
public sealed record ProfitLossTrend(string Month, decimal? Actual, decimal? Budget);
public sealed record ProfitLossReport(string Organization, string Period, string StartMonth, string EndMonth,
    string? ComparedThroughMonth, BudgetPlanInfo? BudgetPlan, BudgetPlanInfo[] AvailablePlans, ProfitLossCoverage Coverage,
    ProfitLossLine[] Lines, ProfitLossLine[] Totals, ProfitLossTrend[] Trends);
public sealed record ProfitLossGroup(string Id, string Name, ProfitLossLine Values);
public sealed record ProfitLossDetailRow(string Month, long? SnapshotId, string? ReportMonth, string? GeneralLedgerMonth,
    Guid? SourceBatchId, string? SourceName, Dictionary<string, string> Dimensions, ProfitLossLine Values);
public sealed record ProfitLossDetails(string Category, string Type, string GroupBy, ProfitLossLine Total,
    ProfitLossGroup[] Groups, ProfitLossDetailRow[] Rows, int Page, int PageSize, int TotalRows);
