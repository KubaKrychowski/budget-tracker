using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BusinessIdOnAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Categories_CategoryId",
                table: "BudgetItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryRules_Categories_CategoryId",
                table: "CategoryRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_ImportBatches_ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Transactions",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "ImportBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ImportBatches",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "CategoryRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "CategoryRules",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Categories",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Budgets",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "BudgetItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BudgetItems",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Accounts",
                type: "timestamptz",
                nullable: true);


            // ── Backfill istniejących wierszy ────────────────────────────────────────────
            // Kolumna powstała wyżej jako nullowalna, bo jedna wartość domyślna dla wszystkich
            // wierszy zderzyłaby się z unikalnym indeksem zakładanym niżej. Tu każdy istniejący
            // wiersz dostaje własny identyfikator, a dopiero potem kolumna staje się NOT NULL.
            //
            // `gen_random_uuid()` jest w Postgresie wbudowane od 13 — bez rozszerzenia pgcrypto.
            // Istniejące wiersze dostaną UUID v4 zamiast v7; to bez znaczenia, bo v7 wybraliśmy
            // dla KOLEJNOŚCI WSTAWIANIA nowych wierszy, a te już są w tabeli.

            // Transactions
            migrationBuilder.Sql(
                "UPDATE \"Transactions\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "Transactions",
                type: "uuid",
                nullable: false);

            // Categories
            migrationBuilder.Sql(
                "UPDATE \"Categories\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "Categories",
                type: "uuid",
                nullable: false);

            // Accounts
            migrationBuilder.Sql(
                "UPDATE \"Accounts\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "Accounts",
                type: "uuid",
                nullable: false);

            // Budgets
            migrationBuilder.Sql(
                "UPDATE \"Budgets\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "Budgets",
                type: "uuid",
                nullable: false);

            // BudgetItems
            migrationBuilder.Sql(
                "UPDATE \"BudgetItems\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "BudgetItems",
                type: "uuid",
                nullable: false);

            // CategoryRules
            migrationBuilder.Sql(
                "UPDATE \"CategoryRules\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "CategoryRules",
                type: "uuid",
                nullable: false);

            // ImportBatches
            migrationBuilder.Sql(
                "UPDATE \"ImportBatches\" SET \"BusinessId\" = gen_random_uuid() WHERE \"BusinessId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "BusinessId",
                table: "ImportBatches",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BusinessId",
                table: "Transactions",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_BusinessId",
                table: "ImportBatches",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_BusinessId",
                table: "CategoryRules",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BusinessId",
                table: "Categories",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BusinessId",
                table: "Budgets",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems",
                columns: new[] { "BudgetId", "CategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BusinessId",
                table: "BudgetItems",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_BusinessId",
                table: "Accounts",
                column: "BusinessId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Categories_CategoryId",
                table: "BudgetItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryRules_Categories_CategoryId",
                table: "CategoryRules",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_ImportBatches_ImportBatchId",
                table: "Transactions",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Categories_CategoryId",
                table: "BudgetItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryRules_Categories_CategoryId",
                table: "CategoryRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_ImportBatches_ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BusinessId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_BusinessId",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_CategoryRules_BusinessId",
                table: "CategoryRules");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BusinessId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_BusinessId",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BusinessId",
                table: "BudgetItems");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_BusinessId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CategoryRules");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Accounts");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems",
                columns: new[] { "BudgetId", "CategoryId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Categories_CategoryId",
                table: "BudgetItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryRules_Categories_CategoryId",
                table: "CategoryRules",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Budgets_BudgetId",
                table: "ImportBatches",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_AccountId",
                table: "Transactions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Budgets_BudgetId",
                table: "Transactions",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Categories_CategoryId",
                table: "Transactions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_ImportBatches_ImportBatchId",
                table: "Transactions",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
