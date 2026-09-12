using System.Reflection;
using BudgetTracker.Api.Domain;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Konwencje enumów (CLAUDE.md §5): numeracja od 1 i miejsce w folderze `Consts`.
///
/// Test jest czysto refleksyjny — nie potrzebuje bazy.
/// </summary>
public sealed class EnumConventionsTests
{
    private static IReadOnlyList<Type> AllEnums => [.. typeof(Entity).Assembly
        .GetTypes()
        .Where(t => t.IsEnum)
        .OrderBy(t => t.FullName)];

    [Fact]
    public void Every_enum_starts_at_one()
    {
        // Nie kosmetyka. `0` jako wartość enuma znaczy, że `default(T)` jest legalnym stanem —
        // a wtedy pominięte pole w JSON-ie albo niezainicjalizowana właściwość wygląda jak
        // świadomy wybór. Przy słownikach w bazie to się kończy kodem spoza słownika:
        // enum idzie do kolumny NAZWĄ, więc brak wartości nie ma nazwy i klucz obcy go odrzuca.
        // Złapane na realnym przypadku — konto bez `Type` i reguła bez `direction` w pliku lokalnym.
        var withZero = AllEnums
            .Where(t => Enum.GetValuesAsUnderlyingType(t).Cast<object>()
                .Any(v => Convert.ToInt64(v) == 0))
            .Select(t => t.FullName!)
            .ToList();

        Assert.Empty(withZero);
    }

    [Fact]
    public void Every_enum_lives_in_a_consts_namespace()
    {
        var misplaced = AllEnums
            .Where(t => !t.Namespace!.EndsWith(".Consts", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();

        Assert.Empty(misplaced);
    }
}
