namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Zastosowanie strategii w budżecie — tylko zaznaczone akcje.</summary>
/// <param name="NodeIds">Identyfikatory kafelków do zastosowania; pomijane są te, których status nie jest „nowa” ani „zmiana”.</param>
public sealed record ApplyStrategyRequestDto(IReadOnlyList<string> NodeIds);
