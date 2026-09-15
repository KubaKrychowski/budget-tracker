namespace BudgetTracker.Api.Features.Cli.Services;

/// <summary>
/// Rejestr wszystkich komend CLI, budowany RAZ przy starcie w <c>Program.cs</c> (każdy
/// <c>Map&lt;Feature&gt;Cli</c> dopisuje swoje komendy) i przekazywany do <see cref="CliModule.MapCli"/>.
/// Zwykły obiekt, nie serwis DI — nie ma stanu per żądanie, więc nie ma powodu, żeby żył w kontenerze.
/// </summary>
public sealed class CliCommandRegistry
{
    private readonly Dictionary<(string Noun, string Verb), CliCommandDefinition> commands = new();

    /// <summary>
    /// Dopisuje komendę. Zwraca <c>this</c>, żeby dało się łączyć wywołania łańcuchem
    /// (<c>registry.MapBudgetsCli().MapTransactionsCli()...</c>), tak jak <c>IServiceCollection.AddXxx</c>.
    /// </summary>
    public CliCommandRegistry Register(
        string noun,
        string verb,
        string description,
        string usage,
        IReadOnlyList<CliFlagDefinition> flags,
        Func<IServiceProvider, Cli.CliArgs, CancellationToken, Task<object?>> execute)
    {
        commands[(noun, verb)] = new CliCommandDefinition(noun, verb, description, usage, flags, execute);
        return this;
    }

    public CliCommandDefinition? Find(string noun, string verb) =>
        commands.GetValueOrDefault((noun, verb));

    /// <summary>Wszystkie komendy, opcjonalnie zawężone do jednego rzeczownika — treść odpowiedzi na <c>help</c>.</summary>
    public IReadOnlyList<CliCommandDefinition> Describe(string? noun = null) =>
        commands.Values
            .Where(c => noun is null || c.Noun == noun)
            .OrderBy(c => c.Noun, StringComparer.Ordinal)
            .ThenBy(c => c.Verb, StringComparer.Ordinal)
            .ToList();
}
