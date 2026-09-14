namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Ekran celu oszczędnościowego: cel, historia miesięcy z werdyktami i propozycja podniesienia celu.</summary>
/// <param name="Months">
/// Historia miesięcy, od najnowszego. Jest jednocześnie danymi TABELI i danymi WYKRESU —
/// nie ma osobnej struktury na wykres, bo to te same liczby, a dwie struktury rozjechałyby się.
/// Próg na wykresie to <see cref="SavingsMonthResponseDto.Goal"/> per miesiąc, czyli SCHODEK.
/// </param>
/// <param name="CurrentMonth">
/// Bieżący miesiąc, ZAWSZE obecny — nawet gdy nie ma w nim jeszcze ani jednej transakcji.
/// Kafel „odłożone w tym miesiącu" musi mieć co pokazać od pierwszego dnia.
/// </param>
/// <param name="HasAnyEpisodicExpense">
/// Czy w budżecie jest choć jedno zrealizowane zlecenie epizodyczne (wydatek jednorazowy). <c>false</c> włącza
/// stan „Brak oznaczonych wydatków" — bez nich dowód nie ma jak powstać i trzeba to powiedzieć
/// wprost, zamiast pokazywać „0 dowodów" jak fakt o dyscyplinie użytkownika.
/// </param>
/// <param name="HasAnySavings">
/// Czy widać jakiekolwiek wpłaty na oszczędności. <c>false</c> przy istniejącym celu znaczy
/// najczęściej, że przelew nie wpadł w kategorię „Oszczędności" — ekran ma o tym powiedzieć,
/// zamiast pokazywać zero jako fakt (patrz ryzyko „przed #10" w planie).
/// </param>
/// <param name="Budgets">Budżety do przełącznika nad ekranem — patrz <see cref="SavingsBudgetOptionResponseDto"/>.</param>
public sealed record SavingsResponseDto(
    SavingsGoalResponseDto? Goal,
    IReadOnlyList<SavingsMonthResponseDto> Months,
    SavingsMonthResponseDto CurrentMonth,
    int ProofCount,
    decimal DepositedThisYear,
    RaiseGoalSuggestionResponseDto? RaiseSuggestion,
    bool HasAnyEpisodicExpense,
    bool HasAnySavings,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<SavingsBudgetOptionResponseDto> Budgets);
