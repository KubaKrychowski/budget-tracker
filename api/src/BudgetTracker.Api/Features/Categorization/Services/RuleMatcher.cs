using System.Text.RegularExpressions;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Categorization.Models;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Jedno miejsce, w którym rozstrzyga się „czy ta reguła łapie tę transakcję".
/// </summary>
/// <remarks>
/// ⚠️ Istnieje dlatego, że dopasowanie ma DWÓCH wywołujących: <see cref="RuleCategorizer"/> (kategoryzuje
/// na serio) i podgląd trafień w kreatorze reguł (pokazuje, co reguła złapie, nic nie zapisując). Gdyby
/// każdy miał własną kopię tych warunków, podgląd rozjechałby się z silnikiem po pierwszej zmianie —
/// a podgląd, który kłamie o skutku reguły, jest gorszy niż jego brak, bo użytkownik zapisuje regułę
/// w zaufaniu do niego.
/// </remarks>
public static class RuleMatcher
{
    /// <summary>
    /// Wzorce pochodzą z bazy, więc teoretycznie mogą być kosztowne. Timeout chroni przed
    /// zawieszeniem importu na patologicznym wyrażeniu (ReDoS).
    /// </summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Kompiluje wzorzec reguły; <c>null</c> znaczy „ten wzorzec nie jest ustawiony".</summary>
    /// <remarks>
    /// Opcje są częścią kontraktu dopasowania, nie szczegółem: <c>IgnoreCase</c>, bo opisy z wyciągu
    /// przychodzą wersalikiem, a użytkownik pisze wzorce małymi literami. Rzuca <see cref="ArgumentException"/>
    /// na niepoprawnym wzorcu — wywołujący decyduje, czy to pominąć (dane), czy zgłosić (wejście użytkownika).
    /// </remarks>
    public static Regex? Compile(string? pattern) => pattern is null
        ? null
        : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);

    /// <summary>Czy reguła łapie transakcję o podanym znormalizowanym opisie, typie i kwocie.</summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Kierunek patrzy na ZNAK kwoty, a widełki na jej wartość bezwzględną — inaczej wydatek
    /// −200 nigdy nie wpadłby w widełki 100–300.</item>
    /// <item>Górna granica jest wyłączna (<c>abs &gt;= max</c> odpada), dolna włączna. Stąd walidacja
    /// odrzuca <c>min == max</c>: takie widełki nie łapią niczego (patrz <see cref="CategoryRuleValidator"/>).</item>
    /// <item>Oba wzorce są opcjonalne, ale USTAWIONE muszą pasować jednocześnie — reguła z wzorcem
    /// opisu i typu to koniunkcja, nie alternatywa.</item>
    /// </list>
    /// </remarks>
    public static bool Matches(CompiledRule rule, string normalizedDescription, string transactionType, decimal amount)
    {
        if (!DirectionAllows(rule.Direction, amount)) return false;

        var abs = Math.Abs(amount);
        if (rule.Min is { } min && abs < min) return false;
        if (rule.Max is { } max && abs >= max) return false;

        if (rule.Pattern is { } p && !p.IsMatch(normalizedDescription)) return false;
        if (rule.TypePattern is { } t && !t.IsMatch(transactionType)) return false;

        return true;
    }

    /// <summary>Czy kierunek reguły dopuszcza transakcję o takim znaku kwoty.</summary>
    public static bool DirectionAllows(RuleDirection direction, decimal amount) => direction switch
    {
        RuleDirection.Expense => amount < 0,
        RuleDirection.Income => amount > 0,
        _ => true,
    };
}
