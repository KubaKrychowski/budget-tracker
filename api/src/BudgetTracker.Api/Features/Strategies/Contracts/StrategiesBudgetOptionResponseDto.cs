namespace BudgetTracker.Api.Features.Strategies.Contracts;

/// <summary>Budżet do przełącznika na ekranie strategii.</summary>
/// <param name="Id">Identyfikator budżetu.</param>
/// <param name="Name">Nazwa budżetu.</param>
/// <param name="Month">Miesiąc budżetu (klucz).</param>
/// <param name="Disabled">Czy budżet jest wyłączony.</param>
public sealed record StrategiesBudgetOptionResponseDto(Guid Id, string Name, DateOnly Month, bool Disabled);
