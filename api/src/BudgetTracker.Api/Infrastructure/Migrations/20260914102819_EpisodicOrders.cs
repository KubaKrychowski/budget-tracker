using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EpisodicOrders : Migration
    {
        /// <summary>Tabela zleceń epizodycznych i przeniesienie na nie flagi „duży wydatek”.</summary>
        /// <remarks>
        /// Flaga → zrealizowane zlecenie (nazwa = tytuł transakcji), PRZED usunięciem kolumny, bo po nim nie ma już czego
        /// przenosić. Stempel usunięcia idzie z transakcji, żeby przywrócenie budżetu oddało też zlecenie. Poza zakresem:
        /// flagi na wpływach i na transakcjach bez budżetu — zlecenie wymaga budżetu i liczy się jako wydatek.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "EpisodicOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    PlannedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DueMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    TransactionBusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationBusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpisodicOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpisodicOrders_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpisodicOrders_BudgetBusinessId",
                table: "EpisodicOrders",
                column: "BudgetBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodicOrders_BusinessId",
                table: "EpisodicOrders",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EpisodicOrders_CategoryId",
                table: "EpisodicOrders",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodicOrders_TransactionBusinessId",
                table: "EpisodicOrders",
                column: "TransactionBusinessId",
                unique: true,
                filter: "\"TransactionBusinessId\" IS NOT NULL AND \"DeletedAt\" IS NULL");

            migrationBuilder.Sql("""
                INSERT INTO "EpisodicOrders" ("BusinessId", "BudgetBusinessId", "Name", "TransactionBusinessId", "CreatedAt", "DeletedAt")
                SELECT gen_random_uuid(), t."BudgetBusinessId", left(t."Description", 100), t."BusinessId", now(), t."DeletedAt"
                FROM "Transactions" t
                WHERE t."IsLargeExpense" AND t."Amount" < 0 AND t."BudgetBusinessId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "IsLargeExpense",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLargeExpense",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Transactions" t SET "IsLargeExpense" = true
                WHERE EXISTS (SELECT 1 FROM "EpisodicOrders" o WHERE o."TransactionBusinessId" = t."BusinessId");
                """);

            migrationBuilder.DropTable(
                name: "EpisodicOrders");
        }
    }
}
