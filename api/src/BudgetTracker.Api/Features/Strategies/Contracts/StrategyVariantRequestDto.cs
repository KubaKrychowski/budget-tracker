namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wariant w żądaniu zapisu strategii — pola jak w <c>StrategyVariant</c>.</summary>
/// <param name="Id">Identyfikator wariantu nadany przez klienta, stabilny między zapisami (1–64 znaki).</param>
/// <param name="Name">Nazwa wariantu, 1–60 znaków, niepowtarzalna w strategii.</param>
/// <param name="DisabledNodeIds">Kafelki wyłączone w tym wariancie; każdy musi istnieć w grafie.</param>
public sealed record StrategyVariantRequestDto(string Id, string Name, IReadOnlyList<string> DisabledNodeIds);
