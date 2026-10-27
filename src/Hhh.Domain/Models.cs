namespace Hhh.Domain;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal MlrTarget { get; set; } = 85m;
}

public enum DimensionKind { Payer, MedicalCenter, Provider, Contract, Plan, LineOfBusiness, Region }

public sealed class Dimension
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public DimensionKind Kind { get; set; }
    public string Name { get; set; } = "";
}

public sealed class SourceBatch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset LoadedAt { get; set; }
    public bool IsSynthetic { get; set; }
    public int SnapshotCount { get; set; }
}

public sealed class FinancialSnapshot
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SourceBatchId { get; set; }
    public DateOnly EffectiveMonth { get; set; }
    public DateOnly ReportMonth { get; set; }
    public DateOnly GeneralLedgerMonth { get; set; }
    public Guid PayerId { get; set; }
    public Guid MedicalCenterId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ContractId { get; set; }
    public Guid PlanId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid RegionId { get; set; }
    public MembershipFact Membership { get; set; } = new();
    public List<RevenueFact> Revenue { get; set; } = [];
    public List<ExpenseFact> Expenses { get; set; } = [];
}

public sealed class MembershipFact
{
    public long SnapshotId { get; set; }
    public int MemberMonths { get; set; }
}

public sealed class RevenueFact
{
    public long Id { get; set; }
    public long SnapshotId { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
}

public sealed class ExpenseFact
{
    public long Id { get; set; }
    public long SnapshotId { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsAdministrative { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = "";
    public string Action { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class BudgetPlan
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public int Year { get; set; }
    public string Version { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
    public bool IsSynthetic { get; set; }
    public List<BudgetSnapshot> Snapshots { get; set; } = [];
}

public sealed class BudgetSnapshot
{
    public long Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BudgetPlanId { get; set; }
    public DateOnly Month { get; set; }
    public Guid PayerId { get; set; }
    public Guid MedicalCenterId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ContractId { get; set; }
    public Guid PlanId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid RegionId { get; set; }
    public int PlannedMemberMonths { get; set; }
    public List<BudgetLine> Lines { get; set; } = [];
}

public sealed class BudgetLine
{
    public long Id { get; set; }
    public long BudgetSnapshotId { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
}

public static class Roles
{
    public const string SystemAdministrator = "System Administrator";
    public const string OrganizationAdministrator = "Organization Administrator";
    public const string Cfo = "Chief Financial Officer";
    public const string Analyst = "Financial Analyst";
    public const string Viewer = "Executive Viewer";
    public static readonly string[] All = [SystemAdministrator, OrganizationAdministrator, Cfo, Analyst, Viewer];
}
