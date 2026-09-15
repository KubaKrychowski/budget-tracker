using System.Text.RegularExpressions;
using BudgetTracker.Api.Features.Cli.Contracts;
using BudgetTracker.Api.Features.Cli.Exceptions;

namespace BudgetTracker.Api.Features.Cli.Services;

/// <summary>
/// Wykonuje jedną linię CLI: tokenizacja → <c>rzeczownik czasownik</c> → reszta jako pozycyjne
/// argumenty i <c>--flagi</c> → wywołanie zarejestrowanej komendy.
/// </summary>
/// <remarks>
/// ⚠️ Wyjątek z <see cref="CliCommandDefinition.Execute"/> NIE jest tu łapany (poza
/// <see cref="CliArgumentException"/> — to błąd składni, nie domeny). Leci dalej przez pipeline ASP.NET
/// i trafia w ISTNIEJĄCY <c>DomainExceptionHandler</c> — ten sam kod 400/404/409 i ten sam zlokalizowany
/// komunikat co przy zwykłym wywołaniu REST tego samego handlera. Gdyby dispatcher łapał wszystko sam,
/// zduplikowałby całe mapowanie z <c>DomainExceptionHandler</c> i rozjechał się z nim przy pierwszej zmianie.
/// </remarks>
public sealed class CliCommandDispatcher(CliCommandRegistry registry)
{
    /// <summary>
    /// ⚠️ Dwa rodzaje cudzysłowu CELOWO, nie tylko podwójny: JSON (<c>--rules-json '[{"a":1}]'</c>) sam jest
    /// pełen podwójnych cudzysłowów, więc naiwne parowanie <c>"</c>...<c>"</c> ucięłoby wartość na PIERWSZYM
    /// cudzysłowie wewnątrz JSON-a. Pojedynczy cudzysłów nie koliduje ze składnią JSON, więc nie trzeba
    /// dodatkowego escapowania.
    /// </summary>
    private static readonly Regex TokenPattern = new("\"[^\"]*\"|'[^']*'|\\S+", RegexOptions.Compiled);

    public async Task<IResult> ExecuteAsync(string line, IServiceProvider requestServices, CancellationToken ct)
    {
        var tokens = Tokenize(line);
        if (tokens.Count == 0)
            return Results.BadRequest(new { error = "Pusta komenda. Wpisz „help”." });

        if (tokens[0] == "help")
            return Results.Ok(ToDto(registry.Describe()));

        if (tokens.Count >= 2 && tokens[1] == "help")
            return Results.Ok(ToDto(registry.Describe(tokens[0])));

        if (tokens.Count < 2)
            return Results.BadRequest(new { error = $"Brakuje czasownika. Spróbuj „{tokens[0]} help”." });

        var noun = tokens[0];
        var verb = tokens[1];
        var definition = registry.Find(noun, verb);

        if (definition is null)
        {
            var knownVerbs = registry.Describe(noun).Select(c => c.Verb).ToList();
            var error = knownVerbs.Count == 0
                ? $"Nieznany rzeczownik „{noun}”. Wpisz „help”, żeby zobaczyć dostępne."
                : $"Nieznany czasownik „{verb}” dla „{noun}”. Dostępne: {string.Join(", ", knownVerbs)}.";
            return Results.BadRequest(new { error });
        }

        var args = ParseArgs(tokens.Skip(2).ToList());

        try
        {
            var result = await definition.Execute(requestServices, args, ct);
            return Results.Ok(result);
        }
        catch (CliArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IReadOnlyList<CliCommandDescriptionDto> ToDto(IReadOnlyList<CliCommandDefinition> commands) =>
        commands
            .Select(c => new CliCommandDescriptionDto(
                c.Noun, c.Verb, c.Description, c.Usage,
                c.Flags.Select(f => new CliFlagDescriptionDto(f.Name, f.Required, f.Description)).ToList()))
            .ToList();

    private static List<string> Tokenize(string line) =>
        TokenPattern.Matches(line).Select(m => Unquote(m.Value)).ToList();

    private static string Unquote(string token) =>
        token.Length >= 2 && (token[0] == '"' || token[0] == '\'') && token[^1] == token[0]
            ? token[1..^1]
            : token;

    /// <summary>
    /// <c>--flaga wartość</c> parami; <c>--flaga</c> bez wartości (ostatni token albo kolejna flaga zaraz
    /// po niej) to flaga logiczna, ustawiana na <c>"true"</c>. Wszystko inne to argument pozycyjny.
    /// </summary>
    private static CliArgs ParseArgs(IReadOnlyList<string> tokens)
    {
        var positional = new List<string>();
        var flags = new Dictionary<string, string>();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(tokens[i]);
                continue;
            }

            var name = tokens[i][2..];
            var hasValue = i + 1 < tokens.Count && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal);
            flags[name] = hasValue ? tokens[++i] : "true";
        }

        return new CliArgs(positional, flags);
    }
}
