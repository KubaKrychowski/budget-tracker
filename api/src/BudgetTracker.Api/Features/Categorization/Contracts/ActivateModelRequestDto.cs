namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Przywrócenie wskazanej wersji modelu jako aktywnej.</summary>
/// <param name="VersionId">Identyfikator z <see cref="ModelVersionResponseDto.Id"/>.</param>
public sealed record ActivateModelRequestDto(Guid VersionId);
