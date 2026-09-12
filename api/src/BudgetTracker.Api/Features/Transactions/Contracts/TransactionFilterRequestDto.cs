using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Transactions.Consts;

namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>
/// Kryteria filtrowania — wspólne dla listy (GET) i dla zasięgu „cały pasujący filtr"
/// w akcjach masowych (<see cref="TransactionSelectionRequestDto"/>), żeby oba wejścia liczyły
/// dokładnie ten sam zestaw wierszy.
/// </summary>
/// <param name="BudgetIds">
/// Puste albo <c>null</c> = weź budżet domyślny, tą samą regułą co dashboard (patrz
/// <c>TransactionBudgetScope.Resolve</c>). Wskazanie kilku liczy je RAZEM.
///
/// ⚠️ Suma po kilku budżetach potrafi policzyć te same pieniądze dwa razy i to nie jest błąd,
/// tylko konsekwencja tego, czym budżet jest: zbiorem zasad na TYCH SAMYCH transakcjach
/// (CLAUDE.md §5), więc ten sam wyciąg wolno zaimportować na dwa budżety-warianty. Zaznaczenie
/// obu podwaja wtedy kafle. Ostrzega o tym front, gdy wybrano więcej niż jeden.
/// </param>
/// <param name="AmountFrom">
/// Zakres działa na WARTOŚCI BEZWZGLĘDNEJ kwoty — użytkownik wpisuje „od-do" bez znaku,
/// tak jak widzi liczbę w kolumnie, niezależnie od kierunku transakcji.
/// </param>
public sealed record TransactionFilterRequestDto(
    IReadOnlyList<Guid>? BudgetIds,
    DateOnly? From,
    DateOnly? To,
    Guid? CategoryId,
    bool Uncategorized,
    TransactionDirection Direction,
    IReadOnlyList<TransactionStatus>? Status,
    decimal? AmountFrom,
    decimal? AmountTo,
    string? Search);
