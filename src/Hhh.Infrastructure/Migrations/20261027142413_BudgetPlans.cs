using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hhh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsSynthetic = table.Column<bool>(type: "bit", nullable: false)
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
                name: "BudgetSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BudgetPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    PayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicalCenterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineOfBusinessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedMemberMonths = table.Column<int>(type: "int", nullable: false)
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
                name: "Facts_Budget",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudgetSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPlans_OrganizationId_Year_Version",
                table: "BudgetPlans",
                columns: new[] { "OrganizationId", "Year", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetSnapshots_BudgetPlanId_Month_PayerId_MedicalCenterId_ProviderId_ContractId_PlanId_LineOfBusinessId_RegionId",
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
                name: "IX_Facts_Budget_BudgetSnapshotId_Type_Category",
                table: "Facts_Budget",
                columns: new[] { "BudgetSnapshotId", "Type", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Facts_Budget");

            migrationBuilder.DropTable(
                name: "BudgetSnapshots");

            migrationBuilder.DropTable(
                name: "BudgetPlans");
        }
    }
}
