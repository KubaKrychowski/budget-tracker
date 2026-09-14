using BudgetTracker.Api.Features.Limits.Consts;

namespace BudgetTracker.Api.Features.Limits.Contracts;

/// <summary>Wiersz tabeli limitów — limit kategorii obowiązujący w oglądanym miesiącu i to, ile w nim wydano.</summary>
/// <param name="Id">Identyfikator limitu (<c>BudgetItem.BusinessId</c>) — do zmiany i usunięcia.</param>
/// <param name="Remaining">Limit minus wydane. Może być ujemne — to jest informacja, nie błąd.</param>
/// <param name="Percent">Wykorzystanie w procentach, zaokrąglone; ponad 100 przy przekroczeniu.</param>
/// <param name="NextLimit">
/// Kwota, która zastąpi ten limit w którymś z KOLEJNYCH miesięcy („od IX: 1 200 zł"), albo <c>null</c>.
/// Bez tego miniony miesiąc z inną kwotą niż dziś wyglądałby na błąd, a nie na historię.
/// </param>
public sealed record LimitRowResponseDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    int Percent,
    int WarningThreshold,
    LimitState State,
    DateOnly ValidFrom,
    decimal? NextLimit,
    DateOnly? NextValidFrom);
