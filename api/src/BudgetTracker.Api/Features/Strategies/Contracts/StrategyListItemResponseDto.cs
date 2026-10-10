namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Wiersz listy „Wybierz strategię”.</summary>
/// <param name="Id">Identyfikator strategii (<c>BusinessId</c>).</param>
/// <param name="Name">Nazwa.</param>
/// <param name="EventCount">Ile kafelków to zdarzenia (wpływ, wydatek, zdarzenie bez skutku).</param>
/// <param name="ActionCount">Ile kafelków to akcje.</param>
/// <param name="UpdatedAt">Ostatni zapis.</param>
/// <param name="VariantCount">Ile wariantów (poza bazowym) ma strategia — okno „Duplikuj” pyta, czy je kopiować.</param>
public sealed record StrategyListItemResponseDto(
    Guid Id, string Name, int EventCount, int ActionCount, DateTimeOffset UpdatedAt, int VariantCount);
