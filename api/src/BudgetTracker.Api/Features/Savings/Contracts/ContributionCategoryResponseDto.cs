namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>Kategoria do wyboru przy wpłacie ze zwykłego konta — z limitem i wydanym w bieżącym okresie budżetu.</summary>
/// <param name="Limit"><c>null</c>, gdy kategoria nie ma w tym okresie limitu — okno nie pokazuje wtedy podglądu.</param>
/// <param name="Spent">Wydane w okresie razem z wpłatami na cele ze zwykłego konta, dodatnie.</param>
public sealed record ContributionCategoryResponseDto(Guid Id, string Name, decimal? Limit, decimal Spent);
