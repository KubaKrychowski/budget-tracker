namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Podgląd okna „Zastosuj w budżecie” — co strategia założyłaby w budżecie, z którego pochodzi.</summary>
public sealed record StrategyApplyPreviewResponseDto(
    Guid BudgetId,
    string BudgetName,
    IReadOnlyList<StrategyApplyItemResponseDto> Items);
