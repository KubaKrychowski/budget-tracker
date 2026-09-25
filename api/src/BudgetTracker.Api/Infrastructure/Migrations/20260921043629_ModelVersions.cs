using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Historia wersji modelu kategoryzacji — po jednym wierszu na opublikowany trening.</summary>
    /// <remarks>
    /// <para>
    /// Bajty modelu leżą w blobie użytkownika; tu jest katalog: która wersja jest aktywna, na czym się
    /// uczyła i jaki ma <c>ETag</c>. Dzięki temu ekran „Dane treningowe" nie musi dotykać storage.
    /// </para>
    /// <para>
    /// ⚠️ RLS jak przy <c>CategoryRules</c>, ale BEZ gałęzi o wierszach wspólnych: wersja modelu zawsze
    /// należy do jednego użytkownika. Sam filtr EF nie wystarcza — przepuszcza wszystko, gdy w sesji
    /// nie ma użytkownika.
    /// </para>
    /// <para>
    /// ⚠️ Rola zadań w tle OMIJA RLS (tak samo jak przy regułach), więc trening uruchamiany z Hangfire
    /// musi zawężać zapytania po <c>UserId</c> sam — identyfikator jedzie w argumencie zadania.
    /// </para>
    /// </remarks>
    public partial class ModelVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TrainingSet = table.Column<int>(type: "integer", nullable: false),
                    CategoriesCount = table.Column<int>(type: "integer", nullable: false),
                    Accuracy = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageAccuracy = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ETag = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_BusinessId",
                table: "ModelVersions",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_UserId",
                table: "ModelVersions",
                column: "UserId");

            migrationBuilder.Sql(
                """
                ALTER TABLE "ModelVersions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ModelVersions" FORCE ROW LEVEL SECURITY;

                CREATE POLICY model_versions_read ON "ModelVersions" FOR SELECT
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY model_versions_insert ON "ModelVersions" FOR INSERT
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY model_versions_update ON "ModelVersions" FOR UPDATE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid)
                    WITH CHECK ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);

                CREATE POLICY model_versions_delete ON "ModelVersions" FOR DELETE
                    USING ("UserId" = NULLIF(current_setting('app.current_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS model_versions_delete ON "ModelVersions";
                DROP POLICY IF EXISTS model_versions_update ON "ModelVersions";
                DROP POLICY IF EXISTS model_versions_insert ON "ModelVersions";
                DROP POLICY IF EXISTS model_versions_read ON "ModelVersions";
                ALTER TABLE "ModelVersions" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "ModelVersions" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "ModelVersions");
        }
    }
}
