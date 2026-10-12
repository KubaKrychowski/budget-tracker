using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>Dodaje do paragonów kolumnę <c>Items</c> (jsonb) z produktami odczytanymi z paragonu.</summary>
    /// <remarks>
    /// Istniejące paragony dostają pustą listę (<c>[]</c>), nie obiekt: EF generuje dla kolumny kolekcji domyślne <c>{}</c>, które
    /// nie zmaterializowałoby się jako lista. Paragony wgrane przed tą migracją nie mają produktów — nie da się ich odczytać
    /// wstecz bez ponownego odczytu pliku, którego nie robimy (płatne wywołanie).
    /// </remarks>
    public partial class AddReceiptItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Items",
                table: "Receipts",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Items",
                table: "Receipts");
        }
    }
}
