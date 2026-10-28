using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hhh.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstallationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    AdministratorSetupCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationStates", x => x.Id);
                    table.CheckConstraint("CK_InstallationState_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    MlrTarget = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AccessVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InvitationPending = table.Column<bool>(type: "boolean", nullable: false),
                    InvitationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    InvitationExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AccessVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSynthetic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPlans", x => x.Id);
                    table.UniqueConstraint("AK_BudgetPlans_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_BudgetPlans_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dimensions", x => x.Id);
                    table.UniqueConstraint("AK_Dimensions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Dimensions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MlrDefinitions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlrDefinitions", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_MlrDefinitions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SourceBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    LoadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsSynthetic = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceBatches", x => x.Id);
                    table.UniqueConstraint("AK_SourceBatches_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_SourceBatches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    PayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedMemberMonths = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_BudgetPlans_OrganizationId_BudgetPlanId",
                        columns: x => new { x.OrganizationId, x.BudgetPlanId },
                        principalTable: "BudgetPlans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_ContractId",
                        columns: x => new { x.OrganizationId, x.ContractId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_LineOfBusinessId",
                        columns: x => new { x.OrganizationId, x.LineOfBusinessId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_MedicalCenterId",
                        columns: x => new { x.OrganizationId, x.MedicalCenterId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_PayerId",
                        columns: x => new { x.OrganizationId, x.PayerId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_PlanId",
                        columns: x => new { x.OrganizationId, x.PlanId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_ProviderId",
                        columns: x => new { x.OrganizationId, x.ProviderId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetSnapshots_Dimensions_OrganizationId_RegionId",
                        columns: x => new { x.OrganizationId, x.RegionId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MlrTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DimensionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlrTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MlrTargets_Dimensions_OrganizationId_DimensionId",
                        columns: x => new { x.OrganizationId, x.DimensionId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MlrComponents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlrComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MlrComponents_MlrDefinitions_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "MlrDefinitions",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneralLedgerMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    PayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_ContractId",
                        columns: x => new { x.OrganizationId, x.ContractId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_LineOfBusinessId",
                        columns: x => new { x.OrganizationId, x.LineOfBusinessId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_MedicalCenterId",
                        columns: x => new { x.OrganizationId, x.MedicalCenterId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_PayerId",
                        columns: x => new { x.OrganizationId, x.PayerId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_PlanId",
                        columns: x => new { x.OrganizationId, x.PlanId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_ProviderId",
                        columns: x => new { x.OrganizationId, x.ProviderId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_Dimensions_OrganizationId_RegionId",
                        columns: x => new { x.OrganizationId, x.RegionId },
                        principalTable: "Dimensions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Snapshots_SourceBatches_OrganizationId_SourceBatchId",
                        columns: x => new { x.OrganizationId, x.SourceBatchId },
                        principalTable: "SourceBatches",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Facts_Budget",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facts_Budget", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facts_Budget_BudgetSnapshots_BudgetSnapshotId",
                        column: x => x.BudgetSnapshotId,
                        principalTable: "BudgetSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Facts_MedicalExpenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    IsAdministrative = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facts_MedicalExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facts_MedicalExpenses_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Facts_Membership",
                columns: table => new
                {
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    MemberMonths = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facts_Membership", x => x.SnapshotId);
                    table.ForeignKey(
                        name: "FK_Facts_Membership_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Facts_Revenue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facts_Revenue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facts_Revenue_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "InstallationStates",
                columns: new[] { "Id", "AdministratorSetupCompletedAt" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId_DisplayName",
                table: "AspNetUsers",
                columns: new[] { "OrganizationId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganizationId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "OrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_OrganizationId_Year_Version",
                table: "BudgetPlans",
                columns: new[] { "OrganizationId", "Year", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_BudgetPlanId_Month_PayerId_MedicalCenterId_~",
                table: "BudgetSnapshots",
                columns: new[] { "BudgetPlanId", "Month", "PayerId", "MedicalCenterId", "ProviderId", "ContractId", "PlanId", "LineOfBusinessId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_BudgetPlanId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "BudgetPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_ContractId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_LineOfBusinessId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "LineOfBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_MedicalCenterId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "MedicalCenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_PayerId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "PayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_PlanId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_ProviderId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_OrganizationId_RegionId",
                table: "BudgetSnapshots",
                columns: new[] { "OrganizationId", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Dimensions_OrganizationId_Kind_Name",
                table: "Dimensions",
                columns: new[] { "OrganizationId", "Kind", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facts_Budget_BudgetSnapshotId_Type_Category",
                table: "Facts_Budget",
                columns: new[] { "BudgetSnapshotId", "Type", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facts_MedicalExpenses_SnapshotId",
                table: "Facts_MedicalExpenses",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Facts_Revenue_SnapshotId",
                table: "Facts_Revenue",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_MlrComponents_OrganizationId_Type_Category",
                table: "MlrComponents",
                columns: new[] { "OrganizationId", "Type", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MlrTargets_OrganizationId_DimensionId",
                table: "MlrTargets",
                columns: new[] { "OrganizationId", "DimensionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_ContractId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "ContractId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_EffectiveMonth_ReportMonth_General~",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "EffectiveMonth", "ReportMonth", "GeneralLedgerMonth", "PayerId", "MedicalCenterId", "ProviderId", "ContractId", "PlanId", "LineOfBusinessId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_LineOfBusinessId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "LineOfBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_MedicalCenterId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "MedicalCenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_PayerId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "PayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_PlanId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_ProviderId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_RegionId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_OrganizationId_SourceBatchId",
                table: "Snapshots",
                columns: new[] { "OrganizationId", "SourceBatchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Facts_Budget");

            migrationBuilder.DropTable(
                name: "Facts_MedicalExpenses");

            migrationBuilder.DropTable(
                name: "Facts_Membership");

            migrationBuilder.DropTable(
                name: "Facts_Revenue");

            migrationBuilder.DropTable(
                name: "InstallationStates");

            migrationBuilder.DropTable(
                name: "MlrComponents");

            migrationBuilder.DropTable(
                name: "MlrTargets");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "BudgetSnapshots");

            migrationBuilder.DropTable(
                name: "Snapshots");

            migrationBuilder.DropTable(
                name: "MlrDefinitions");

            migrationBuilder.DropTable(
                name: "BudgetPlans");

            migrationBuilder.DropTable(
                name: "Dimensions");

            migrationBuilder.DropTable(
                name: "SourceBatches");

            migrationBuilder.DropTable(
                name: "Organizations");
        }
    }
}
