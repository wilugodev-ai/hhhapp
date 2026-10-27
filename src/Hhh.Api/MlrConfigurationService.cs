using System.Text.Json;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Api;

public sealed record MlrTargetInfo(Guid DimensionId, string Kind, string Name, decimal Target, int Version, DateTimeOffset UpdatedAt);
public sealed record MlrConfigurationInfo(string Name, int Version, DateTimeOffset? UpdatedAt, string[] RevenueCategories,
    string[] ExpenseCategories, string[] RevenueCatalog, string[] ExpenseCatalog, decimal OrganizationTarget, MlrTargetInfo[] Targets);
public sealed record MlrDefinitionRequest(string? Name, int ExpectedVersion, string[]? RevenueCategories, string[]? ExpenseCategories);
public sealed record MlrTargetRequest(decimal Target, int ExpectedVersion);
public sealed class MlrConfigurationConflict() : Exception("MLR settings changed. Reload the configuration before saving again.");

public sealed class MlrConfigurationService(HhhDbContext db)
{
    public async Task<MlrSelection> Selection(Guid org, CancellationToken ct)
    {
        var definition = await db.MlrDefinitions.AsNoTracking().Include(x => x.Components).SingleOrDefaultAsync(x => x.OrganizationId == org, ct);
        return definition is null ? MlrSelection.Standard : new(definition.Name, definition.Version,
            definition.Components.Where(x => x.Type == "revenue").Select(x => x.Category).Order(StringComparer.Ordinal).ToArray(),
            definition.Components.Where(x => x.Type == "medical").Select(x => x.Category).Order(StringComparer.Ordinal).ToArray());
    }

    public Task<Dictionary<Guid, MlrTargetRule>> Targets(Guid org, CancellationToken ct)
        => db.MlrTargets.AsNoTracking().Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.DimensionId, ct);

    public async Task<MlrConfigurationInfo> Read(Guid org, CancellationToken ct)
    {
        var selection = await Selection(org, ct);
        var revenue = await db.Revenue.AsNoTracking().Where(r => db.Snapshots.Any(s => s.OrganizationId == org && s.Id == r.SnapshotId))
            .Select(x => x.Category).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        var expenses = await db.Expenses.AsNoTracking().Where(e => !e.IsAdministrative && db.Snapshots.Any(s => s.OrganizationId == org && s.Id == e.SnapshotId))
            .Select(x => x.Category).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == org, ct);
        var rules = await Targets(org, ct);
        var dimensions = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.Id, ct);
        var updated = await db.MlrDefinitions.AsNoTracking().Where(x => x.OrganizationId == org).Select(x => (DateTimeOffset?)x.UpdatedAt).SingleOrDefaultAsync(ct);
        return new(selection.Name, selection.Version, updated, selection.IsImplicit ? revenue : selection.RevenueCategories,
            selection.IsImplicit ? expenses : selection.ExpenseCategories, revenue, expenses, organization.MlrTarget,
            rules.Values.Select(x => new MlrTargetInfo(x.DimensionId, dimensions[x.DimensionId].Kind.ToString(), dimensions[x.DimensionId].Name, x.Target, x.Version, x.UpdatedAt))
                .OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray());
    }

    public async Task<MlrConfigurationInfo> Save(Guid org, string user, MlrDefinitionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80 || request.ExpectedVersion < 0)
            throw new ArgumentException("Provide a definition name up to 80 characters and its current version.");
        var catalog = await Read(org, ct);
        string[] Validate(string[]? values, string[] allowed)
        {
            if (values is null || values.Length is < 1 or > 64 || values.Any(x => x is null || x.Length > 80)
                || values.Distinct(StringComparer.Ordinal).Count() != values.Length || values.Any(x => !allowed.Contains(x, StringComparer.Ordinal)))
                throw new ArgumentException("Select 1–64 distinct authorized categories for both revenue and medical expenses. Administrative costs cannot be included.");
            return values.Order(StringComparer.Ordinal).ToArray();
        }
        var revenue = Validate(request.RevenueCategories, catalog.RevenueCatalog);
        var expenses = Validate(request.ExpenseCategories, catalog.ExpenseCatalog);
        var definition = await db.MlrDefinitions.Include(x => x.Components).SingleOrDefaultAsync(x => x.OrganizationId == org, ct);
        if ((definition?.Version ?? 0) != request.ExpectedVersion) throw new MlrConfigurationConflict();
        if (definition is null) { definition = new() { OrganizationId = org, Version = 0 }; db.MlrDefinitions.Add(definition); }
        // Retain matching component rows; removing and re-inserting the same unique keys can violate constraints.
        foreach (var component in definition.Components.ToArray())
            if (!(component.Type == "revenue" ? revenue : expenses).Contains(component.Category, StringComparer.Ordinal))
                db.MlrComponents.Remove(component);
        foreach (var (type, values) in new[] { ("revenue", revenue), ("medical", expenses) })
            foreach (var category in values)
                if (!definition.Components.Any(x => x.Type == type && x.Category == category))
                    definition.Components.Add(new() { OrganizationId = org, Type = type, Category = category });
        definition.Name = request.Name.Trim(); definition.Version++; definition.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(Event(org, user, "mlr.definition.updated", new { definition.Name, definition.Version, previousVersion = request.ExpectedVersion, revenueCategories = revenue, expenseCategories = expenses }));
        await db.SaveChangesAsync(ct);
        return await Read(org, ct);
    }

    public async Task<MlrConfigurationInfo> SaveTarget(Guid org, string user, Guid dimensionId, MlrTargetRequest request, CancellationToken ct)
    {
        if (request.Target is < 1 or > 100 || request.ExpectedVersion < 0) throw new ArgumentException("MLR target must be between 1 and 100%, with its current version.");
        if (!await db.Dimensions.AnyAsync(x => x.OrganizationId == org && x.Id == dimensionId, ct)) throw new ArgumentException("This target dimension is not available in your organization.");
        var rule = await db.MlrTargets.SingleOrDefaultAsync(x => x.OrganizationId == org && x.DimensionId == dimensionId, ct);
        if ((rule?.Version ?? 0) != request.ExpectedVersion) throw new MlrConfigurationConflict();
        var previousTarget = rule is null ? (decimal?)null : rule.Target;
        if (rule is null) { rule = new() { Id = Guid.NewGuid(), OrganizationId = org, DimensionId = dimensionId, Version = 0 }; db.MlrTargets.Add(rule); }
        rule.Target = decimal.Round(request.Target, 2); rule.Version++; rule.UpdatedAt = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(Event(org, user, "mlr.target.updated", new { dimensionId, previousTarget, rule.Target, rule.Version }));
        await db.SaveChangesAsync(ct);
        return await Read(org, ct);
    }

    public async Task RemoveTarget(Guid org, string user, Guid dimensionId, int version, CancellationToken ct)
    {
        var rule = await db.MlrTargets.SingleOrDefaultAsync(x => x.OrganizationId == org && x.DimensionId == dimensionId, ct)
            ?? throw new ArgumentException("This target override is not available in your organization.");
        if (version != rule.Version) throw new MlrConfigurationConflict();
        db.MlrTargets.Remove(rule);
        db.AuditEvents.Add(Event(org, user, "mlr.target.reset", new { dimensionId, previousTarget = rule.Target, previousVersion = version }));
        await db.SaveChangesAsync(ct);
    }

    private static AuditEvent Event(Guid org, string user, string action, object details)
        => new() { OrganizationId = org, UserId = user, Action = action, OccurredAt = DateTimeOffset.UtcNow, DetailsJson = JsonSerializer.Serialize(details) };
}
