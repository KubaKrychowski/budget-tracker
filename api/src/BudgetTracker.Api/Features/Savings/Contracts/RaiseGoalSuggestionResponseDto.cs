namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>
/// Propozycja podniesienia celu. <c>null</c> w odpowiedzi, dopóki nie ma DWÓCH dowodów.
/// </summary>
/// <remarks>
/// ⚠️ To jedyna wypowiedź tego ekranu, która mówi o przyszłości — i dlatego jest osobnym polem,
/// a nie doklejką do zdania o odporności. Dowód opisuje przeszłość i jest faktem; propozycja
/// jest ekstrapolacją. Zlanie ich w jeden komunikat zamieniłoby fakt w zachętę.
/// </remarks>
/// <param name="Headroom">
/// Zapas ponad cel, policzony jako NAJMNIEJSZY jednorazowy wydatek z miesięcy-dowodów.
/// Minimum, nie średnia: średnia obiecywałaby zapas, którego w najgorszym z tych miesięcy
/// nie było.
/// </param>
public sealed record RaiseGoalSuggestionResponseDto(decimal Headroom, decimal SuggestedAmount);
