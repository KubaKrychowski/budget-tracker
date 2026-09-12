using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Budgets",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Budgets");
        }
    }
}
