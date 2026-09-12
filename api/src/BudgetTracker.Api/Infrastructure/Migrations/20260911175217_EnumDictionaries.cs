using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Statusy transakcji, strony przepływu reguł i typy kont jako słowniki: kolumny przechodzą z liczby na kod.
    /// </summary>
    /// <remarks>
    /// Pisana ręcznie. Wygenerowane <c>AlterColumn</c> zamieniłoby liczbę na jej zapis tekstowy (<c>1</c> → <c>'1'</c>),
    /// którego nie ma w słowniku, więc klucz obcy odrzuciłby każdy istniejący wiersz. <c>CASE</c> bez <c>ELSE</c> daje
    /// <c>NULL</c> dla wartości spoza enuma, a <c>NOT NULL</c> na kolumnie przerywa wtedy migrację, zamiast po cichu
    /// zgubić dane. Domyślną wartość <c>Direction</c> trzeba zdjąć przed zmianą typu — <c>USING</c> jej nie konwertuje.
    /// </remarks>
    public partial class EnumDictionaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "RuleDirections",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleDirections", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "TransactionStatuses",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionStatuses", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                column: "Code",
                values: new object[]
                {
                    "Bank",
                    "Cash",
                    "LunchCard"
                });

            migrationBuilder.InsertData(
                table: "RuleDirections",
                column: "Code",
                values: new object[]
                {
                    "Any",
                    "Expense",
                    "Income"
                });

            migrationBuilder.InsertData(
                table: "TransactionStatuses",
                column: "Code",
                values: new object[]
                {
                    "AutoCategorized",
                    "Confirmed",
                    "Imported",
                    "ManuallyCategorized",
                    "PendingReview"
                });

            migrationBuilder.Sql("""
                ALTER TABLE "Transactions" ALTER COLUMN "Status" TYPE character varying(50) USING CASE "Status"
                    WHEN 0 THEN 'Imported'
                    WHEN 1 THEN 'AutoCategorized'
                    WHEN 2 THEN 'PendingReview'
                    WHEN 3 THEN 'ManuallyCategorized'
                    WHEN 4 THEN 'Confirmed'
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "CategoryRules" ALTER COLUMN "Direction" DROP DEFAULT;
                ALTER TABLE "CategoryRules" ALTER COLUMN "Direction" TYPE character varying(50) USING CASE "Direction"
                    WHEN 0 THEN 'Any'
                    WHEN 1 THEN 'Expense'
                    WHEN 2 THEN 'Income'
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" ALTER COLUMN "Type" TYPE character varying(50) USING CASE "Type"
                    WHEN 0 THEN 'Bank'
                    WHEN 1 THEN 'LunchCard'
                    WHEN 2 THEN 'Cash'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_Direction",
                table: "CategoryRules",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Type",
                table: "Accounts",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_AccountTypes_Type",
                table: "Accounts",
                column: "Type",
                principalTable: "AccountTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoryRules_RuleDirections_Direction",
                table: "CategoryRules",
                column: "Direction",
                principalTable: "RuleDirections",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_TransactionStatuses_Status",
                table: "Transactions",
                column: "Status",
                principalTable: "TransactionStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_AccountTypes_Type",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoryRules_RuleDirections_Direction",
                table: "CategoryRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_TransactionStatuses_Status",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_CategoryRules_Direction",
                table: "CategoryRules");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_Type",
                table: "Accounts");

            migrationBuilder.Sql("""
                ALTER TABLE "Transactions" ALTER COLUMN "Status" TYPE integer USING CASE "Status"
                    WHEN 'Imported' THEN 0
                    WHEN 'AutoCategorized' THEN 1
                    WHEN 'PendingReview' THEN 2
                    WHEN 'ManuallyCategorized' THEN 3
                    WHEN 'Confirmed' THEN 4
                END;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "CategoryRules" ALTER COLUMN "Direction" TYPE integer USING CASE "Direction"
                    WHEN 'Any' THEN 0
                    WHEN 'Expense' THEN 1
                    WHEN 'Income' THEN 2
                END;
                ALTER TABLE "CategoryRules" ALTER COLUMN "Direction" SET DEFAULT 0;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" ALTER COLUMN "Type" TYPE integer USING CASE "Type"
                    WHEN 'Bank' THEN 0
                    WHEN 'LunchCard' THEN 1
                    WHEN 'Cash' THEN 2
                END;
                """);

            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropTable(
                name: "RuleDirections");

            migrationBuilder.DropTable(
                name: "TransactionStatuses");
        }
    }
}
