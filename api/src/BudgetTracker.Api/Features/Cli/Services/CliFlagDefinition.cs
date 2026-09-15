namespace BudgetTracker.Api.Features.Cli.Services;

/// <summary>Jedna flaga komendy — wyłącznie do opisu w <c>help</c>, parsowanie samych wartości robi <see cref="Cli.CliArgs"/>.</summary>
public sealed record CliFlagDefinition(string Name, bool Required, string Description);
