using System.Globalization;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Api;

public sealed class AnalyticsFilter
{
    public string? Month { get; set; }
    public int? Year { get; set; }
    public string? ReportMonth { get; set; }
    public Guid? Payer { get; set; }
    public Guid? Center { get; set; }
    public Guid? Provider { get; set; }
    public Guid? Contract { get; set; }
    public Guid? Plan { get; set; }
    public Guid? LineOfBusiness { get; set; }
    public Guid? Region { get; set; }

    public static AnalyticsFilter Parse(IQueryCollection query)
    {
        var result = new AnalyticsFilter { Month = Text("month"), ReportMonth = Text("reportMonth") };
        string? Text(string name) => query[name].FirstOrDefault() is { Length: > 0 } v ? v : null;
        Guid? Id(string name) => Text(name) is not { } v ? null : Guid.TryParse(v, out var id) ? id : throw new ArgumentException($"Invalid {name} identifier.");
        if (Text("year") is { } year)
            result.Year = int.TryParse(year, out var y) && y is >= 2000 and <= 2100 ? y : throw new ArgumentException("Invalid year.");
        foreach (var value in new[] { result.Month, result.ReportMonth }.Where(v => v is not null))
            if (!DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("Months must use YYYY-MM.");
        if (query.ContainsKey("organizationId")) throw new ArgumentException("Organization scope comes from your authenticated session.");
        result.Payer = Id("payer"); result.Center = Id("center"); result.Provider = Id("provider");
        result.Contract = Id("contract"); result.Plan = Id("plan"); result.LineOfBusiness = Id("lineOfBusiness"); result.Region = Id("region");
        return result;
    }
}

public sealed class AnalyticsService(HhhDbContext db)
{
    public async Task<object> Dashboard(Guid organizationId, AnalyticsFilter filter, CancellationToken ct)
    {
        var all = await Rows(organizationId, filter, ct);
        var dimensions = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == organizationId).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var org = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == organizationId, ct);
        var latest = all.Count == 0 ? (DateOnly?)null : all.Max(x => x.EffectiveMonth);
        var selected = filter.Month is { } month ? DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : filter.Year is { } year ? all.Where(x => x.EffectiveMonth.Year == year).Select(x => (DateOnly?)x.EffectiveMonth).Max() : latest;
        var period = all.Where(x => (filter.Month is null && filter.Year is not null)
            ? x.EffectiveMonth.Year == filter.Year : x.EffectiveMonth == selected).ToList();
        var previous = all.Where(x => x.EffectiveMonth == selected?.AddMonths(-1)).ToList();
        var priorYear = all.Where(x => x.EffectiveMonth == selected?.AddYears(-1)).ToList();
        var totals = FinancialMetrics.Calculate(period);
        var currentMonth = FinancialMetrics.Calculate(all.Where(x => x.EffectiveMonth == selected));
        var comparisons = new
        {
            currentMonth,
            previous = FinancialMetrics.Calculate(previous),
            priorYear = FinancialMetrics.Calculate(priorYear),
            previousAvailable = previous.Count > 0,
            priorYearAvailable = priorYear.Count > 0
        };
        var trends = all.Where(x => selected is null || x.EffectiveMonth <= selected)
            .GroupBy(x => x.EffectiveMonth).OrderBy(x => x.Key).TakeLast(12)
            .Select(g => new { month = g.Key.ToString("yyyy-MM"), metrics = FinancialMetrics.Calculate(g) });
        object[] Group(Func<FinancialSnapshot, Guid> key) => period.GroupBy(key)
            .Select(g => (object)new { id = g.Key, name = dimensions[g.Key], metrics = FinancialMetrics.Calculate(g) }).ToArray();
        var pnl = period.SelectMany(x => x.Revenue).GroupBy(x => x.Category)
            .Select(g => new { category = g.Key, type = "revenue", amount = g.Sum(x => x.Amount) })
            .Concat(period.SelectMany(x => x.Expenses).GroupBy(x => new { x.Category, x.IsAdministrative })
                .Select(g => new { category = g.Key.Category, type = g.Key.IsAdministrative ? "administrative" : "medical", amount = g.Sum(x => x.Amount) })).ToArray();
        return new
        {
            organization = org.Name,
            mlrTarget = org.MlrTarget,
            period = selected?.ToString("yyyy-MM"),
            isSynthetic = true,
            periodMode = filter.Month is null && filter.Year is not null ? "year" : "month",
            totals,
            comparisons,
            trends,
            endingMembership = FinancialMetrics.Calculate(period.Where(x => x.EffectiveMonth == period.Select(r => (DateOnly?)r.EffectiveMonth).Max())).MemberMonths,
            payers = Group(x => x.PayerId),
            centers = Group(x => x.MedicalCenterId),
            providers = Group(x => x.ProviderId),
            pnl,
            snapshotCount = period.Count,
            latestAvailableMonth = latest?.ToString("yyyy-MM")
        };
    }

    public Task<DateOnly?> LatestMonth(Guid organizationId, AnalyticsFilter filter, CancellationToken ct)
        => Query(organizationId, filter).Select(x => (DateOnly?)x.EffectiveMonth).MaxAsync(ct);

    public async Task<List<FinancialSnapshot>> Rows(Guid organizationId, AnalyticsFilter f, CancellationToken ct, DateOnly? from = null, DateOnly? to = null)
    {
        var q = Query(organizationId, f);
        if (from is { } start) q = q.Where(x => x.EffectiveMonth >= start);
        if (to is { } end) q = q.Where(x => x.EffectiveMonth <= end);
        return await q.Include(x => x.Membership).Include(x => x.Revenue).Include(x => x.Expenses).AsSplitQuery().ToListAsync(ct);
    }

    private IQueryable<FinancialSnapshot> Query(Guid organizationId, AnalyticsFilter f)
    {
        var q = db.Snapshots.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (f.Payer is { } payer) q = q.Where(x => x.PayerId == payer);
        if (f.Center is { } center) q = q.Where(x => x.MedicalCenterId == center);
        if (f.Provider is { } provider) q = q.Where(x => x.ProviderId == provider);
        if (f.Contract is { } contract) q = q.Where(x => x.ContractId == contract);
        if (f.Plan is { } plan) q = q.Where(x => x.PlanId == plan);
        if (f.LineOfBusiness is { } lob) q = q.Where(x => x.LineOfBusinessId == lob);
        if (f.Region is { } region) q = q.Where(x => x.RegionId == region);
        if (f.ReportMonth is { } report)
        {
            var date = DateOnly.ParseExact(report + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            q = q.Where(x => x.ReportMonth == date);
        }
        return q;
    }

    public async Task<object> Metadata(Guid org, CancellationToken ct)
    {
        var dimensions = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == org).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, kind = x.Kind.ToString() }).ToListAsync(ct);
        var periods = await db.Snapshots.AsNoTracking().Where(x => x.OrganizationId == org)
            .Select(x => new { x.EffectiveMonth, x.ReportMonth }).Distinct().OrderByDescending(x => x.EffectiveMonth).ToListAsync(ct);
        return new
        {
            dimensions,
            months = periods.Select(x => x.EffectiveMonth.ToString("yyyy-MM")).Distinct(),
            reportMonths = periods.Select(x => x.ReportMonth.ToString("yyyy-MM")).Distinct(),
            years = periods.Select(x => x.EffectiveMonth.Year).Distinct()
        };
    }
}
