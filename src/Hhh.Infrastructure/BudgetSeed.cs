using Hhh.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Infrastructure;

public static class BudgetSeed
{
    public static async Task RunAsync(HhhDbContext db, Guid orgId, int orgIndex)
    {
        var dimensions = await db.Dimensions.AsNoTracking().Where(x => x.OrganizationId == orgId).ToListAsync();
        Guid Id(DimensionKind kind, string name) => dimensions.Single(x => x.Kind == kind && x.Name == name).Id;
        string[] payers = ["Horizon Health", "Northstar Medicare", "Meridian Care"];
        string[] centers = ["Bayside Medical", "Westlake Health", "Palm Grove Care", "Northpoint Clinic"];
        string[] providers = ["Synthetic PCP A", "Synthetic PCP B", "Synthetic PCP C", "Synthetic PCP D"];
        string[] contracts = ["Value Care 2026", "Community Risk", "Senior Advantage"];
        string[] plans = ["Horizon HMO", "Northstar PPO", "Meridian HMO"];
        foreach (var year in new[] { 2025, 2026 })
        {
            if (await db.BudgetPlans.AnyAsync(x => x.OrganizationId == orgId && x.Year == year && x.Version == "Baseline v1")) continue;
            var plan = new BudgetPlan
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                Year = year,
                Name = $"{year} operating budget",
                Version = "Baseline v1",
                ApprovedAt = new DateTimeOffset(year - 1, 12, 15, 12, 0, 0, TimeSpan.Zero),
                IsSynthetic = true
            };
            for (var month = 1; month <= 12; month++)
                for (var p = 0; p < 3; p++)
                    for (var c = 0; c < 4; c++)
                    {
                        // Independent planning drivers, not a discount applied to observed actuals.
                        var elapsed = (year - 2025) * 12 + month - 1;
                        var members = (int)((545 + p * 95 + c * 46) * (1 + elapsed * .0075) * (orgIndex == 0 ? 1 : .63));
                        var revenue = decimal.Round(members * (1035m + p * 62 + elapsed * 3.2m + c * 8), 2);
                        var medical = decimal.Round(revenue * (.80m + p * .027m + c * .004m), 2);
                        var row = new BudgetSnapshot
                        {
                            OrganizationId = orgId,
                            BudgetPlanId = plan.Id,
                            Month = new(year, month, 1),
                            PayerId = Id(DimensionKind.Payer, payers[p]),
                            MedicalCenterId = Id(DimensionKind.MedicalCenter, centers[c]),
                            ProviderId = Id(DimensionKind.Provider, providers[c]),
                            ContractId = Id(DimensionKind.Contract, contracts[p]),
                            PlanId = Id(DimensionKind.Plan, plans[p]),
                            LineOfBusinessId = Id(DimensionKind.LineOfBusiness, p == 2 ? "Managed Medicaid" : "Medicare Advantage"),
                            RegionId = Id(DimensionKind.Region, c < 2 ? "South Florida" : "Central Florida"),
                            PlannedMemberMonths = members
                        };
                        Add(row, "revenue", revenue, [("Capitation", .79m), ("Premium", .09m), ("Risk adjustment", .065m),
                    ("Shared savings", .02m), ("Quality bonuses", .015m), ("Incentives", .01m), ("Other revenue", .01m)]);
                        Add(row, "medical", medical, [("Inpatient", .30m), ("Outpatient", .18m), ("Physician", .14m), ("Specialist", .09m),
                    ("Pharmacy", .19m), ("Ancillary", .05m), ("IBNR", .045m), ("Stop-loss recoveries", -.01m), ("Other medical", .015m)]);
                        row.Lines.Add(new() { Type = "administrative", Category = "Administrative", Amount = decimal.Round(revenue * .062m, 2) });
                        plan.Snapshots.Add(row);
                    }
            db.BudgetPlans.Add(plan);
            await db.SaveChangesAsync();
        }
    }

    private static void Add(BudgetSnapshot row, string type, decimal total, (string Category, decimal Weight)[] parts)
    {
        var allocated = 0m;
        for (var i = 0; i < parts.Length; i++)
        {
            var amount = i == parts.Length - 1 ? total - allocated : decimal.Round(total * parts[i].Weight, 2);
            row.Lines.Add(new() { Type = type, Category = parts[i].Category, Amount = amount });
            allocated += amount;
        }
    }
}
