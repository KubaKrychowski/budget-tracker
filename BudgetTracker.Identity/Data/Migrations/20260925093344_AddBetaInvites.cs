using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Identity.Data.Migrations
{
    /// <summary>
    /// Tabela adresów zaproszonych do zamkniętej bety (<c>BetaInvite</c>): lista, z której rejestracja sprawdza,
    /// czy wolno założyć konto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lista jest w bazie, a nie w konfiguracji jak <c>Admin:Emails</c>, bo zaprasza się w trakcie działania
    /// serwera — dopisanie adresu z panelu nie może wymagać restartu.
    /// </para>
    /// <para>
    /// ⚠️ Unikalny indeks na <c>Email</c> jest częścią reguły, nie optymalizacją: dwa równoległe dopisania tego
    /// samego adresu przeszłyby oba przez sprawdzenie w serwisie, a duplikat robi wpis, który trzeba usunąć dwa
    /// razy, żeby faktycznie przestał wpuszczać. Kolumna trzyma adres PO normalizacji (małe litery, przycięty).
    /// </para>
    /// <para>
    /// ⚠️ Tabela zawiera adresy e-mail osób, które nie mają jeszcze konta — to dane osobowe. Po zakończeniu bety
    /// ma zostać skasowana, a nie „zostawiona na wszelki wypadek".
    /// </para>
    /// </remarks>
    public partial class AddBetaInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BetaInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetaInvites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BetaInvites_Email",
                table: "BetaInvites",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetaInvites");
        }
    }
}
