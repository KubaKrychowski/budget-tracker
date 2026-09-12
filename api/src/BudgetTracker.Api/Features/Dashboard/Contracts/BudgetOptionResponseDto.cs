namespace BudgetTracker.Api.Features.Dashboard.Contracts;

/// <summary>Budżet jako pozycja przełącznika na dashboardzie.</summary>
/// <param name="Id">Publiczny <c>BusinessId</c> budżetu — front nigdy nie widzi klucza z bazy.</param>
/// <param name="Disabled">
/// Budżet wyłączony zostaje na liście i daje się wybrać — historię ogląda się także po
/// zamknięciu budżetu. Dashboard tylko go OZNACZA i gasi akcje dopisujące dane; ukrycie
/// go tutaj byłoby cichym skasowaniem widoku na przeszłość.
/// </param>
public record BudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
