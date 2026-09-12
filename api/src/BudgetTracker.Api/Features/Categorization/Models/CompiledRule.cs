using System.Text.RegularExpressions;
using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>
/// Reguła gotowa do dopasowania — wzorce skompilowane raz, przy wczytaniu.
/// </summary>
/// <remarks>
/// Typ wewnętrzny feature'a: nie wychodzi przez API, żyje między <see cref="Services.RuleMatcher"/>
/// a tym, kto reguły wczytał. <c>CategoryId</c> to klucz zapisu, nie <c>BusinessId</c> — reguły
/// dopasowuje się w pętli po transakcjach, więc liczy się to, co da się porównać bez dodatkowego zapytania.
/// </remarks>
/// <param name="Priority">
/// Potrzebny podglądowi trafień, nie samej kategoryzacji: ta dostaje reguły już posortowane i bierze
/// pierwsze trafienie, ale podgląd musi umieć powiedzieć, że regułę PRZESŁONI inna, stojąca wcześniej.
/// </param>
public sealed record CompiledRule(
    int Priority,
    Regex? Pattern,
    Regex? TypePattern,
    RuleDirection Direction,
    int CategoryId,
    decimal? Min,
    decimal? Max);
