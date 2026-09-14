using BudgetTracker.Api.Features.Savings.Consts;

namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Jeden miesiąc historii oszczędzania — wiersz tabeli i punkt wykresu naraz.</summary>
/// <param name="Month">Pierwszy dzień miesiąca — miesiąc kalendarzowy <c>Transaction.Date</c>.</param>
/// <param name="Deposited">
/// Ile odłożono. ⚠️ Suma WPŁAT, nie netto — patrz <c>GetSavingsQueryHandler</c>.
/// </param>
/// <param name="Withdrawn">
/// Ile wypłacono z oszczędności. Osobna kolumna, nie odejmowana od wpłat: z tych pieniędzy
/// płaci się zaplanowane wydatki i po to się je trzyma. Widoczna, żeby dało się odróżnić
/// uzasadnioną wypłatę od przelewu tam i z powrotem — jedno i drugie widać w wierszu.
/// </param>
/// <param name="Goal">Cel obowiązujący W TYM miesiącu; <c>null</c>, gdy wtedy celu nie było.</param>
/// <param name="OneOffTotal">Suma jednorazowych wydatków (transakcje zrealizowanych zleceń epizodycznych) tego miesiąca.</param>
/// <param name="OneOffCount">Ile ich było — znacznik na wykresie zapala się od jednego.</param>
public sealed record SavingsMonthResponseDto(
    DateOnly Month,
    decimal Deposited,
    decimal Withdrawn,
    decimal? Goal,
    decimal OneOffTotal,
    int OneOffCount,
    MonthVerdict Verdict);
