using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StandingOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StandingOrderBusinessId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StandingOrderUnpinnedFrom",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StandingOrderRhythms",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingOrderRhythms", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "StandingOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetBusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Rhythm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DueMonth = table.Column<int>(type: "integer", nullable: true),
                    TitlePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AmountFrom = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountTo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingOrders_StandingOrderRhythms_Rhythm",
                        column: x => x.Rhythm,
                        principalTable: "StandingOrderRhythms",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "StandingOrderRhythms",
                column: "Code",
                values: new object[]
                {
                    "Monthly",
                    "Quarterly",
                    "Yearly"
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StandingOrderBusinessId",
                table: "Transactions",
                column: "StandingOrderBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_BudgetBusinessId",
                table: "StandingOrders",
                column: "BudgetBusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_BusinessId",
                table: "StandingOrders",
                column: "BusinessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_Rhythm",
                table: "StandingOrders",
                column: "Rhythm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandingOrders");

            migrationBuilder.DropTable(
                name: "StandingOrderRhythms");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_StandingOrderBusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "StandingOrderBusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "StandingOrderUnpinnedFrom",
                table: "Transactions");
        }
    }
}
