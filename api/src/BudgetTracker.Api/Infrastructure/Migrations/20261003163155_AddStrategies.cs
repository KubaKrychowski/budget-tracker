using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Tabela <c>Strategies</c> — graf kafelków kreatora strategii (zdarzenia, akcje, warunki) w jednym budżecie.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wyłącznie NOWA tabela: nie rusza istniejących danych ani kolumn. Węzły i połączenia siedzą w kolumnach <c>jsonb</c>
    /// (<c>Nodes</c>, <c>Edges</c>), bo nie mają własnego cyklu życia — kasują się i przywracają razem ze strategią.
    /// </para>
    /// <para>
    /// ⚠️ Izolacja użytkowników jak przy pozostałych encjach budżetu (<c>AddChildUserIdAndRls</c>): RLS z polityką
    /// <c>owner_isolation</c> na <c>UserId</c> i <c>FORCE</c>, żeby ominąć ją mogła tylko rola z <c>BYPASSRLS</c>.
    /// Zawartość strategii (salda, kredyt) jest daną osobową, więc bez RLS wyciekłaby między kontami.
    /// </para>
    /// </remarks>
    public partial class AddStrategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    StartCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HorizonMonths = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Edges = table.Column<string>(type: "jsonb", nullable: false),
                    Nodes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                    table.CheckConstraint("CK_Strategies_HorizonMonths", "\"HorizonMonths\" BETWEEN 6 AND 60");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_BudgetBusinessId",
                table: "Strategies",
                column: "BudgetBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_BusinessId",
                table: "Strategies",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_UserId",
                table: "Strategies",
                column: "UserId");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Strategies" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Strategies" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "Strategies"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS owner_isolation ON "Strategies";
                """);

            migrationBuilder.DropTable(
                name: "Strategies");
        }
    }
}
