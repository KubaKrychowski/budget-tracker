using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetCreatedAtAndDisabledAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Ręcznie poprawione względem tego, co wygenerował EF.
            //
            // EF dał `defaultValue: 0001-01-01`, bo DateTimeOffset nie jest nullowalny. Kolumna
            // trafia na ekran ustawień jako „Data utworzenia", więc istniejące budżety pokazałyby
            // rok 1 — a to nie jest brak danych, to jest dana WYGLĄDAJĄCA na prawdziwą.
            // Backfill przez now(): dla wierszy sprzed migracji to data migracji, nie data
            // faktycznego utworzenia (tej nikt nie zapisał i nie da się jej odtworzyć).
            //
            // Domyślna wartość zostaje zdjęta zaraz po backfillu — datę nadaje kod, nie baza,
            // żeby nie było dwóch źródeł prawdy dla tego samego pola.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Budgets",
                type: "timestamptz",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.Sql(@"ALTER TABLE ""Budgets"" ALTER COLUMN ""CreatedAt"" DROP DEFAULT;");

            // null = aktywny. To NIE jest DeletedAt — patrz Budget.DisabledAt.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "Budgets",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "Budgets");
        }
    }
}
