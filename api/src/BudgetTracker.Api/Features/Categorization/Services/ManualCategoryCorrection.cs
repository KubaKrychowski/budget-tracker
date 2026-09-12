using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Skutek ręcznej korekty kategorii przez człowieka — jedno miejsce, wołane z importu
/// (krok 3 steppera, <c>CommitRowRequestDto.Edited</c>) i z listy transakcji (edycja pojedyncza,
/// masowa i akcja „ustaw kategorię").
/// </summary>
/// <remarks>
/// Bez tego oba wejścia mogłyby z czasem rozjechać się w drobiazgu, który sam się nie zgłosi jako bug:
/// pewność zerowana w jednym miejscu, a zostawiona w drugim.
/// </remarks>
public static class ManualCategoryCorrection
{
    public const TransactionStatus Status = TransactionStatus.ManuallyCategorized;

    /// <summary>Pewność nie ma znaczenia przy decyzji człowieka — to nie predykcja modelu.</summary>
    public static readonly decimal? Confidence = null;
}
