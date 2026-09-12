using BudgetTracker.Api.Features.Categorization.Contracts;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>
/// Stan katalogu modeli w jednym odczycie — lista wersji plus trening, który wyprodukował
/// AKTYWNY model (<c>null</c>, gdy aktywny powstał przed wprowadzeniem historii).
/// </summary>
public sealed record ModelCatalog(
    IReadOnlyList<ModelVersionResponseDto> Versions,
    TrainingHistoryEntry? ActiveTraining);
