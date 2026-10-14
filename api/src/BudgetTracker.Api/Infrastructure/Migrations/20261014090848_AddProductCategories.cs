using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Kategorie produktów z paragonów (<c>ProductCategories</c>) i reguły ich przypisywania po nazwie (<c>ProductRules</c>) z izolacją
    /// użytkowników na poziomie bazy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tylko dodaje dwie tabele, indeksy i polityki — nie dotyka istniejących danych. Kategoria produktu pozycji paragonu
    /// (<c>ProductCategoryId</c>, <c>CategoryConfirmed</c>) siedzi w kolumnie <c>jsonb</c> <c>Receipts.Items</c> i NIE wymaga zmiany schematu:
    /// istniejące pozycje bez tych pól materializują się jako „bez kategorii”, niepotwierdzone.
    /// </para>
    /// <para>
    /// ⚠️ Polityki są cztery na tabelę, jak przy <c>CustomCategories</c>: odczyt obejmuje własne ORAZ wspólne wiersze (<c>UserId</c> = pusty Guid),
    /// a zapis tylko własne. Kategorie i reguły bazowe zakłada <c>ProductBaselineSeed</c> jako <c>budget_jobs</c> (<c>BYPASSRLS</c>).
    /// Rola <c>budget</c> (superuser) omija RLS zawsze — lokalnie chroni wyłącznie filtr Owner w EF.
    /// </para>
    /// </remarks>
    public partial class AddProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRules_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_BusinessId",
                table: "ProductCategories",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_UserId_Name",
                table: "ProductCategories",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRules_BusinessId",
                table: "ProductRules",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRules_Priority",
                table: "ProductRules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRules_ProductCategoryId",
                table: "ProductRules",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRules_UserId",
                table: "ProductRules",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRules_UserId_Pattern",
                table: "ProductRules",
                columns: new[] { "UserId", "Pattern" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.Sql(
                """
                ALTER TABLE "ProductCategories" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ProductCategories" FORCE ROW LEVEL SECURITY;

                CREATE POLICY product_categories_read ON "ProductCategories" FOR SELECT
                    USING ("UserId" = '00000000-0000-0000-0000-000000000000'::uuid
                        OR "UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_categories_insert ON "ProductCategories" FOR INSERT
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_categories_update ON "ProductCategories" FOR UPDATE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_categories_delete ON "ProductCategories" FOR DELETE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "ProductRules" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ProductRules" FORCE ROW LEVEL SECURITY;

                CREATE POLICY product_rules_read ON "ProductRules" FOR SELECT
                    USING ("UserId" = '00000000-0000-0000-0000-000000000000'::uuid
                        OR "UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_rules_insert ON "ProductRules" FOR INSERT
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_rules_update ON "ProductRules" FOR UPDATE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY product_rules_delete ON "ProductRules" FOR DELETE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY product_rules_delete ON "ProductRules";
                DROP POLICY product_rules_update ON "ProductRules";
                DROP POLICY product_rules_insert ON "ProductRules";
                DROP POLICY product_rules_read ON "ProductRules";
                ALTER TABLE "ProductRules" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "ProductRules" DISABLE ROW LEVEL SECURITY;

                DROP POLICY product_categories_delete ON "ProductCategories";
                DROP POLICY product_categories_update ON "ProductCategories";
                DROP POLICY product_categories_insert ON "ProductCategories";
                DROP POLICY product_categories_read ON "ProductCategories";
                ALTER TABLE "ProductCategories" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "ProductCategories" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "ProductRules");

            migrationBuilder.DropTable(
                name: "ProductCategories"); 
        }
    }
}
