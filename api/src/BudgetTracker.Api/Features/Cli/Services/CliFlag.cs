namespace BudgetTracker.Api.Features.Cli.Services;

/// <summary>Skrót do budowania <see cref="CliFlagDefinition"/> przy rejestracji komend.</summary>
public static class CliFlag
{
    public static CliFlagDefinition Required(string name, string description) => new(name, Required: true, description);
    public static CliFlagDefinition Optional(string name, string description) => new(name, Required: false, description);
}
