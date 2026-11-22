using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hhh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyntheaClinicalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyntheaDatasets",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyntheaDatasets", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_SyntheaDatasets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyntheaAdmissions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DischargedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EncounterCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyntheaAdmissions", x => new { x.OrganizationId, x.EncounterId });
                    table.ForeignKey(
                        name: "FK_SyntheaAdmissions_SyntheaDatasets_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "SyntheaDatasets",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyntheaRecords",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyntheaRecords", x => new { x.OrganizationId, x.SourceTable, x.RowNumber });
                    table.ForeignKey(
                        name: "FK_SyntheaRecords_SyntheaDatasets_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "SyntheaDatasets",
                        principalColumn: "OrganizationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyntheaAdmissions_OrganizationId_AdmittedAt_EncounterId",
                table: "SyntheaAdmissions",
                columns: new[] { "OrganizationId", "AdmittedAt", "EncounterId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyntheaRecords_OrganizationId_EncounterId_SourceTable",
                table: "SyntheaRecords",
                columns: new[] { "OrganizationId", "EncounterId", "SourceTable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyntheaAdmissions");

            migrationBuilder.DropTable(
                name: "SyntheaRecords");

            migrationBuilder.DropTable(
                name: "SyntheaDatasets");
        }
    }
}
