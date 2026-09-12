using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportBudgetTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Accounts_AccountId",
                table: "ImportBatches");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "ImportBatches",
                newName: "BudgetId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_AccountId",
                table: "ImportBatches",
                newName: "IX_ImportBatches_BudgetId");

            migrationBuilder.AddColumn<int>(
                name: "BudgetId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "ImportBatches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetId",
                table: "Transactions",
                column: "BudgetId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BudgetId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Bank",
                table: "ImportBatches");

            migrationBuilder.RenameColumn(
                name: "BudgetId",
                table: "ImportBatches",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_BudgetId",
                table: "ImportBatches",
                newName: "IX_ImportBatches_AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Accounts_AccountId",
                table: "ImportBatches",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
