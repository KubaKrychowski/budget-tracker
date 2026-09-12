using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Consts;
using BudgetTracker.Api.Features.Budgets.Contracts;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Budgets.Commands;

/// <summary>
/// Tworzenie budżetu — krok 1 kreatora.
/// </summary>
/// <remarks>
/// <para>
/// Makieta nie pyta o miesiąc, choć budżet jest miesięczny: bierzemy bieżący z <see cref="TimeProvider"/>,
/// żeby testy nie zależały od zegara maszyny.
/// </para>
/// <para>
/// ⚠️ Świadomie bez ograniczenia „jeden budżet na miesiąc". Kilka budżetów na ten sam okres to
/// porównywanie wariantów tych samych danych — rozróżnia je nazwa, nie okres (CLAUDE.md §5).
/// Ujemny bilans początkowy jest dozwolony: debet to legalny stan konta.
/// </para>
/// </remarks>
public sealed class CreateBudgetCommandHandler(AppDbContext db, TimeProvider clock)
{
    public async Task<CreateBudgetResponseDto> HandleAsync(CreateBudgetRequestDto request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return new CreateBudgetResponseDto(null, CreateBudgetError.NameRequired);
        }

        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (!await db.Currencies.AnyAsync(c => c.Code == currency, ct))
        {
            return new CreateBudgetResponseDto(null, CreateBudgetError.UnsupportedCurrency);
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var budget = new Budget(
            name,
            new DateOnly(today.Year, today.Month, 1),
            request.InitialBalance,
            clock.GetUtcNow(),
            currency);

        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);

        var budgetResponse = new BudgetResponseDto(
            budget.BusinessId,
            budget.Name,
            budget.Month,
            budget.Currency,
            budget.InitialBalance);

        return new CreateBudgetResponseDto(budgetResponse, CreateBudgetError.None);
    }
}
