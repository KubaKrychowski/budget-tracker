using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>Kategoria jako pozycja filtra i selecta w edycji.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> kategorii.</param>
/// <param name="Hidden">Ukryta przez to konto (#30) — zwracana tylko przy <c>includeHidden=true</c>; ekran nie pokazuje jej w wyborze, ale potrafi nazwać nią istniejącą transakcję.</param>
/// <param name="Type">Wydatek albo wpływ — front nie podpowiada kategorii przychodowych przy wydatkach.</param>
public sealed record CategoryOptionResponseDto(Guid Id, string Name, CategoryType Type, bool Hidden = false);
