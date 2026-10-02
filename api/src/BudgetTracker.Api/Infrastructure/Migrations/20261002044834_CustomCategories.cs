using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Własne kategorie kont: dokłada kategoriom typ (<c>Type</c>: wydatek albo wpływ) i właściciela (<c>UserId</c>),
    /// zastępuje globalnie unikalną nazwę nazwą unikalną u właściciela i włącza row-level security na <c>Categories</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Istniejące kategorie dostają <c>UserId</c> = pusty Guid, czyli stają się kategoriami WSPÓLNYMI — widzą je wszyscy,
    /// nikt ich nie zmieni ani nie skasuje. Typ dostaje <c>Expense</c>, a potem <c>Income</c> te kategorie, które mają
    /// co najmniej jedną żywą regułę i WSZYSTKIE ich reguły dotyczą wyłącznie wpływów (<c>Direction = 'Income'</c>) — ta sama
    /// heurystyka, którą dotąd stosowało <c>LimitCategories</c>. Kategoria bez reguł zostaje wydatkową. Jeśli któraś
    /// kategoria wspólna ma być przychodowa mimo to, trzeba ją poprawić ręcznie w bazie.
    /// </para>
    /// <para>
    /// ⚠️ Polityki są cztery, nie jedna: odczyt obejmuje własne ORAZ wspólne wiersze, a zapis (<c>INSERT</c>,
    /// <c>UPDATE</c>, <c>DELETE</c>) tylko własne — tak samo jak przy <c>CategoryRules</c> (<c>EnableRulesRls</c>).
    /// Kategorie bazowe zakłada seed jako <c>budget_jobs</c> (<c>BYPASSRLS</c>), zob. <c>Program.cs</c>.
    /// </para>
    /// <para>
    /// RLS dotyczy roli <c>budget_app</c>. Rola <c>budget</c> (superuser, właściciel tabel) omija je zawsze, więc lokalnie
    /// przy connection stringu z <c>Username=budget</c> chroni wyłącznie filtr Owner w EF.
    /// </para>
    /// </remarks>
    public partial class CustomCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryTypes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTypes", x => x.Code);
                });

            migrationBuilder.InsertData(
                table: "CategoryTypes",
                column: "Code",
                values: new object[]
                {
                    "Expense",
                    "Income"
                });

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Expense");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE "Categories" c SET "Type" = 'Income'
                WHERE EXISTS (SELECT 1 FROM "CategoryRules" r WHERE r."CategoryId" = c."Id" AND r."DeletedAt" IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM "CategoryRules" r
                                  WHERE r."CategoryId" = c."Id" AND r."DeletedAt" IS NULL AND r."Direction" <> 'Income');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Type",
                table: "Categories",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_CategoryTypes_Type",
                table: "Categories",
                column: "Type",
                principalTable: "CategoryTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Categories" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Categories" FORCE ROW LEVEL SECURITY;

                CREATE POLICY categories_read ON "Categories" FOR SELECT
                    USING ("UserId" = '00000000-0000-0000-0000-000000000000'::uuid
                        OR "UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY categories_insert ON "Categories" FOR INSERT
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY categories_update ON "Categories" FOR UPDATE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY categories_delete ON "Categories" FOR DELETE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY categories_delete ON "Categories";
                DROP POLICY categories_update ON "Categories";
                DROP POLICY categories_insert ON "Categories";
                DROP POLICY categories_read ON "Categories";
                ALTER TABLE "Categories" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "Categories" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_CategoryTypes_Type",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "CategoryTypes");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Type",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }
    }
}
