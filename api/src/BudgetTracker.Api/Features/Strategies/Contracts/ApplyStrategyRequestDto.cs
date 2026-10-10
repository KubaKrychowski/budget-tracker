namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Zastosowanie strategii w budżecie — tylko zaznaczone akcje.</summary>
/// <param name="NodeIds">Identyfikatory kafelków do zastosowania; pomijane są te, których status nie jest „nowa” ani „zmiana”.</param>
/// <param name="VariantId">Wariant, który stosujemy; pomiń dla bazowego. Wyłączone w nim akcje nie są brane pod uwagę.</param>
public sealed record ApplyStrategyRequestDto(IReadOnlyList<string> NodeIds, string? VariantId = null);
