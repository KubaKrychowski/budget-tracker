using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Identity.Data.Migrations
{
    /// <summary>
    /// Tabela próśb o dostęp do bety zostawionych na landingu (<c>BetaAccessRequest</c>). Nowa tabela — istniejących
    /// danych nie rusza.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Unikalny indeks na <c>Email</c> jest częścią reguły: dwa równoległe zgłoszenia tego samego adresu przeszłyby
    /// oba przez sprawdzenie w serwisie. Kolumna trzyma adres PO normalizacji — tę samą postać co <c>BetaInvites</c>.
    /// </para>
    /// <para>
    /// ⚠️ Tabela zawiera adresy e-mail osób bez konta, podane za zgodą — dane osobowe. <c>ConsentVersion</c> to wersja
    /// Regulaminu i Polityki prywatności zaakceptowana przy zgłoszeniu; bez niej zgody nie da się udowodnić.
    /// </para>
    /// </remarks>
    public partial class AddBetaAccessRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BetaAccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetaAccessRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BetaAccessRequests_Email",
                table: "BetaAccessRequests",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetaAccessRequests");
        }
    }
}
