using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Powiązanie wersji modelu ze zgłoszeniem treningu, które ją wyprodukowało.</summary>
    /// <remarks>
    /// Ekran kolejkuje trening i dostaje numer zadania; po nim rozpoznaje SWÓJ wynik. Bez tej kolumny
    /// musiałby zgadywać po najnowszej aktywnej wersji — a ta bywa efektem przywrócenia starszego modelu
    /// przez kogoś innego albo w innej karcie.
    ///
    /// Kolumna jest nullowalna, bo trening z CLI idzie z pominięciem kolejki i nie ma numeru zgłoszenia.
    /// </remarks>
    public partial class ModelVersionJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobId",
                table: "ModelVersions",
                type: "text",
                nullable: true);

            // Ekran odpytuje po tej kolumnie w pętli, dopóki trening trwa — bez indeksu kazde odpytanie
            // to skan calej historii wersji. Czesciowy, bo wiersze z CLI nie maja zgloszenia.
            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_ModelVersions_JobId" ON "ModelVersions" ("JobId") WHERE "JobId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_ModelVersions_JobId";""");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "ModelVersions");
        }
    }
}
