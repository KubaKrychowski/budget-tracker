using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReservationContributions : Migration
    {
        /// <summary>Lista umownych wpłat na rezerwację (zgłoszenie #23).</summary>
        /// <remarks>
        /// Istniejące rezerwacje dostają pustą listę — uzbierane liczyła dotąd kolejka z samego stanu konta, więc
        /// nie ma prawdziwych wpłat, które dałoby się odtworzyć. Użytkownik wpłaca od nowa.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contributions",
                table: "SavingsReservations",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contributions",
                table: "SavingsReservations");
        }
    }
}
