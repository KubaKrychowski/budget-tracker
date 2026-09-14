namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Ekran „Zlecenia epizodyczne” dla jednego budżetu — obie zakładki jednym żądaniem.</summary>
/// <param name="Planned">Zaplanowane, od najbliższego terminu.</param>
/// <param name="Realized">Zrealizowane, od najnowszej transakcji.</param>
/// <param name="PlannedTotal">Suma kwot zaplanowanych.</param>
/// <param name="CollectedTotal">Uzbierane w rezerwacjach zaplanowanych zleceń.</param>
/// <param name="ReservedTotal">Kwoty tych rezerwacji — „1 800 z 4 000”.</param>
/// <param name="RealizedThisYear">Suma zrealizowanych z datą w bieżącym roku.</param>
/// <param name="WithoutSavingsCount">Ile zaplanowanych nie ma rezerwacji.</param>
public sealed record EpisodicOrdersResponseDto(
    IReadOnlyList<EpisodicOrderRowResponseDto> Planned,
    IReadOnlyList<EpisodicOrderRowResponseDto> Realized,
    decimal PlannedTotal,
    decimal CollectedTotal,
    decimal ReservedTotal,
    decimal RealizedThisYear,
    int WithoutSavingsCount,
    DateOnly CurrentMonth,
    IReadOnlyList<Guid> SelectedBudgetIds,
    IReadOnlyList<EpisodicOrdersBudgetOptionResponseDto> Budgets);
