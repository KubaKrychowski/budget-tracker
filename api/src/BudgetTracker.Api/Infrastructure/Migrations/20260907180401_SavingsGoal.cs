using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SavingsGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavingsGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsGoals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_BudgetBusinessId_StartedOn",
                table: "SavingsGoals",
                columns: new[] { "BudgetBusinessId", "StartedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsGoals_BusinessId",
                table: "SavingsGoals",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsGoals");
        }
    }
}
