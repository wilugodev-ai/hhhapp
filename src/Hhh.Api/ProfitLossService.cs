using System.Globalization;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Api;

public sealed class ProfitLossService(HhhDbContext db, AnalyticsService analytics)
{
    public async Task<ProfitLossReport> Report(Guid org, AnalyticsFilter filter, ProfitLossOptions options, CancellationToken ct)
        => (await Build(org, filter, options, ct)).Report;

    public async Task<ProfitLossDetails> Details(Guid org, AnalyticsFilter filter, ProfitLossOptions options, CancellationToken ct)
    {
        var context = await Build(org, filter, options, ct);
        var line = context.Report.Lines.SingleOrDefault(x => x.Category == options.Category && x.Type == options.Type)
            ?? throw new ArgumentException("Choose an available financial component and type.");
        var actual = context.Actual.ToDictionary(CohortKey.From);
        var budget = context.Budget.ToDictionary(CohortKey.From);
        var prior = context.Report.Coverage.PriorYearAvailable ? context.Prior.ToDictionary(x => CohortKey.From(x).Shift(12)) : [];
        var previous = context.Report.Coverage.PreviousPeriodAvailable ? context.Previous.ToDictionary(x => CohortKey.From(x).Shift(-context.Period.PreviousPeriodOffset)) : [];
        var keys = actual.Count > 0 ? actual.Keys.ToList() : budget.Keys.ToList();
        var sourceNames = await db.SourceBatches.AsNoTracking().Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var rows = keys.OrderBy(x => x.Month).ThenBy(x => context.Names[x.Payer]).ThenBy(x => context.Names[x.Center]).ThenBy(x => x.Provider).Select(key =>
        {
            actual.TryGetValue(key, out var a); budget.TryGetValue(key, out var b);
            prior.TryGetValue(key, out var y); previous.TryGetValue(key, out var p);
            return new ProfitLossDetailRow(Month(key.Month), a?.Id, a is null ? null : Month(a.ReportMonth), a is null ? null : Month(a.GeneralLedgerMonth),
                a?.SourceBatchId, a is null ? null : sourceNames[a.SourceBatchId], Names(key, context.Names),
                Line(line.Category, line.Type, a is null ? null : Amount(a, line.Category, line.Type),
                    context.Report.Coverage.BudgetAvailable && b is not null ? BudgetAmount(b, line.Category, line.Type) : null,
                    context.Report.Coverage.PriorYearAvailable && y is not null ? Amount(y, line.Category, line.Type) : null,
                    context.Report.Coverage.PreviousPeriodAvailable && p is not null ? Amount(p, line.Category, line.Type) : null));
        }).ToArray();
        var groups = rows.GroupBy(x => options.GroupBy == "month" ? x.Month : x.Dimensions[options.GroupBy])
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(g => new ProfitLossGroup(g.Key, g.Key,
                Line(line.Category, line.Type, Sum(g.Select(x => x.Values.Actual)), Sum(g.Select(x => x.Values.Budget)),
                    Sum(g.Select(x => x.Values.PriorYear)), Sum(g.Select(x => x.Values.PreviousPeriod))))).ToArray();
        return new(line.Category, line.Type, options.GroupBy, line, groups,
            rows.Skip((options.Page - 1) * options.PageSize).Take(options.PageSize).ToArray(), options.Page, options.PageSize, rows.Length);
    }

    private async Task<ReportContext> Build(Guid org, AnalyticsFilter filter, ProfitLossOptions options, CancellationToken ct)
    {
        var latest = await analytics.LatestMonth(org, filter, ct) ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var anchor = filter.Month is { } month ? DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture) : latest;
        var period = FinancialPeriod.Create(options.Period, anchor, filter.Year, options.Quarter);
        var all = await analytics.Rows(org, filter, ct, period.Start.AddYears(-1), period.End);
        if (options.Period == "month" && filter.Year is { } year && year != anchor.Year)
            throw new ArgumentException("The month and year must refer to the same year.");
        var actual = all.Where(x => period.Months.Contains(x.EffectiveMonth)).ToList();
        if (actual.GroupBy(CohortKey.From).Any(x => x.Count() != 1))
            throw new ArgumentException("Multiple report versions match this period. Select a report month to avoid double counting.");
        var actualKeys = actual.Select(CohortKey.From).ToHashSet();
        var coveredMonths = actual.Select(x => x.EffectiveMonth).Distinct().Order().ToArray();
        var comparedMonths = actual.Count > 0 ? coveredMonths : period.Months.ToArray();
        var plans = await db.BudgetPlans.AsNoTracking().Where(x => x.OrganizationId == org).OrderByDescending(x => x.ApprovedAt).ThenBy(x => x.Id)
            .Select(x => new BudgetPlanInfo(x.Id, x.Name, x.Year, x.Version, x.ApprovedAt, x.IsSynthetic)).ToArrayAsync(ct);
        var plan = options.BudgetPlan is { } planId ? plans.SingleOrDefault(x => x.Id == planId && x.Year == period.Start.Year)
            ?? throw new ArgumentException("This budget plan is not available for the selected year and organization.")
            : plans.FirstOrDefault(x => x.Year == period.Start.Year);
        var budgetAll = await Budgets(org, plan, filter, period, ct);
        var budget = budgetAll.Where(x => comparedMonths.Contains(x.Month)).ToList();
        var budgetKeys = budget.Select(CohortKey.From).ToHashSet();
        var budgetMonths = budget.Select(x => x.Month).ToHashSet();
        var budgetMatches = actual.Count > 0 && actualKeys.SetEquals(budgetKeys);
        // Complete component coverage is also required; a missing plan category is not a zero budget.
        var actualComponents = actual.SelectMany(x => Components(x).Select(c => (Key: CohortKey.From(x), c.Category, c.Type))).ToHashSet();
        var budgetComponents = budget.SelectMany(x => x.Lines.Select(c => (Key: CohortKey.From(x), c.Category, c.Type))).ToHashSet();
        var budgetAvailable = plan is not null && budget.Count > 0 && (actual.Count > 0
            ? budgetMatches && actualComponents.SetEquals(budgetComponents)
            : comparedMonths.All(budgetMonths.Contains));
        List<FinancialSnapshot> Comparison(int offset) => all.Where(x => actualKeys.Contains(CohortKey.From(x).Shift(-offset))).ToList();
        var prior = Comparison(-12);
        var previous = Comparison(period.PreviousPeriodOffset);
        bool Complete(List<FinancialSnapshot> rows, int offset) => actual.Count > 0
            && rows.Count == actual.Count && actualKeys.SetEquals(rows.Select(x => CohortKey.From(x).Shift(-offset)))
            && actualComponents.SetEquals(rows.SelectMany(x => Components(x).Select(c => (Key: CohortKey.From(x).Shift(-offset), c.Category, c.Type))));
        var priorAvailable = Complete(prior, -12);
        var previousAvailable = Complete(previous, period.PreviousPeriodOffset);
        var categories = actual.SelectMany(Components).Select(x => (x.Category, x.Type))
            .Concat(budget.SelectMany(x => x.Lines).Select(x => (x.Category, x.Type))).Distinct()
            .OrderBy(x => x.Type == "revenue" ? 0 : x.Type == "medical" ? 1 : 2).ThenBy(x => x.Category, StringComparer.Ordinal).ToArray();
        var lines = categories.Select(c => Line(c.Category, c.Type,
            actual.Count > 0 ? actual.Sum(x => Amount(x, c.Category, c.Type)) : null,
            budgetAvailable ? budget.Sum(x => BudgetAmount(x, c.Category, c.Type)) : null,
            priorAvailable ? prior.Sum(x => Amount(x, c.Category, c.Type)) : null,
            previousAvailable ? previous.Sum(x => Amount(x, c.Category, c.Type)) : null)).ToArray();
        ProfitLossLine Total(string name, string type) => Line(name, type,
            actual.Count > 0 ? TotalActual(actual, type) : null, budgetAvailable ? TotalBudget(budget, type) : null,
            priorAvailable ? TotalActual(prior, type) : null, previousAvailable ? TotalActual(previous, type) : null);
        var totals = new[] { Total("Total revenue", "revenue"), Total("Total medical expenses", "medical"),
            Total("Administrative expenses", "administrative"), Total("Net profit / loss", "profit") };
        var names = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var organization = await db.Organizations.AsNoTracking().Where(x => x.Id == org).Select(x => x.Name).SingleAsync(ct);
        var coverage = new ProfitLossCoverage(period.Months.Select(Month).ToArray(), coveredMonths.Select(Month).ToArray(),
            period.Months.Except(coveredMonths).Select(Month).ToArray(), comparedMonths.Where(x => !budgetMonths.Contains(x)).Select(Month).ToArray(),
            budgetMatches, budgetAvailable, priorAvailable, previousAvailable, actual.Count, budget.Count, coveredMonths.Length < period.Months.Count);
        var trends = period.Months.Select(m =>
        {
            var a = actual.Where(x => x.EffectiveMonth == m).ToList(); var b = budgetAll.Where(x => x.Month == m).ToList();
            var matched = a.Count == 0 || (a.Select(CohortKey.From).ToHashSet().SetEquals(b.Select(CohortKey.From))
                && a.SelectMany(x => Components(x).Select(c => (Key: CohortKey.From(x), c.Category, c.Type))).ToHashSet()
                    .SetEquals(b.SelectMany(x => x.Lines.Select(c => (Key: CohortKey.From(x), c.Category, c.Type)))));
            return new ProfitLossTrend(Month(m), a.Count == 0 ? null : TotalActual(a, "profit"), b.Count == 0 || !matched ? null : TotalBudget(b, "profit"));
        }).ToArray();
        var report = new ProfitLossReport(organization, period.Mode, Month(period.Start), Month(period.End), coveredMonths.Length > 0 ? Month(coveredMonths[^1]) : null,
            plan, plans, coverage, lines, totals, trends);
        return new(report, period, actual, budget, prior, previous, names);
    }

    private async Task<List<BudgetSnapshot>> Budgets(Guid org, BudgetPlanInfo? plan, AnalyticsFilter f, FinancialPeriod period, CancellationToken ct)
    {
        if (plan is null) return [];
        var q = db.BudgetSnapshots.AsNoTracking().Where(x => x.OrganizationId == org && x.BudgetPlanId == plan.Id && x.Month >= period.Start && x.Month <= period.End);
        if (f.Payer is { } payer) q = q.Where(x => x.PayerId == payer);
        if (f.Center is { } center) q = q.Where(x => x.MedicalCenterId == center);
        if (f.Provider is { } provider) q = q.Where(x => x.ProviderId == provider);
        if (f.Contract is { } contract) q = q.Where(x => x.ContractId == contract);
        if (f.Plan is { } productPlan) q = q.Where(x => x.PlanId == productPlan);
        if (f.LineOfBusiness is { } lob) q = q.Where(x => x.LineOfBusinessId == lob);
        if (f.Region is { } region) q = q.Where(x => x.RegionId == region);
        return await q.Include(x => x.Lines).AsSplitQuery().ToListAsync(ct);
    }

    public static ProfitLossLine Line(string category, string type, decimal? actual, decimal? budget, decimal? prior, decimal? previous)
        => new(category, type, actual, budget, prior, previous, FinancialVariance.Calculate(actual, budget, type is "medical" or "administrative"),
            actual is not null && prior is not null ? FinancialMetrics.Change(actual.Value, prior.Value) : null,
            actual is not null && previous is not null ? FinancialMetrics.Change(actual.Value, previous.Value) : null);
    private static IEnumerable<(string Category, string Type, decimal Amount)> Components(FinancialSnapshot row)
        => row.Revenue.Select(x => (x.Category, "revenue", x.Amount)).Concat(row.Expenses.Select(x => (x.Category, x.IsAdministrative ? "administrative" : "medical", x.Amount)));
    private static decimal Amount(FinancialSnapshot row, string category, string type) => Components(row).Where(x => x.Category == category && x.Type == type).Sum(x => x.Amount);
    private static decimal BudgetAmount(BudgetSnapshot row, string category, string type) => row.Lines.Where(x => x.Category == category && x.Type == type).Sum(x => x.Amount);
    private static decimal TotalActual(IEnumerable<FinancialSnapshot> rows, string type) => type == "profit" ? rows.Sum(x => x.Revenue.Sum(r => r.Amount) - x.Expenses.Sum(e => e.Amount))
        : rows.SelectMany(Components).Where(x => x.Type == type).Sum(x => x.Amount);
    private static decimal TotalBudget(IEnumerable<BudgetSnapshot> rows, string type) => rows.SelectMany(x => x.Lines).Sum(x => type == "profit" ? x.Type == "revenue" ? x.Amount : -x.Amount : x.Type == type ? x.Amount : 0);
    private static decimal? Sum(IEnumerable<decimal?> source) { var values = source.ToArray(); return values.Length == 0 || values.Any(x => x is null) ? null : values.Sum(x => x!.Value); }
    private static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static Dictionary<string, string> Names(CohortKey key, Dictionary<Guid, string> names) => new()
    {
        ["payer"] = names[key.Payer],
        ["center"] = names[key.Center],
        ["provider"] = names[key.Provider],
        ["contract"] = names[key.Contract],
        ["plan"] = names[key.Plan],
        ["lineOfBusiness"] = names[key.LineOfBusiness],
        ["region"] = names[key.Region]
    };
    private sealed record ReportContext(ProfitLossReport Report, FinancialPeriod Period, List<FinancialSnapshot> Actual, List<BudgetSnapshot> Budget,
        List<FinancialSnapshot> Prior, List<FinancialSnapshot> Previous, Dictionary<Guid, string> Names);
}
