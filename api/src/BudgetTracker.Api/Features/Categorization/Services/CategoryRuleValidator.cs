using System.Text.RegularExpressions;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>Walidacja reguły przychodzącej — ta sama przy tworzeniu i przy edycji.</summary>
public static class CategoryRuleValidator
{
    /// <summary>
    /// Ten sam timeout co w <see cref="RuleCategorizer"/>. Kompilujemy wzorzec przy zapisie
    /// wyłącznie po to, żeby sprawdzić, czy w ogóle się kompiluje.
    /// </summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Walidacja wejścia. Wszystkie trzy przypadki łączy to samo: reguła zapisuje się bez
    /// błędu i po prostu nigdy nie działa, a użytkownik nie ma jak się dowiedzieć dlaczego.
    /// </summary>
    /// <remarks>
    /// Widełki są odrzucane przy <c>min &gt;= max</c>, nie <c>min &gt; max</c>: silnik sprawdza <c>abs &lt; min</c>
    /// i <c>abs &gt;= max</c>, więc widełki min == max odrzucają wszystko. Równe granice to zawsze pomyłka,
    /// nie celowe „nic nie łap”.
    /// </remarks>
    public static void Validate(CategoryRuleRequestDto request)
    {
        var pattern = Trim(request.Pattern);
        var typePattern = Trim(request.TransactionTypePattern);

        if (pattern is null && typePattern is null) throw new RulePatternRequiredException();

        foreach (var candidate in new[] { pattern, typePattern })
        {
            if (candidate is null) continue;
            try
            {
                _ = new Regex(candidate, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            }
            catch (ArgumentException)
            {
                throw new RulePatternInvalidException();
            }
        }

        if (request.MinAmount is { } min && request.MaxAmount is { } max && min >= max)
        {
            throw new RuleAmountRangeInvalidException();
        }
    }

    /// <summary>Puste i białe znaki traktujemy jak brak — inaczej „ ” byłoby wzorcem.</summary>
    public static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
