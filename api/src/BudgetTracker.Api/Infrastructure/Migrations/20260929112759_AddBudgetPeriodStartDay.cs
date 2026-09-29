using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Dzień początku okresu rozliczeniowego budżetu (1–28).</summary>
    /// <remarks>
    /// Istniejące budżety dostają 1, czyli miesiąc kalendarzowy — zachowanie bez zmian, żadne dane się nie przesuwają.
    /// Check constraint pilnuje zakresu także poza API (dni 29–31 nie istnieją w każdym miesiącu).
    /// </remarks>
    public partial class AddBudgetPeriodStartDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodStartDay",
                table: "Budgets",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budgets_PeriodStartDay",
                table: "Budgets",
                sql: "\"PeriodStartDay\" BETWEEN 1 AND 28");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Budgets_PeriodStartDay",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "PeriodStartDay",
                table: "Budgets");
        }
    }
}
