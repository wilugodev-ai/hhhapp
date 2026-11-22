using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hhh.Domain;
using Hhh.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hhh.Tests;

public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task Synthea_import_links_files_and_exposes_only_the_organizations_inpatient_stays()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HhhDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Synthea integration fixture" };
        var email = $"admissions-{org.Id:N}@example.invalid";
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        try
        {
            var user = new AppUser { UserName = email, Email = email, OrganizationId = org.Id, DisplayName = "Admissions test" };
            Assert.True((await users.CreateAsync(user, SyntheticSeed.DemoPassword)).Succeeded);
            var patient = Guid.NewGuid();
            var facility = Guid.NewGuid();
            var inpatient = Guid.NewGuid();
            var emergency = Guid.NewGuid();
            var unclosed = Guid.NewGuid();
            var payer = Guid.NewGuid();
            var claim = Guid.NewGuid();
            var files = new Dictionary<string, string>
            {
                ["patients"] = $"Id,FIRST,LAST\n{patient},Sample,Patient",
                ["organizations"] = $"Id,NAME\n{facility},\"Hospital, West\"",
                ["encounters"] = "Id,PATIENT,ORGANIZATION,ENCOUNTERCLASS,START,STOP,CODE,DESCRIPTION,REASONCODE,REASONDESCRIPTION\n"
                    + $"{inpatient},{patient},{facility},inpatient,2025-11-01T14:00:00Z,2025-11-02T12:00:00Z,32485007,Admission,73211009,Diabetes mellitus\n"
                    + $"{emergency},{patient},{facility},emergency,2025-10-01T10:00:00Z,,50849002,Emergency room visit,,\n"
                    + $"{unclosed},{patient},{facility},inpatient,2025-09-01T08:00:00Z,,32485007,Admission,,",
                ["observations"] = $"DATE,PATIENT,ENCOUNTER,DESCRIPTION,VALUE,UNITS\n2025-11-01,{patient},{inpatient},\"Reading, repeated\",120,mmHg",
                ["claims"] = $"Id,PATIENTID,APPOINTMENTID,STATUS1\n{claim},{patient},{inpatient},CLOSED",
                ["claims_transactions"] = $"ID,CLAIMID,PATIENTID,APPOINTMENTID,TYPE,PAYMENTS\n{Guid.NewGuid()},{claim},{patient},{inpatient},PAYMENT,42.25",
                ["medications"] = $"PATIENT,ENCOUNTER,PAYER,DESCRIPTION,DISPENSES\n{patient},{emergency},{payer},Sample drug,3",
                ["payer_transitions"] = $"PATIENT,MEMBERID,START_DATE,END_DATE,PAYER\n{patient},member-1,2025-01-01,2025-12-31,{payer}",
                ["payers"] = $"Id,NAME\n{payer},Sample payer"
            };
            using (var malformed = ClinicalZip(new Dictionary<string, string>(files) { ["observations"] = "PATIENT,ENCOUNTER\ninvalid" }))
            await using (var failedScope = factory.Services.CreateAsyncScope())
            {
                var failedDb = failedScope.ServiceProvider.GetRequiredService<HhhDbContext>();
                await Assert.ThrowsAsync<InvalidDataException>(() => SyntheaImporter.ImportAsync(failedDb, org.Id, malformed));
            }
            Assert.False(await db.SyntheaDatasets.AnyAsync(s => s.OrganizationId == org.Id));
            using var sample = ClinicalZip(files);
            Assert.True(await SyntheaImporter.ImportAsync(db, org.Id, sample));
            Assert.False(await SyntheaImporter.ImportAsync(db, org.Id, sample));
            Assert.Equal(11, await db.SyntheaRecords.CountAsync(r => r.OrganizationId == org.Id));

            using var client = await Login(email);
            const string filesUrl = "/api/data/synthea/files";
            var catalog = await client.GetFromJsonAsync<JsonElement>(filesUrl);
            Assert.Equal(9, catalog.GetArrayLength());
            var prescriptions = await client.GetFromJsonAsync<JsonElement>(filesUrl + "/medications?column=DESCRIPTION&q=DRUG&page=200");
            Assert.Equal(1, prescriptions.GetProperty("page").GetInt32());
            Assert.Equal("Sample drug", prescriptions.GetProperty("rows")[0][3].GetString());
            Assert.Equal("Sample Patient", prescriptions.GetProperty("names").GetProperty(patient.ToString()).GetString());
            var coverage = await client.GetFromJsonAsync<JsonElement>(filesUrl + "/payer_transitions");
            Assert.Equal("Sample payer", coverage.GetProperty("names").GetProperty(payer.ToString()).GetString());
            var transactions = await client.GetFromJsonAsync<JsonElement>(filesUrl + "/claims_transactions?column=CLAIMID&q=" + claim);
            Assert.Equal("42.25", transactions.GetProperty("rows")[0][5].GetString());
            var empty = await client.GetFromJsonAsync<JsonElement>(filesUrl + "/claims?column=STATUS1&q=unknown");
            Assert.Empty(empty.GetProperty("rows").EnumerateArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(filesUrl + "/claims?column=bogus&q=anything")).StatusCode);
            var page = await client.GetFromJsonAsync<JsonElement>("/api/admissions");
            Assert.Equal(2, page.GetProperty("total").GetInt32());
            Assert.Equal(1, page.GetProperty("patients").GetInt32());
            Assert.Equal(1, page.GetProperty("facilities").GetInt32());
            Assert.Equal("Hospital, West", page.GetProperty("items")[0].GetProperty("facilityName").GetString());
            var path = $"/api/admissions/{inpatient}";
            var related = await client.GetFromJsonAsync<JsonElement>(path + "/records?table=observations");
            Assert.Contains(related.GetProperty("tables").EnumerateArray(), x => x.GetProperty("name").GetString() == "claims");
            Assert.Contains(related.GetProperty("rows")[0].EnumerateArray(), x => x.GetString() == "Reading, repeated");
            var resource = await client.GetAsync(path + "/fhir");
            Assert.Equal("application/fhir+json", resource.Content.Headers.ContentType?.MediaType);
            var fhir = await resource.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Encounter", fhir.GetProperty("resourceType").GetString());
            Assert.Equal("IMP", fhir.GetProperty("class").GetProperty("code").GetString());
            Assert.Equal("2025-11-02T12:00:00Z", fhir.GetProperty("period").GetProperty("end").GetString());
            var open = await client.GetFromJsonAsync<JsonElement>($"/api/admissions/{unclosed}/fhir");
            Assert.False(open.GetProperty("period").TryGetProperty("end", out _));
            Assert.False(open.TryGetProperty("reasonCode", out _));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admissions/{emergency}/fhir")).StatusCode);
            using var other = await Login("summit@hhh.local");
            Assert.Empty((await other.GetFromJsonAsync<JsonElement>(filesUrl)).EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(filesUrl + "/payer_transitions")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/records")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/fhir")).StatusCode);
            using var anonymous = factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admissions")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(filesUrl + "/medications")).StatusCode);
        }
        finally
        {
            await db.AuditEvents.Where(a => a.OrganizationId == org.Id).ExecuteDeleteAsync();
            await db.Users.Where(u => u.OrganizationId == org.Id).ExecuteDeleteAsync();
            await db.SyntheaDatasets.Where(s => s.OrganizationId == org.Id).ExecuteDeleteAsync();
            await db.Organizations.Where(o => o.Id == org.Id).ExecuteDeleteAsync();
        }
    }

    private static MemoryStream ClinicalZip(Dictionary<string, string> files)
    {
        var bytes = new MemoryStream();
        using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name + ".csv").Open());
                writer.Write(text);
            }
        }
        bytes.Position = 0;
        return bytes;
    }
}
