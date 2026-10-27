using Hhh.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hhh.Infrastructure;

public sealed class AppUser : IdentityUser
{
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = "";
}

public sealed class HhhDbContext(DbContextOptions<HhhDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Dimension> Dimensions => Set<Dimension>();
    public DbSet<SourceBatch> SourceBatches => Set<SourceBatch>();
    public DbSet<FinancialSnapshot> Snapshots => Set<FinancialSnapshot>();
    public DbSet<RevenueFact> Revenue => Set<RevenueFact>();
    public DbSet<ExpenseFact> Expenses => Set<ExpenseFact>();
    public DbSet<MembershipFact> Membership => Set<MembershipFact>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();
    public DbSet<BudgetSnapshot> BudgetSnapshots => Set<BudgetSnapshot>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Organization>().Property(x => x.Name).HasMaxLength(180);
        b.Entity<Organization>().Property(x => x.MlrTarget).HasPrecision(5, 2);
        b.Entity<AppUser>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(180);
        b.Entity<Dimension>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Entity<Dimension>().HasIndex(x => new { x.OrganizationId, x.Kind, x.Name }).IsUnique();
        b.Entity<Dimension>().Property(x => x.Name).HasMaxLength(180);
        b.Entity<Dimension>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SourceBatch>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Entity<SourceBatch>().HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SourceBatch>().Property(x => x.Name).HasMaxLength(180);

        var s = b.Entity<FinancialSnapshot>();
        s.HasIndex(x => new
        {
            x.OrganizationId,
            x.EffectiveMonth,
            x.ReportMonth,
            x.GeneralLedgerMonth,
            x.PayerId,
            x.MedicalCenterId,
            x.ProviderId,
            x.ContractId,
            x.PlanId,
            x.LineOfBusinessId,
            x.RegionId
        }).IsUnique();
        s.HasOne<SourceBatch>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SourceBatchId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PayerId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MedicalCenterId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ProviderId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ContractId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PlanId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LineOfBusinessId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        s.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RegionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipFact>().ToTable("Facts_Membership").HasKey(x => x.SnapshotId);
        s.HasOne(x => x.Membership).WithOne().HasForeignKey<MembershipFact>(x => x.SnapshotId);
        b.Entity<RevenueFact>().ToTable("Facts_Revenue");
        b.Entity<ExpenseFact>().ToTable("Facts_MedicalExpenses");
        s.HasMany(x => x.Revenue).WithOne().HasForeignKey(x => x.SnapshotId);
        s.HasMany(x => x.Expenses).WithOne().HasForeignKey(x => x.SnapshotId);
        b.Entity<RevenueFact>().Property(x => x.Amount).HasPrecision(19, 4);
        b.Entity<ExpenseFact>().Property(x => x.Amount).HasPrecision(19, 4);
        b.Entity<RevenueFact>().Property(x => x.Category).HasMaxLength(80);
        b.Entity<ExpenseFact>().Property(x => x.Category).HasMaxLength(80);
        b.Entity<AuditEvent>().Property(x => x.Action).HasMaxLength(120);
        b.Entity<AuditEvent>().Property(x => x.UserId).HasMaxLength(450);
        b.Entity<AuditEvent>().HasIndex(x => new { x.OrganizationId, x.OccurredAt });

        var plan = b.Entity<BudgetPlan>();
        plan.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        plan.HasIndex(x => new { x.OrganizationId, x.Year, x.Version }).IsUnique();
        plan.Property(x => x.Name).HasMaxLength(180);
        plan.Property(x => x.Version).HasMaxLength(40);
        plan.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        var budget = b.Entity<BudgetSnapshot>();
        budget.HasOne<BudgetPlan>().WithMany(x => x.Snapshots).HasForeignKey(x => new { x.OrganizationId, x.BudgetPlanId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasIndex(x => new { x.BudgetPlanId, x.Month, x.PayerId, x.MedicalCenterId, x.ProviderId, x.ContractId, x.PlanId, x.LineOfBusinessId, x.RegionId }).IsUnique();
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PayerId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.MedicalCenterId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ProviderId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ContractId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.PlanId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.LineOfBusinessId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasOne<Dimension>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RegionId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        budget.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.BudgetSnapshotId);
        b.Entity<BudgetLine>().ToTable("Facts_Budget");
        b.Entity<BudgetLine>().HasIndex(x => new { x.BudgetSnapshotId, x.Type, x.Category }).IsUnique();
        b.Entity<BudgetLine>().Property(x => x.Amount).HasPrecision(19, 4);
        b.Entity<BudgetLine>().Property(x => x.Type).HasMaxLength(20);
        b.Entity<BudgetLine>().Property(x => x.Category).HasMaxLength(80);
    }
}
