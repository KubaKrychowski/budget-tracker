using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Dwa nowe parametry kafelka strategii (<c>CategoryId</c>, <c>StandingOrderId</c>) i nowy rodzaj kafelka — wszystko
    /// siedzi w kolumnie <c>jsonb</c> grafu, więc schemat tabel się NIE zmienia; migracja istnieje tylko po to, żeby
    /// migawka modelu zgadzała się z kodem. Stare zapisy nie mają tych pól i czytają się jako <c>null</c>.
    /// </summary>
    public partial class AddStrategyNodeReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bez zmian schematu — patrz opis klasy.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bez zmian schematu — patrz opis klasy.
        }
    }
}
