using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hhh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MlrDefinitionsAndTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "AuditEvents",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MlrDefinitions",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                name: "MlrTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DimensionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Target = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MlrComponents");

            migrationBuilder.DropTable(
                name: "MlrTargets");

            migrationBuilder.DropTable(
                name: "MlrDefinitions");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "AuditEvents");
        }
    }
}
