using Hhh.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Infrastructure;

public static class SyntheticSeed
{
    public const string DemoPassword = "Hhh-Demo-2026!";

    public static async Task RunAsync(HhhDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole> roles)
    {
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role)) await Ensure(roles.CreateAsync(new IdentityRole(role)));

        for (var orgIndex = 0; orgIndex < 2; orgIndex++)
        {
            var orgId = Guid.Parse(orgIndex == 0 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222");
            if (!await db.Organizations.AnyAsync(x => x.Id == orgId))
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                db.Organizations.Add(new() { Id = orgId, Name = orgIndex == 0 ? "Horizon Care Partners" : "Summit Health Group" });
                await db.SaveChangesAsync();
                var dims = new Dictionary<DimensionKind, List<Dimension>>();
                void Add(DimensionKind kind, params string[] names)
                {
                    dims[kind] = names.Select(name => new Dimension { Id = Guid.NewGuid(), OrganizationId = orgId, Kind = kind, Name = name }).ToList();
                    db.Dimensions.AddRange(dims[kind]);
                }
                Add(DimensionKind.Payer, "Horizon Health", "Northstar Medicare", "Meridian Care");
                Add(DimensionKind.MedicalCenter, "Bayside Medical", "Westlake Health", "Palm Grove Care", "Northpoint Clinic");
                Add(DimensionKind.Provider, "Synthetic PCP A", "Synthetic PCP B", "Synthetic PCP C", "Synthetic PCP D");
                Add(DimensionKind.Contract, "Value Care 2026", "Community Risk", "Senior Advantage");
                Add(DimensionKind.Plan, "Horizon HMO", "Northstar PPO", "Meridian HMO");
                Add(DimensionKind.LineOfBusiness, "Medicare Advantage", "Managed Medicaid");
                Add(DimensionKind.Region, "South Florida", "Central Florida");
                var batch = new SourceBatch
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    Name = "Synthetic financial baseline v1",
                    IsSynthetic = true,
                    LoadedAt = new DateTimeOffset(2026, 10, 2, 8, 30, 0, TimeSpan.Zero),
                    SnapshotCount = 21 * 12
                };
                db.SourceBatches.Add(batch);
                await db.SaveChangesAsync();
                var random = new Random(700 + orgIndex);
                for (var month = 0; month < 21; month++)
                    for (var p = 0; p < 3; p++)
                        for (var c = 0; c < 4; c++)
                        {
                            var effective = new DateOnly(2025, 1, 1).AddMonths(month);
                            var members = (int)((540 + p * 95 + c * 46) * (1 + month * 0.008) * (orgIndex == 0 ? 1 : 0.63));
                            var pmpm = 1030m + p * 62 + month * 3.8m + c * 8;
                            var revenue = decimal.Round(members * pmpm, 2);
                            var season = (decimal)Math.Sin(month / 12.0 * Math.PI * 2) * 0.012m;
                            var mlr = 0.792m + p * 0.031m + c * 0.005m + season + (decimal)random.NextDouble() * 0.009m;
                            if (month == 20 && p == 2) mlr += 0.025m;
                            var medical = decimal.Round(revenue * mlr, 2);
                            var snapshot = new FinancialSnapshot
                            {
                                OrganizationId = orgId,
                                SourceBatchId = batch.Id,
                                EffectiveMonth = effective,
                                ReportMonth = effective.AddMonths(1),
                                GeneralLedgerMonth = effective,
                                PayerId = dims[DimensionKind.Payer][p].Id,
                                MedicalCenterId = dims[DimensionKind.MedicalCenter][c].Id,
                                ProviderId = dims[DimensionKind.Provider][c].Id,
                                ContractId = dims[DimensionKind.Contract][p].Id,
                                PlanId = dims[DimensionKind.Plan][p].Id,
                                LineOfBusinessId = dims[DimensionKind.LineOfBusiness][p == 2 ? 1 : 0].Id,
                                RegionId = dims[DimensionKind.Region][c < 2 ? 0 : 1].Id,
                                Membership = new() { MemberMonths = members }
                            };
                            var revenueParts = new (string Category, decimal Weight)[] { ("Capitation", .79m), ("Premium", .09m),
                        ("Risk adjustment", .065m), ("Shared savings", .02m), ("Quality bonuses", .015m), ("Incentives", .01m), ("Other revenue", .01m) };
                            var allocated = 0m;
                            foreach (var part in revenueParts)
                            {
                                var amount = part.Category == "Other revenue" ? revenue - allocated : decimal.Round(revenue * part.Weight, 2);
                                snapshot.Revenue.Add(new() { Category = part.Category, Amount = amount });
                                allocated += amount;
                            }
                            var expenseParts = new (string Category, decimal Weight)[] { ("Inpatient", .30m), ("Outpatient", .18m),
                        ("Physician", .14m), ("Specialist", .09m), ("Pharmacy", .19m), ("Ancillary", .05m),
                        ("IBNR", .045m), ("Stop-loss recoveries", -.01m), ("Other medical", .015m) };
                            allocated = 0m;
                            foreach (var part in expenseParts)
                            {
                                var amount = part.Category == "Other medical" ? medical - allocated : decimal.Round(medical * part.Weight, 2);
                                snapshot.Expenses.Add(new() { Category = part.Category, Amount = amount });
                                allocated += amount;
                            }
                            snapshot.Expenses.Add(new() { Category = "Administrative", Amount = decimal.Round(revenue * .063m, 2), IsAdministrative = true });
                            db.Snapshots.Add(snapshot);
                        }
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            var accounts = orgIndex == 0
                ? new[] { ("demo@hhh.local", "Alex Morgan", Roles.Cfo), ("analyst@hhh.local", "Jordan Lee", Roles.Analyst),
                    ("viewer@hhh.local", "Taylor Reed", Roles.Viewer), ("admin@hhh.local", "Casey Rivera", Roles.OrganizationAdministrator),
                    ("sysadmin@hhh.local", "Morgan Blake", Roles.SystemAdministrator) }
                : new[] { ("summit@hhh.local", "Sam Patel", Roles.Cfo), ("summit-admin@hhh.local", "Jamie Park", Roles.OrganizationAdministrator) };
            foreach (var (email, name, role) in accounts)
            {
                if (await users.FindByEmailAsync(email) is not null) continue;
                var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name, OrganizationId = orgId };
                await Ensure(users.CreateAsync(user, DemoPassword));
                await Ensure(users.AddToRoleAsync(user, role));
            }
            await BudgetSeed.RunAsync(db, orgId, orgIndex);
            if (!await db.MlrDefinitions.AnyAsync(x => x.OrganizationId == orgId))
            {
                var definition = new MlrDefinition { OrganizationId = orgId, UpdatedAt = DateTimeOffset.UtcNow };
                var revenueCategories = await db.Revenue.Where(r => db.Snapshots.Any(s => s.Id == r.SnapshotId && s.OrganizationId == orgId)).Select(r => r.Category).Distinct().ToListAsync();
                var medicalCategories = await db.Expenses.Where(e => !e.IsAdministrative && db.Snapshots.Any(s => s.Id == e.SnapshotId && s.OrganizationId == orgId)).Select(e => e.Category).Distinct().ToListAsync();
                definition.Components = revenueCategories.Select(c => new MlrComponent { OrganizationId = orgId, Type = "revenue", Category = c })
                    .Concat(medicalCategories.Select(c => new MlrComponent { OrganizationId = orgId, Type = "medical", Category = c })).ToList();
                db.MlrDefinitions.Add(definition);
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task Ensure(Task<IdentityResult> task)
    {
        var result = await task;
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
