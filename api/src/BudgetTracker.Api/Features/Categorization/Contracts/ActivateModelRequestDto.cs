namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Przywrócenie wskazanej wersji modelu jako aktywnej.</summary>
/// <param name="Version">Wersja z <see cref="ModelVersionResponseDto.Version"/>.</param>
public sealed record ActivateModelRequestDto(string Version);
