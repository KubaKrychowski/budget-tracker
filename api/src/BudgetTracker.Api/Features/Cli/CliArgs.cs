using System.Globalization;
using System.Text.Json;
using BudgetTracker.Api.Features.Cli.Exceptions;

namespace BudgetTracker.Api.Features.Cli;

/// <summary>
/// Sparsowana linia komendy (bez pierwszych dwóch tokenów — rzeczownika i czasownika): pozycyjne
/// argumenty w kolejności wystąpienia i słownik <c>--flaga wartość</c>. Każdy <c>Get*</c> rzuca
/// <see cref="CliArgumentException"/> przy braku albo złym formacie — dispatcher zamienia to na 400,
/// więc handler komendy nie musi sam sprawdzać, czy argument w ogóle przyszedł.
/// </summary>
public sealed class CliArgs(IReadOnlyList<string> positional, IReadOnlyDictionary<string, string> flags)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string GetPositional(int index) => index < positional.Count
        ? positional[index]
        : throw new CliArgumentException($"Brakuje argumentu #{index + 1}.");

    public Guid GetGuid(int index) => ParseGuid(GetPositional(index), $"argument #{index + 1}");

    public string? GetFlag(string name) => flags.GetValueOrDefault(name);

    public string GetRequiredFlag(string name) =>
        GetFlag(name) ?? throw new CliArgumentException($"Brakuje flagi --{name}.");

    public Guid? GetGuidFlag(string name) =>
        GetFlag(name) is { } value ? ParseGuid(value, $"--{name}") : null;

    public Guid GetRequiredGuidFlag(string name) => ParseGuid(GetRequiredFlag(name), $"--{name}");

    public Guid[]? GetGuidArrayFlag(string name) =>
        GetFlag(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => ParseGuid(v, $"--{name}"))
            .ToArray();

    public decimal? GetDecimalFlag(string name) =>
        GetFlag(name) is { } value ? ParseDecimal(value, $"--{name}") : null;

    public decimal GetRequiredDecimalFlag(string name) => ParseDecimal(GetRequiredFlag(name), $"--{name}");

    public DateOnly? GetDateFlag(string name) =>
        GetFlag(name) is { } value ? ParseDate(value, $"--{name}") : null;

    public DateOnly GetRequiredDateFlag(string name) => ParseDate(GetRequiredFlag(name), $"--{name}");

    public bool GetBoolFlag(string name, bool defaultValue = false) =>
        GetFlag(name) is { } value ? value is "true" or "1" or "yes" : defaultValue;

    public int? GetIntFlag(string name) =>
        GetFlag(name) is { } value ? ParseInt(value, $"--{name}") : null;

    public int GetIntFlag(string name, int defaultValue) => GetIntFlag(name) ?? defaultValue;

    public int GetRequiredIntFlag(string name) => ParseInt(GetRequiredFlag(name), $"--{name}");

    public TEnum? GetEnumFlag<TEnum>(string name) where TEnum : struct, Enum =>
        GetFlag(name) is { } value ? ParseEnum<TEnum>(value, $"--{name}") : null;

    public TEnum GetEnumFlag<TEnum>(string name, TEnum defaultValue) where TEnum : struct, Enum =>
        GetFlag(name) is { } value ? ParseEnum<TEnum>(value, $"--{name}") : defaultValue;

    /// <summary>Lista wartości enuma rozdzielona przecinkami, np. <c>--status Imported,PendingReview</c>.</summary>
    public TEnum[]? GetEnumArrayFlag<TEnum>(string name) where TEnum : struct, Enum =>
        GetFlag(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => ParseEnum<TEnum>(v, $"--{name}"))
            .ToArray();

    /// <summary>Pola złożone (listy reguł, jsonb) idą jako surowy JSON w jednej fladze — patrz DECISIONS.md.</summary>
    public T? GetJsonFlag<T>(string name) =>
        GetFlag(name) is { } value ? Deserialize<T>(value, $"--{name}") : default;

    public T GetRequiredJsonFlag<T>(string name) => Deserialize<T>(GetRequiredFlag(name), $"--{name}")
        ?? throw new CliArgumentException($"Flaga --{name} zdeserializowała się do null.");

    private static Guid ParseGuid(string value, string source) =>
        Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new CliArgumentException($"{source}: „{value}” to nie jest poprawny identyfikator (GUID).");

    private static decimal ParseDecimal(string value, string source) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new CliArgumentException($"{source}: „{value}” to nie jest poprawna kwota.");

    private static DateOnly ParseDate(string value, string source) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new CliArgumentException($"{source}: „{value}” to nie jest poprawna data (RRRR-MM-DD).");

    private static int ParseInt(string value, string source) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new CliArgumentException($"{source}: „{value}” to nie jest poprawna liczba całkowita.");

    private static TEnum ParseEnum<TEnum>(string value, string source) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new CliArgumentException(
                $"{source}: „{value}” to nie jedna z: {string.Join(", ", Enum.GetNames<TEnum>())}.");

    private static T? Deserialize<T>(string value, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(value, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CliArgumentException($"{source}: niepoprawny JSON ({ex.Message}).");
        }
    }
}
