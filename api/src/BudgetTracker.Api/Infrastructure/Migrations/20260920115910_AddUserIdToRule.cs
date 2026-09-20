using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Dokłada właściciela do reguł kategoryzacji (<c>CategoryRules</c>): kolumna <c>UserBusinessId</c>, domyślnie pusty Guid.
    /// </summary>
    /// <remarks>
    /// Istniejące reguły dostają pusty Guid, czyli stają się regułami wspólnymi. Kolumna została przemianowana na <c>UserId</c>
    /// w <c>EnableRulesRls</c>, która włącza też row-level security — migracje idą razem.
    /// </remarks>
    public partial class AddUserIdToRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserBusinessId",
                table: "CategoryRules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserBusinessId",
                table: "CategoryRules");
        }
    }
}
