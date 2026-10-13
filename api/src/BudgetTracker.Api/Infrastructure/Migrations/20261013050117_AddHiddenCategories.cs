using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Tabela kategorii wspólnych ukrytych przez konto (zgłoszenie #30) z izolacją użytkowników na poziomie bazy.</summary>
    /// <remarks>
    /// Tylko dodaje tabelę, indeks i politykę — nie dotyka istniejących danych; nikt jeszcze niczego nie ukrył, więc po migracji
    /// wszystkie kategorie są widoczne jak dotąd. ⚠️ Izolacja jak przy strategiach i paragonach: RLS z polityką <c>owner_isolation</c>
    /// na <c>UserId</c> i <c>FORCE</c> — bez niej jedno konto widziałoby, co ukryło inne. Indeks unikalny jest częściowy
    /// (<c>DeletedAt IS NULL</c>), żeby skasowane logicznie ukrycie nie blokowało ponownego ukrycia tej samej kategorii.
    /// </remarks>
    public partial class AddHiddenCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiddenCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiddenCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HiddenCategories_BusinessId",
                table: "HiddenCategories",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HiddenCategories_UserId_CategoryId",
                table: "HiddenCategories",
                columns: new[] { "UserId", "CategoryId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.Sql(
                """
                ALTER TABLE "HiddenCategories" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "HiddenCategories" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "HiddenCategories"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS owner_isolation ON "HiddenCategories";
                """);

            migrationBuilder.DropTable(
                name: "HiddenCategories");
        }
    }
}
