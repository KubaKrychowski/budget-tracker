using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Dokłada właściciela budżetu (issue #22, przygotowanie pod model multi-user).</summary>
    /// <remarks>
    /// ⚠️ Istniejące wiersze dostają <see cref="Guid.Empty"/> jako <c>UserId</c> (domyślna wartość
    /// kolumny) — filtr właściciela w <c>AppDbContext</c> pokazuje je tylko poza kontekstem żądania
    /// HTTP (testy, seedy, Hangfire), NIE żadnemu zalogowanemu użytkownikowi. Na bazie z prawdziwymi
    /// danymi (<c>budgettracker</c>) te budżety staną się niewidoczne po zalogowaniu, dopóki ktoś
    /// ręcznie nie przepisze <c>UserId</c> na identyfikator realnego konta z BudgetTracker.Identity —
    /// to świadomie NIE jest zrobione w tej migracji, bo wymaga decyzji, czyje to konto.
    /// </remarks>
    public partial class AddBudgetOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Budgets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId",
                table: "Budgets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Budgets");
        }
    }
}
