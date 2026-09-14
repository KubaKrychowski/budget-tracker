namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Zapis zlecenia epizodycznego — zaplanowanego albo od razu zrealizowanego.</summary>
/// <param name="BudgetId">Budżet; <c>null</c> = domyślny. Przy zmianie ignorowany — zlecenie nie zmienia budżetu.</param>
/// <param name="TransactionId">
/// Transakcja zlecenia zrealizowanego („Już zrealizowane” w modalu, „Oznacz jako zlecenie epizodyczne” na liście
/// transakcji). Wtedy plan (<paramref name="CategoryId"/>, <paramref name="Amount"/>, <paramref name="DueMonth"/>) nie
/// jest potrzebny — kwotę, datę i kategorię niesie transakcja. Przy zmianie ignorowany.
/// </param>
/// <param name="CategoryId">Publiczny identyfikator kategorii planu.</param>
/// <param name="DueMonth">Dowolny dzień miesiąca terminu — liczy się miesiąc.</param>
public sealed record SaveEpisodicOrderRequestDto(
    Guid? BudgetId,
    string Name,
    string? Description,
    Guid? TransactionId,
    Guid? CategoryId,
    decimal? Amount,
    DateOnly? DueMonth);
