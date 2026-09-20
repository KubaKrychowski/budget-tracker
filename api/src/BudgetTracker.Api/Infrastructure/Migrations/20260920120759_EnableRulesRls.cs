using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Reguły kategoryzacji należą do konta: zmienia nazwę kolumny <c>UserBusinessId</c> (z <c>AddUserIdToRule</c>) na
    /// <c>UserId</c>, jak w pozostałych tabelach, dokłada indeks i włącza row-level security na <c>CategoryRules</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Istniejące wiersze zostają bez zmian: mają <c>UserId</c> = pusty Guid (wartość domyślna z <c>AddUserIdToRule</c>),
    /// czyli stają się regułami WSPÓLNYMI — widzą je wszyscy, nikt ich nie zmieni ani nie skasuje. To dokładnie reguły
    /// bazowe z <c>BaselineSeed</c> i wczytane z pliku lokalnego. Reguły, które ktoś dodał ręcznie przed tą migracją, też
    /// staną się wspólne i tylko do odczytu; żeby były czyjeś, trzeba ręcznie ustawić <c>UserId</c> w bazie.
    /// </para>
    /// <para>
    /// ⚠️ Polityki są cztery, nie jedna jak przy <c>Budgets</c>: odczyt obejmuje własne ORAZ wspólne wiersze, a zapis
    /// (<c>INSERT</c>, <c>UPDATE</c>, <c>DELETE</c>) tylko własne. Jedna polityka „<c>UserId</c> = użytkownik z sesji” ukryłaby
    /// reguły bazowe przed wszystkimi, a polityka „własne lub wspólne” pozwoliłaby każdemu edytować cudze reguły bazowe.
    /// Reguły bazowe zakłada seed jako <c>budget_jobs</c> (<c>BYPASSRLS</c>), zob. <c>Program.cs</c>.
    /// </para>
    /// <para>
    /// RLS dotyczy roli <c>budget_app</c>. Rola <c>budget</c> (superuser, właściciel tabel) omija je zawsze, więc lokalnie
    /// przy connection stringu z <c>Username=budget</c> chroni wyłącznie filtr Owner w EF.
    /// </para>
    /// </remarks>
    public partial class EnableRulesRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserBusinessId",
                table: "CategoryRules",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_UserId",
                table: "CategoryRules",
                column: "UserId");

            migrationBuilder.Sql(
                """
                ALTER TABLE "CategoryRules" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "CategoryRules" FORCE ROW LEVEL SECURITY;

                CREATE POLICY rules_read ON "CategoryRules" FOR SELECT
                    USING ("UserId" = '00000000-0000-0000-0000-000000000000'::uuid
                        OR "UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY rules_insert ON "CategoryRules" FOR INSERT
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY rules_update ON "CategoryRules" FOR UPDATE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY rules_delete ON "CategoryRules" FOR DELETE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY rules_delete ON "CategoryRules";
                DROP POLICY rules_update ON "CategoryRules";
                DROP POLICY rules_insert ON "CategoryRules";
                DROP POLICY rules_read ON "CategoryRules";
                ALTER TABLE "CategoryRules" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "CategoryRules" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CategoryRules_UserId",
                table: "CategoryRules");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "CategoryRules",
                newName: "UserBusinessId");
        }
    }
}
