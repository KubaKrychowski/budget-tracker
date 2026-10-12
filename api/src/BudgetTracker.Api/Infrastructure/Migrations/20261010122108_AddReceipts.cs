using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Tabela paragonów (załączniki do transakcji z odczytem OCR) z izolacją użytkowników na poziomie bazy.</summary>
    /// <remarks>
    /// Tylko dodaje tabelę, indeksy i politykę — nie dotyka istniejących danych. ⚠️ Izolacja jak przy strategiach
    /// (<c>AddStrategies</c>): RLS z polityką <c>owner_isolation</c> na <c>UserId</c> i <c>FORCE</c>, bo na paragonie są
    /// sprzedawca i kwota, a sam plik leży w kontenerze użytkownika.
    /// </remarks>
    public partial class AddReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionBusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    BlobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Merchant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_BusinessId",
                table: "Receipts",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TransactionBusinessId",
                table: "Receipts",
                column: "TransactionBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId",
                table: "Receipts",
                column: "UserId");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Receipts" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Receipts" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "Receipts"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS owner_isolation ON "Receipts";
                """);

            migrationBuilder.DropTable(
                name: "Receipts");
        }
    }
}
