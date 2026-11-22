using System.Text.Json;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Api;

public static class SyntheaFiles
{
    internal const int PageSize = 20;
    internal static int PageInRange(int page, int total) => Math.Clamp(page, 1, total == 0 ? 1 : (total - 1) / PageSize + 1);

    public static async Task<IResult> Catalog(HhhDbContext db, Guid org, CancellationToken ct)
    {
        var files = await db.SyntheaRecords.Where(r => r.OrganizationId == org).GroupBy(r => r.SourceTable)
            .Select(g => new { Name = g.Key, Count = g.Count() }).OrderBy(f => f.Name).ToArrayAsync(ct);
        return Results.Ok(files);
    }

    public static async Task<IResult> Read(HhhDbContext db, Guid org, string table, int page, string? column, string? q, CancellationToken ct)
    {
        var source = db.SyntheaRecords.AsNoTracking().Where(r => r.OrganizationId == org && r.SourceTable == table);
        var first = await source.OrderBy(r => r.RowNumber).Select(r => r.FieldsJson).FirstOrDefaultAsync(ct);
        if (first == null) return Results.NotFound();
        var columns = Fields(first).Keys.ToArray();
        var term = q?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var key = columns.FirstOrDefault(c => c.Equals(column, StringComparison.OrdinalIgnoreCase));
            if (key == null || term.Length > 200) throw new ArgumentException("Choose a source column and enter up to 200 characters.");
            source = Find(db, org, table, key, term);
        }
        var total = await source.CountAsync(ct);
        page = PageInRange(page, total);
        var rows = await ReadPage(source, page, ct);
        var names = await ReferenceNames(db, org, rows, ct);
        return Results.Ok(new { table, total, page, pageSize = PageSize, columns, rows = Values(rows, columns), names });
    }

    // The selected column comes from this organization's actual CSV header. Both the key and the search term are parameters.
    private static IQueryable<SyntheaRecord> Find(HhhDbContext db, Guid org, string table, string key, string term)
    {
        if (db.Database.IsSqlServer())
        {
            var path = "$." + JsonSerializer.Serialize(key);
            return db.SyntheaRecords.FromSqlInterpolated($"""
                SELECT r.* FROM [SyntheaRecords] r WHERE r.[OrganizationId] = {org} AND r.[SourceTable] = {table}
                AND CHARINDEX(LOWER({term}), LOWER(JSON_VALUE(r.[FieldsJson], {path}))) > 0
                """).AsNoTracking();
        }
        return db.SyntheaRecords.FromSqlInterpolated($"""
            SELECT r.* FROM "SyntheaRecords" r WHERE r."OrganizationId" = {org} AND r."SourceTable" = {table}
            AND strpos(lower(r."FieldsJson"::json ->> {key}), lower({term})) > 0
            """).AsNoTracking();
    }

    internal static async Task<Dictionary<string, string>[]> ReadPage(IQueryable<SyntheaRecord> source, int page, CancellationToken ct)
    {
        var json = await source.OrderBy(r => r.RowNumber).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(r => r.FieldsJson).ToArrayAsync(ct);
        return json.Select(Fields).ToArray();
    }

    private static Dictionary<string, string> Fields(string json) =>
        new(JsonSerializer.Deserialize<Dictionary<string, string>>(json)!, StringComparer.OrdinalIgnoreCase);
    internal static string[][] Values(Dictionary<string, string>[] rows, string[] columns) =>
        rows.Select(row => columns.Select(column => row[column]).ToArray()).ToArray();

    private static async Task<Dictionary<string, string>> ReferenceNames(HhhDbContext db, Guid org, Dictionary<string, string>[] rows, CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = db.SyntheaRecords.Where(r => r.OrganizationId == org);
        var patientIds = rows.Select(r => r.GetValueOrDefault("PATIENT") ?? r.GetValueOrDefault("PATIENTID"))
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToArray();
        if (patientIds.Length > 0)
        {
            var patients = await directory.Where(r => r.SourceTable == "patients" && r.PatientId.HasValue && patientIds.Contains(r.PatientId.Value))
                .Select(r => r.FieldsJson).ToArrayAsync(ct);
            foreach (var p in patients.Select(Fields))
                names[p["Id"]] = $"{p["FIRST"]} {p["LAST"]}".Trim();
        }
        string[] payerColumns = ["PAYER", "SECONDARY_PAYER", "PRIMARYPATIENTINSURANCEID", "SECONDARYPATIENTINSURANCEID"];
        var payerIds = rows.SelectMany(r => payerColumns.Select(c => r.GetValueOrDefault(c)))
            .Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (payerIds.Count > 0)
        {
            var payers = await directory.Where(r => r.SourceTable == "payers").Select(r => r.FieldsJson).ToArrayAsync(ct);
            foreach (var p in payers.Select(Fields))
                if (p.TryGetValue("Id", out var id) && payerIds.Contains(id) && p.TryGetValue("NAME", out var name)) names[id] = name;
        }
        return names;
    }
}
