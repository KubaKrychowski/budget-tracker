namespace BudgetTracker.Api.Features.Cli.Contracts;

/// <summary>
/// Jedna komenda w odpowiedzi na <c>help</c> — to, czego AI potrzebuje, żeby złożyć poprawne wywołanie
/// bez znajomości składni z góry: nazwa, przykład użycia i lista flag.
/// </summary>
public sealed record CliCommandDescriptionDto(
    string Noun,
    string Verb,
    string Description,
    string Usage,
    IReadOnlyList<CliFlagDescriptionDto> Flags);
