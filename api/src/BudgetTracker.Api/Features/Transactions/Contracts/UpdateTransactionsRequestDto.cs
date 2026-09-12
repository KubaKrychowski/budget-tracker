namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Edycja inline — pojedyncza (jeden element w <paramref name="Edits"/>) i masowa tym samym żądaniem.</summary>
/// <param name="BudgetIds">
/// Budżety, w których zasięgu wolno edytować — ta sama rola co <c>Filter</c> w
/// <see cref="TransactionSelectionRequestDto"/>. Puste = budżet domyślny, tą samą regułą co lista.
/// Wiersz spoza nich daje 404, a nie cichą edycję czegoś, czego użytkownik nie widzi.
/// </param>
public sealed record UpdateTransactionsRequestDto(
    IReadOnlyList<TransactionEditRequestDto> Edits,
    IReadOnlyList<Guid>? BudgetIds);
