using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetItemHistoryAndWarningThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId",
                table: "BudgetItems");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidFrom",
                table: "BudgetItems",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "BudgetItems",
                type: "date",
                nullable: true);

            // ⚠️ Poprawione ręcznie: wygenerowane `defaultValue: 0` dałoby istniejącym limitom próg 0%,
            // czyli ostrzeżenie od pierwszej złotówki. Istniejące limity dostają domyślne 80%.
            migrationBuilder.AddColumn<int>(
                name: "WarningThreshold",
                table: "BudgetItems",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            // ⚠️ Dopisane ręcznie: bez tego istniejące limity obowiązywałyby „od roku 1". Limit sprzed historii
            // dostaje miesiąc SWOJEGO budżetu — od wtedy budżet w ogóle istnieje.
            migrationBuilder.Sql("""
                UPDATE "BudgetItems" AS i
                SET "ValidFrom" = b."Month"
                FROM "Budgets" AS b
                WHERE b."BusinessId" = i."BudgetBusinessId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId_ValidFrom",
                table: "BudgetItems",
                columns: new[] { "BudgetBusinessId", "CategoryId", "ValidFrom" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId_ValidFrom",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "WarningThreshold",
                table: "BudgetItems");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId",
                table: "BudgetItems",
                columns: new[] { "BudgetBusinessId", "CategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
