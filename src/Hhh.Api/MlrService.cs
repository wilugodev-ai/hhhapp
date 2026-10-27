using System.Globalization;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Api;

public sealed record MlrOptions(int Window, string GroupBy)
{
    public static MlrOptions Parse(IQueryCollection query)
    {
        var raw = query["window"].FirstOrDefault();
        var window = string.IsNullOrEmpty(raw) ? 1 : int.TryParse(raw, out var value) ? value : 0;
        if (window is not (1 or 3 or 6 or 12)) throw new ArgumentException("MLR window must be 1, 3, 6, or 12 months.");
        var group = query["groupBy"].FirstOrDefault() ?? "payer";
        if (!ProfitLossOptions.Groups.Where(x => x != "month").Contains(group, StringComparer.Ordinal)) throw new ArgumentException("Invalid MLR grouping dimension.");
        return new(window, group);
    }
}
public sealed record MlrGroup(Guid Id, string Name, decimal Target, string TargetSource, MlrWindow Values, MlrWindow Previous,
    decimal? ChangePercentagePoints, decimal? GapPercentagePoints, string Status);
public sealed record MlrTrend(string Month, decimal? Monthly, decimal? Rolling3, decimal? Rolling6, decimal? Rolling12);
public sealed record MlrReport(string Organization, string EndMonth, int Window, string GroupBy, string DefinitionName, int DefinitionVersion,
    string[] RevenueCategories, string[] ExpenseCategories, decimal OrganizationTarget, MlrWindow[] Windows, MlrWindow Current,
    MlrWindow Previous, MlrAttribution Attribution, MlrGroup[] Groups, MlrGroup[] Alerts, MlrTrend[] Trends);

public sealed class MlrService(HhhDbContext db, AnalyticsService analytics, MlrConfigurationService configuration)
{
    public async Task<MlrReport> Report(Guid org, AnalyticsFilter filter, MlrOptions options, CancellationToken ct)
    {
        var latest = await analytics.LatestMonth(org, filter, ct,
            filter.Month is null && filter.Year is { } year ? new DateOnly(year, 1, 1) : null,
            filter.Month is null && filter.Year is { } endYear ? new DateOnly(endYear, 12, 1) : null);
        var end = filter.Month is { } month ? DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : latest ?? (filter.Year is { } fallbackYear ? new DateOnly(fallbackYear, 12, 1) : DateOnly.FromDateTime(DateTime.UtcNow));
        end = new(end.Year, end.Month, 1);
        var rows = await analytics.Rows(org, filter, ct, end.AddMonths(-23), end);
        if (rows.GroupBy(CohortKey.From).Any(x => x.Count() != 1)) throw new ArgumentException("Multiple reporting versions match a cohort. Select a report month to avoid double counting.");
        var selection = await configuration.Selection(org, ct);
        var rules = await configuration.Targets(org, ct);
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == org, ct);
        var names = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var windows = new[] { 1, 3, 6, 12 }.Select(n => MlrCalculation.Window(rows, end, n, selection)).ToArray();
        var current = windows.Single(x => x.Months == options.Window);
        var previous = MlrCalculation.Window(rows, end.AddMonths(-options.Window), options.Window, selection);
        var currentRows = rows.Where(x => x.EffectiveMonth >= end.AddMonths(1 - options.Window)).ToList();
        var priorRows = rows.Where(x => x.EffectiveMonth >= end.AddMonths(1 - options.Window * 2) && x.EffectiveMonth <= end.AddMonths(-options.Window)).ToList();
        Guid Key(FinancialSnapshot row) => options.GroupBy switch
        {
            "center" => row.MedicalCenterId,
            "provider" => row.ProviderId,
            "contract" => row.ContractId,
            "plan" => row.PlanId,
            "lineOfBusiness" => row.LineOfBusinessId,
            "region" => row.RegionId,
            _ => row.PayerId
        };
        var ids = rows.Where(x => x.EffectiveMonth >= end.AddMonths(-11)).Select(Key).Distinct().ToArray();
        var groups = ids.Select(id =>
        {
            var source = rows.Where(x => Key(x) == id).ToList();
            var values = MlrCalculation.Window(source, end, options.Window, selection);
            var prior = MlrCalculation.Window(source, end.AddMonths(-options.Window), options.Window, selection);
            var hasOverride = rules.TryGetValue(id, out var rule);
            var target = hasOverride ? rule!.Target : organization.MlrTarget;
            return new MlrGroup(id, names[id], target, hasOverride ? "Group override" : "Organization default", values, prior,
                values.Mlr is { } value && prior.Mlr is { } priorValue ? decimal.Round(value - priorValue, 2) : null,
                values.Mlr is { } actual ? decimal.Round(actual - target, 2) : null,
                values.Mlr is null ? "unavailable" : values.Mlr > target ? "above-target" : "within-target");
        }).OrderByDescending(x => x.Values.Mlr).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray();
        var trends = Enumerable.Range(0, 12).Select(i => end.AddMonths(i - 11)).Select(m => new MlrTrend(Month(m),
            MlrCalculation.Window(rows, m, 1, selection).Mlr, MlrCalculation.Window(rows, m, 3, selection).Mlr,
            MlrCalculation.Window(rows, m, 6, selection).Mlr, MlrCalculation.Window(rows, m, 12, selection).Mlr)).ToArray();
        return new(organization.Name, Month(end), options.Window, options.GroupBy, selection.Name, selection.Version,
            selection.RevenueCategories, selection.ExpenseCategories, organization.MlrTarget, windows, current, previous,
            MlrCalculation.Attribute(currentRows, priorRows, current, previous, selection), groups,
            groups.Where(x => x.Status == "above-target").ToArray(), trends);
    }
    private static string Month(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
