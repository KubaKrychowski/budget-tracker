using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SavingsReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavingsReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    SettledTransactionBusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsReservations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsReservations_BudgetBusinessId_DueMonth_Priority",
                table: "SavingsReservations",
                columns: new[] { "BudgetBusinessId", "DueMonth", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsReservations_BusinessId",
                table: "SavingsReservations",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsReservations_SettledTransactionBusinessId",
                table: "SavingsReservations",
                column: "SettledTransactionBusinessId",
                unique: true,
                filter: "\"SettledTransactionBusinessId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsReservations");
        }
    }
}
