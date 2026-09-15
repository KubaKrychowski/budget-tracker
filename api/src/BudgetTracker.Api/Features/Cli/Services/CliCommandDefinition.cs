namespace BudgetTracker.Api.Features.Cli.Services;

/// <summary>
/// Zarejestrowana komenda CLI: <c>rzeczownik czasownik</c> + jej wykonanie. <see cref="Execute"/> dostaje
/// <see cref="IServiceProvider"/> ŻĄDANIA (nie kontenera aplikacji) — handlery feature'ów są <c>Scoped</c>,
/// więc muszą być rozwiązywane per wywołanie, tak samo jak przy zwykłym endpoincie REST.
/// </summary>
public sealed record CliCommandDefinition(
    string Noun,
    string Verb,
    string Description,
    string Usage,
    IReadOnlyList<CliFlagDefinition> Flags,
    Func<IServiceProvider, Cli.CliArgs, CancellationToken, Task<object?>> Execute);
