using System.Text.Json;
using System.Text.Json.Serialization;
using Hhh.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static Hhh.Api.SyntheaFiles;

namespace Hhh.Api;

public static class Admissions
{
    private static readonly JsonSerializerOptions FhirJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true
    };

    public static async Task<IResult> List(HhhDbContext db, Guid org, int page, CancellationToken ct)
    {
        var query = db.SyntheaAdmissions.AsNoTracking().Where(a => a.OrganizationId == org);
        var totals = await query.GroupBy(a => a.OrganizationId).Select(group => new
        {
            Admissions = group.Count(), Patients = group.Select(a => a.PatientId).Distinct().Count(),
            Facilities = group.Select(a => a.FacilityId).Distinct().Count()
        }).SingleOrDefaultAsync(ct);
        var total = totals?.Admissions ?? 0;
        page = PageInRange(page, total);
        var items = await query.OrderByDescending(a => a.AdmittedAt).ThenBy(a => a.EncounterId)
            .Skip((page - 1) * PageSize).Take(PageSize).Select(a => new
            {
                a.EncounterId, a.PatientName, a.FacilityName, a.AdmittedAt, a.DischargedAt, a.Description, a.Reason
            }).ToArrayAsync(ct);
        return Results.Ok(new { total, patients = totals?.Patients ?? 0, facilities = totals?.Facilities ?? 0, page, pageSize = PageSize, items });
    }

    public static async Task<IResult> Records(HhhDbContext db, Guid org, Guid encounterId, string? table, int page, CancellationToken ct)
    {
        var patient = await db.SyntheaAdmissions.Where(a => a.OrganizationId == org && a.EncounterId == encounterId)
            .Select(a => (Guid?)a.PatientId).SingleOrDefaultAsync(ct);
        if (patient == null) return Results.NotFound();
        var records = db.SyntheaRecords.AsNoTracking()
            .Where(r => r.OrganizationId == org && r.EncounterId == encounterId && r.PatientId == patient);
        var tables = await records.GroupBy(r => r.SourceTable).OrderBy(g => g.Key)
            .Select(g => new { Name = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        table ??= tables.Any(t => t.Name == "conditions") ? "conditions" : "encounters";
        var selected = tables.SingleOrDefault(t => t.Name == table);
        if (selected == null) return Results.NotFound();
        page = PageInRange(page, selected.Count);
        var rows = await ReadPage(records.Where(r => r.SourceTable == table), page, ct);
        var columns = rows[0].Keys.ToArray();
        return Results.Ok(new { tables, table, total = selected.Count, page, pageSize = PageSize, columns,
            rows = Values(rows, columns) });
    }

    public static async Task<IResult> Fhir(HhhDbContext db, Guid org, Guid encounterId, CancellationToken ct)
    {
        var a = await db.SyntheaAdmissions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == org && x.EncounterId == encounterId, ct);
        if (a == null) return Results.NotFound();
        return Results.Json(new
        {
            resourceType = "Encounter", id = a.EncounterId,
            meta = new { source = "https://synthetichealth.github.io/synthea/" },
            status = a.DischargedAt.HasValue ? "finished" : "unknown",
            @class = new { system = "http://terminology.hl7.org/CodeSystem/v3-ActCode", code = "IMP", display = "inpatient encounter" },
            type = new[] { Concept(a.EncounterCode, a.Description) },
            subject = Reference("Patient", a.PatientId, a.PatientName),
            serviceProvider = Reference("Organization", a.FacilityId, a.FacilityName),
            period = new { start = a.AdmittedAt.UtcDateTime, end = a.DischargedAt?.UtcDateTime },
            reasonCode = a.ReasonCode == null && a.Reason == null ? null : new[] { Concept(a.ReasonCode, a.Reason) }
        }, FhirJson, contentType: "application/fhir+json");
    }

    private static object Concept(string? code, string? display) => new
    {
        coding = code == null ? null : new[] { new { system = "http://snomed.info/sct", code, display } },
        text = display
    };

    private static object Reference(string type, Guid id, string display) => new
    {
        type, identifier = new { system = "urn:ietf:rfc:3986", value = $"urn:uuid:{id}" }, display
    };
}
