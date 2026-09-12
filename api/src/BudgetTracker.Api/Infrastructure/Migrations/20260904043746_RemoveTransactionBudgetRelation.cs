using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionBudgetRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ Ręcznie poprawione względem tego, co wygenerował EF.
            //
            // Domyślny scaffold robi DropColumn("BudgetId") PRZED dodaniem
            // "BudgetBusinessId" — to gubi bezpowrotnie przypisanie budżetu na każdej
            // istniejącej transakcji (na tej bazie: ponad 1300 wierszy). Kolejność musi
            // być: dodaj nową kolumnę → przepisz wartości ze starej, dopóki jeszcze
            // istnieje → dopiero wtedy zdejmij FK, indeks i starą kolumnę.
            migrationBuilder.AddColumn<Guid>(
                name: "BudgetBusinessId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Transactions" t
                SET "BudgetBusinessId" = b."BusinessId"
                FROM "Budgets" b
                WHERE t."BudgetId" = b."Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BudgetId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetBusinessId",
                table: "Transactions",
                column: "BudgetBusinessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Symetrycznie do Up: nowa kolumna (tu: stara `BudgetId`) istnieje i jest
            // wypełniona, ZANIM znika `BudgetBusinessId`, którym się ją przepisuje.
            migrationBuilder.AddColumn<int>(
                name: "BudgetId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Transactions" t
                SET "BudgetId" = b."Id"
                FROM "Budgets" b
                WHERE t."BudgetBusinessId" = b."BusinessId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BudgetBusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BudgetBusinessId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BudgetId",
                table: "Transactions",
                column: "BudgetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
