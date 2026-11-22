using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hhh.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Npgsql;
using NpgsqlTypes;

namespace Hhh.Infrastructure;

public static class SyntheaImporter
{
    public static async Task<bool> ImportAsync(HhhDbContext db, Guid organizationId, Stream source, CancellationToken ct = default)
    {
        source.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, ct));
        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        var entries = archive.Entries.Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => Path.GetFileNameWithoutExtension(e.Name).ToLowerInvariant(), StringComparer.Ordinal);
        string[] required = ["patients", "organizations", "encounters"];
        foreach (var table in required)
            if (!entries.ContainsKey(table)) throw new InvalidDataException($"Missing {table}.csv in Synthea archive.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Concurrent imports for this organization must see the committed fingerprint before inserting.
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Organizations WITH (UPDLOCK, HOLDLOCK) WHERE Id = {organizationId}", ct);
        else
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Organizations\" WHERE \"Id\" = {organizationId} FOR UPDATE", ct);
        if (!await db.Organizations.AnyAsync(o => o.Id == organizationId && o.IsActive, ct))
            throw new InvalidOperationException("Select an existing, active HHH organization.");
        var previous = await db.SyntheaDatasets.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        if (previous != null)
        {
            if (previous.ArchiveSha256 != hash)
                throw new InvalidOperationException("This organization already has a different Synthea sample.");
            return false;
        }

        db.SyntheaDatasets.Add(new() { OrganizationId = organizationId, ArchiveSha256 = hash, ImportedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        var patients = new Dictionary<Guid, string>();
        var facilities = new Dictionary<Guid, string>();
        var admissions = new List<SyntheaAdmission>();
        var batchSize = db.Database.IsNpgsql() ? 10000 : 1000;
        var batch = new List<SyntheaRecord>(batchSize);
        foreach (var table in required.Concat(entries.Keys.Except(required).Order(StringComparer.Ordinal)))
        {
            var rowNumber = 0;
            foreach (var fields in ReadCsv(entries[table]))
            {
                ct.ThrowIfCancellationRequested();
                rowNumber++;
                var patient = table == "patients" ? Id(fields, "Id") : Id(fields, "PATIENT") ?? Id(fields, "PATIENTID");
                var encounter = table == "encounters" ? Id(fields, "Id") : Id(fields, "ENCOUNTER") ?? Id(fields, "APPOINTMENTID");
                if (table == "patients")
                    patients.Add(Guid.Parse(fields["Id"]), $"{fields["FIRST"]} {fields["LAST"]}".Trim());
                if (table == "organizations")
                    facilities.Add(Guid.Parse(fields["Id"]), fields["NAME"]);
                if (table == "encounters" && fields["ENCOUNTERCLASS"].Equals("inpatient", StringComparison.OrdinalIgnoreCase))
                    admissions.Add(Admission(organizationId, fields, patients, facilities));
                batch.Add(new() { OrganizationId = organizationId, SourceTable = table, RowNumber = rowNumber,
                    PatientId = patient, EncounterId = encounter, FieldsJson = JsonSerializer.Serialize(fields) });
                if (batch.Count == batchSize) await SaveBatch(db, batch, ct);
            }
        }
        if (batch.Count > 0) await SaveBatch(db, batch, ct);
        db.SyntheaAdmissions.AddRange(admissions);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task SaveBatch(HhhDbContext db, List<SyntheaRecord> rows, CancellationToken ct)
    {
        if (db.Database.GetDbConnection() is NpgsqlConnection connection)
        {
            await using var writer = await connection.BeginBinaryImportAsync(
                """COPY "SyntheaRecords" ("OrganizationId","SourceTable","RowNumber","PatientId","EncounterId","FieldsJson") FROM STDIN (FORMAT BINARY)""", ct);
            foreach (var row in rows)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(row.OrganizationId, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(row.SourceTable, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.RowNumber, NpgsqlDbType.Integer, ct);
                if (row.PatientId is Guid patient) await writer.WriteAsync(patient, NpgsqlDbType.Uuid, ct);
                else await writer.WriteNullAsync(ct);
                if (row.EncounterId is Guid encounter) await writer.WriteAsync(encounter, NpgsqlDbType.Uuid, ct);
                else await writer.WriteNullAsync(ct);
                await writer.WriteAsync(row.FieldsJson, NpgsqlDbType.Text, ct);
            }
            await writer.CompleteAsync(ct);
        }
        else
        {
            db.SyntheaRecords.AddRange(rows);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
        rows.Clear();
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(ZipArchiveEntry entry)
    {
        using var parser = new TextFieldParser(entry.Open(), Encoding.UTF8, detectEncoding: true)
        {
            HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new InvalidDataException($"No header in {entry.Name}.");
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException($"Invalid column names in {entry.Name}.");
        while (!parser.EndOfData)
        {
            var values = parser.ReadFields();
            if (values?.Length != headers.Length) throw new InvalidDataException($"Invalid column count in {entry.Name}.");
            var fields = new Dictionary<string, string>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++) fields.Add(headers[i], values[i]);
            yield return fields;
        }
    }

    private static Guid? Id(Dictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.Length > 0 ? Guid.Parse(value) : null;

    private static SyntheaAdmission Admission(Guid organizationId, Dictionary<string, string> fields,
        Dictionary<Guid, string> patients, Dictionary<Guid, string> facilities)
    {
        var encounter = Guid.Parse(fields["Id"]);
        var patient = Guid.Parse(fields["PATIENT"]);
        var facility = Guid.Parse(fields["ORGANIZATION"]);
        if (!patients.TryGetValue(patient, out var patientName) || !facilities.TryGetValue(facility, out var facilityName))
            throw new InvalidDataException($"Unresolved patient or facility for inpatient encounter {encounter}.");
        var start = Timestamp(fields["START"]);
        var stop = fields["STOP"].Length > 0 ? Timestamp(fields["STOP"]) : (DateTimeOffset?)null;
        if (stop < start) throw new InvalidDataException($"Discharge precedes admission for encounter {encounter}.");
        return new()
        {
            OrganizationId = organizationId, EncounterId = encounter, PatientId = patient, PatientName = patientName,
            FacilityId = facility, FacilityName = facilityName, AdmittedAt = start, DischargedAt = stop,
            EncounterCode = fields["CODE"], Description = fields["DESCRIPTION"],
            ReasonCode = fields.GetValueOrDefault("REASONCODE") is { Length: > 0 } code ? code : null,
            Reason = fields.GetValueOrDefault("REASONDESCRIPTION") is { Length: > 0 } reason ? reason : null
        };
    }

    private static DateTimeOffset Timestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
