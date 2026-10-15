using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Fizycznie usuwa paragony skasowane do tej pory miękko (<c>DeletedAt IS NOT NULL</c>). Nie zmienia schematu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Do tej zmiany usunięcie paragonu kasowało plik z magazynu, ale wiersz zostawał w bazie ze stemplem <c>DeletedAt</c> — razem ze
    /// sprzedawcą, datą, sumą, nazwami kupionych produktów i oryginalną nazwą pliku, a żadne zadanie ich nie sprzątało. Od teraz
    /// <c>DeleteReceiptCommandHandler</c> kasuje wiersz od razu; ta migracja domyka stare przypadki.
    /// </para>
    /// <para>
    /// ⚠️ NIEODWRACALNE: <c>Down</c> nie przywraca wierszy. Kasowane są wyłącznie wiersze, których pliki użytkownik już wcześniej kazał usunąć,
    /// więc żaden widoczny w aplikacji paragon nie jest ruszany. Pliki w magazynie nie są tu dotykane (zostały skasowane przy usuwaniu paragonu).
    /// </para>
    /// </remarks>
    public partial class PurgeSoftDeletedReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Receipts" WHERE "DeletedAt" IS NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
