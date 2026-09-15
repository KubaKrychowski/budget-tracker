namespace BudgetTracker.Api.Features.Cli.Contracts;

/// <summary>Opis jednej flagi w odpowiedzi na <c>help</c>.</summary>
public sealed record CliFlagDescriptionDto(string Name, bool Required, string Description);
