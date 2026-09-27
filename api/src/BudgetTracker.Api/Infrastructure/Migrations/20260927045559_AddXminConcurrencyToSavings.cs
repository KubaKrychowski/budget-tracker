using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Włącza optymistyczną współbieżność (xmin) na <c>SavingsReservation</c> i <c>SavingsGoal</c>.
    /// </summary>
    /// <remarks>
    /// ⚠️ CELOWO PUSTA (no-op). <c>xmin</c> to SYSTEMOWA kolumna PostgreSQL — istnieje na każdej tabeli i nie
    /// wolno jej tworzyć. `UseXminAsConcurrencyToken()` (które ustawiało flagę „nie scaffolduj") zniknęło w
    /// Npgsql 10, więc mapujemy xmin ręcznie jako shadow property (patrz <c>AppDbContext</c>); wtedy EF chce
    /// wygenerować <c>AddColumn "xmin"</c>, co na kolumnie systemowej WYWALIŁOBY migrację. Usuwamy to DDL:
    /// odwzorowanie działa w czasie działania (EF czyta istniejącą kolumnę systemową i używa jej jako tokenu
    /// współbieżności), a sama migracja jest tylko nośnikiem zmiany w snapshocie modelu.
    /// </remarks>
    public partial class AddXminConcurrencyToSavings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bez DDL — xmin jest kolumną systemową (patrz remarks).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bez DDL — nie tworzyliśmy kolumny, więc nie ma czego usuwać.
        }
    }
}
