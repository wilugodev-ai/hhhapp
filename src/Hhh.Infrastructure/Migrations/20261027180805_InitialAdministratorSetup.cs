using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hhh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdministratorSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstallationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AdministratorSetupCompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationStates", x => x.Id);
                    table.CheckConstraint("CK_InstallationState_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                table: "InstallationStates",
                columns: new[] { "Id", "AdministratorSetupCompletedAt" },
                values: new object[] { 1, null });

            // Existing installations remain closed even if administrator roles change later.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [AspNetUsers] AS u
                    INNER JOIN [AspNetUserRoles] AS ur ON ur.[UserId] = u.[Id]
                    INNER JOIN [AspNetRoles] AS r ON r.[Id] = ur.[RoleId]
                    WHERE r.[Name] = N'System Administrator'
                        AND (u.[PasswordHash] IS NOT NULL OR u.[InvitationPending] = 0)
                )
                    UPDATE [InstallationStates]
                    SET [AdministratorSetupCompletedAt] = SYSUTCDATETIME() WHERE [Id] = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallationStates");
        }
    }
}
