using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Słownik walut z kluczem-kodem oraz przepięcie limitów budżetu z <c>BudgetId</c> (int) na <c>BudgetBusinessId</c> (uuid).
    /// </summary>
    /// <remarks>
    /// ⚠️ Poprawiona ręcznie. Wygenerowana wersja kasowała <c>BudgetId</c> PRZED dodaniem nowej kolumny z pustym Guidem
    /// jako wartością domyślną — limity traciły powiązanie z budżetem, a przy dwóch budżetach z limitami na tych samych
    /// kategoriach migracja wywracała się na unikalnym indeksie. Tu nowa kolumna jest wypełniana joinem po starym kluczu,
    /// zanim stary zniknie; <c>Down</c> robi to samo w drugą stronę.
    /// </remarks>
    public partial class BudgetCurrencyDictionaryAndItemBusinessId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "Currencies",
                column: "Code",
                value: "PLN");

            migrationBuilder.Sql(
                "INSERT INTO \"Currencies\" (\"Code\") SELECT DISTINCT \"Currency\" FROM \"Budgets\" " +
                "ON CONFLICT (\"Code\") DO NOTHING;");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_Currency",
                table: "Budgets",
                column: "Currency");

            migrationBuilder.AddForeignKey(
                name: "FK_Budgets_Currencies_Currency",
                table: "Budgets",
                column: "Currency",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetBusinessId",
                table: "BudgetItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"BudgetItems\" i SET \"BudgetBusinessId\" = b.\"BusinessId\" " +
                "FROM \"Budgets\" b WHERE b.\"Id\" = i.\"BudgetId\";");

            migrationBuilder.AlterColumn<Guid>(
                name: "BudgetBusinessId",
                table: "BudgetItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "BudgetId",
                table: "BudgetItems");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId",
                table: "BudgetItems",
                columns: new[] { "BudgetBusinessId", "CategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BudgetId",
                table: "BudgetItems",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"BudgetItems\" i SET \"BudgetId\" = b.\"Id\" " +
                "FROM \"Budgets\" b WHERE b.\"BusinessId\" = i.\"BudgetBusinessId\";");

            migrationBuilder.AlterColumn<int>(
                name: "BudgetId",
                table: "BudgetItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_BudgetBusinessId_CategoryId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "BudgetBusinessId",
                table: "BudgetItems");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_BudgetId_CategoryId",
                table: "BudgetItems",
                columns: new[] { "BudgetId", "CategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetItems_Budgets_BudgetId",
                table: "BudgetItems",
                column: "BudgetId",
                principalTable: "Budgets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropForeignKey(
                name: "FK_Budgets_Currencies_Currency",
                table: "Budgets");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_Currency",
                table: "Budgets");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
