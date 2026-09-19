using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Dokłada <c>UserId</c> do dzieci budżetu i włącza row-level security (RLS) w Postgresie na
    /// <c>Budgets</c> i wszystkich siedmiu tabelach dzieci — druga warstwa obrony pod tym samym
    /// filtrem, który dotąd egzekwował wyłącznie EF Core (<c>AppDbContext.HasQueryFilter("Owner", ...)</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>RLS nie chroni sam z siebie</b> — appka łączy się jedną wspólną rolą (<c>budget_app</c>,
    /// patrz REWIZJA w <c>api/db/setup-rls-roles.sql</c>) dla wszystkich użytkowników, więc polityki
    /// porównują <c>UserId</c> ze zmienną sesyjną <c>app.current_user_id</c>, ustawianą per połączenie
    /// przez <see cref="RlsSessionInterceptor"/>. Samo <c>ENABLE ROW LEVEL SECURITY</c> NIE wystarcza:
    /// właściciel tabeli (<c>budget</c>, rola, która uruchomiła migracje) domyślnie omija RLS —
    /// superuser zresztą ZAWSZE by je ominął, niezależnie od <c>FORCE</c> — dlatego appka od tej
    /// zmiany łączy się <c>budget_app</c> (zwykłą rolą), nie <c>budget</c>.
    /// </para>
    /// <para>
    /// <c>NULLIF(current_setting(...), '')::uuid</c> zamiast gołego rzutowania: pusty string
    /// (ustawiany przez interceptor poza kontekstem żądania HTTP — testy, seedy) rzutowany wprost
    /// na <c>uuid</c> rzuciłby błędem SQL zamiast po cichu nie dopasować żadnego wiersza.
    /// </para>
    /// <para>
    /// Backfill z <see cref="Domain.Budget.UserId"/> po kluczu, którym dana tabela łączy się z budżetem
    /// (<c>BudgetBusinessId</c> wszędzie poza <c>ImportBatches</c>, tam <c>BudgetId</c> — patrz
    /// <see cref="Domain.ImportBatch"/>). Wiersze bez dopasowania (np. <c>Transactions</c> z
    /// <c>BudgetBusinessId IS NULL</c> — transakcje dodane ręcznie sprzed tego pola) dostają
    /// <see cref="Guid.Empty"/>, tak samo jak budżety w <c>AddBudgetOwner</c>: na
    /// <c>budgettracker</c> te wiersze staną się niewidoczne dla każdego zalogowanego użytkownika,
    /// dopóki ktoś ręcznie nie przepisze <c>UserId</c> — to świadomie NIE jest zrobione tutaj.
    /// </para>
    /// <para>
    /// Role <c>budget_app</c> (appka na co dzień) i <c>budget_jobs</c> (BYPASSRLS, do zadań Hangfire
    /// działających bez kontekstu użytkownika — patrz <see cref="Features.Budgets.Services.BudgetPurger"/>)
    /// zakłada OSOBNY skrypt spoza migracji EF (<c>api/db/setup-rls-roles.sql</c> +
    /// <c>api/db/grant-app-privileges.sql</c>), bo role są własnością całego klastra Postgresa, nie jednej
    /// bazy — uruchomienie go migracją powtórzoną na drugiej bazie w tym samym klastrze wywaliłoby się
    /// błędem „rola już istnieje".
    /// </para>
    /// </remarks>
    public partial class AddChildUserIdAndRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "StandingOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SavingsReservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SavingsGoals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ImportBatches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "EpisodicOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "BudgetItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_UserId",
                table: "StandingOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsReservations_UserId",
                table: "SavingsReservations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_UserId",
                table: "SavingsGoals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_UserId",
                table: "ImportBatches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EpisodicOrders_UserId",
                table: "EpisodicOrders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetItems_UserId",
                table: "BudgetItems",
                column: "UserId");

            migrationBuilder.Sql(
                """
                UPDATE "Transactions" t SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE t."BudgetBusinessId" = b."BusinessId";
                UPDATE "StandingOrders" o SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE o."BudgetBusinessId" = b."BusinessId";
                UPDATE "SavingsReservations" r SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE r."BudgetBusinessId" = b."BusinessId";
                UPDATE "SavingsGoals" g SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE g."BudgetBusinessId" = b."BusinessId";
                UPDATE "ImportBatches" i SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE i."BudgetId" = b."Id";
                UPDATE "EpisodicOrders" o SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE o."BudgetBusinessId" = b."BusinessId";
                UPDATE "BudgetItems" bi SET "UserId" = b."UserId"
                    FROM "Budgets" b WHERE bi."BudgetBusinessId" = b."BusinessId";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Budgets" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Budgets" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "Budgets"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "Transactions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Transactions" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "Transactions"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "StandingOrders" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "StandingOrders" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "StandingOrders"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "SavingsReservations" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "SavingsReservations" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "SavingsReservations"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "SavingsGoals" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "SavingsGoals" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "SavingsGoals"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "ImportBatches" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ImportBatches" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "ImportBatches"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "EpisodicOrders" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "EpisodicOrders" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "EpisodicOrders"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                ALTER TABLE "BudgetItems" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "BudgetItems" FORCE ROW LEVEL SECURITY;
                CREATE POLICY owner_isolation ON "BudgetItems"
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS owner_isolation ON "Budgets";
                ALTER TABLE "Budgets" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "Budgets" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "Transactions";
                ALTER TABLE "Transactions" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "Transactions" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "StandingOrders";
                ALTER TABLE "StandingOrders" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "StandingOrders" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "SavingsReservations";
                ALTER TABLE "SavingsReservations" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "SavingsReservations" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "SavingsGoals";
                ALTER TABLE "SavingsGoals" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "SavingsGoals" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "ImportBatches";
                ALTER TABLE "ImportBatches" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "ImportBatches" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "EpisodicOrders";
                ALTER TABLE "EpisodicOrders" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "EpisodicOrders" DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS owner_isolation ON "BudgetItems";
                ALTER TABLE "BudgetItems" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "BudgetItems" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_StandingOrders_UserId",
                table: "StandingOrders");

            migrationBuilder.DropIndex(
                name: "IX_SavingsReservations_UserId",
                table: "SavingsReservations");

            migrationBuilder.DropIndex(
                name: "IX_SavingsGoals_UserId",
                table: "SavingsGoals");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_UserId",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_EpisodicOrders_UserId",
                table: "EpisodicOrders");

            migrationBuilder.DropIndex(
                name: "IX_BudgetItems_UserId",
                table: "BudgetItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "StandingOrders");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SavingsReservations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SavingsGoals");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "EpisodicOrders");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BudgetItems");
        }
    }
}
