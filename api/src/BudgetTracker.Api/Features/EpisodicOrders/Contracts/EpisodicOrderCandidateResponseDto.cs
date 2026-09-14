namespace BudgetTracker.Api.Features.EpisodicOrders.Contracts;

/// <summary>Wydatek, który może zrealizować zlecenie epizodyczne.</summary>
/// <param name="Amount">Kwota ZE ZNAKIEM, jak na wyciągu — wydatek jest ujemny.</param>
public sealed record EpisodicOrderCandidateResponseDto(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    string? CategoryName);
