namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Kategoria do wyboru w kafelku (limit kategorii, wydatek jednorazowy).</summary>
public sealed record StrategyCategoryOptionResponseDto(Guid Id, string Name);
