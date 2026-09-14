using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Budżet może wskazywać powiązany budżet oszczędnościowy (<c>LinkedSavingsBudgetBusinessId</c>,
    /// jednokierunkowo, bez FK) i nieść reguły rozpoznające własne transakcje będące transferem do/z
    /// niego (<c>SavingsTransferRules</c>, jsonb, wzorem <c>StandingOrder.Rules</c>). Transakcja dostaje
    /// pole przypięcia (<c>SavingsTransferBudgetBusinessId</c>) i pamięć ręcznego odpięcia
    /// (<c>SavingsTransferUnpinnedFrom</c>), dokładnie wzorem <c>StandingOrderBusinessId</c>/
    /// <c>StandingOrderUnpinnedFrom</c>.
    /// </summary>
    /// <remarks>
    /// Istniejące budżety i transakcje dostają same <c>null</c>/puste reguły — żaden budżet nie jest
    /// dziś powiązany, żadna transakcja nie jest transferem, zero zmiany zachowania dla obecnych danych.
    /// </remarks>
    public partial class SavingsTransferLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SavingsTransferBudgetBusinessId",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SavingsTransferUnpinnedFrom",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedSavingsBudgetBusinessId",
                table: "Budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingsTransferRules",
                table: "Budgets",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SavingsTransferBudgetBusinessId",
                table: "Transactions",
                column: "SavingsTransferBudgetBusinessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_SavingsTransferBudgetBusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SavingsTransferBudgetBusinessId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SavingsTransferUnpinnedFrom",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "LinkedSavingsBudgetBusinessId",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "SavingsTransferRules",
                table: "Budgets");
        }
    }
}
