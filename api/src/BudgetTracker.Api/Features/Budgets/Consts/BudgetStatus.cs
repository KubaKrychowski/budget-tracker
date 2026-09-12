using System.Text.Json.Serialization;

namespace BudgetTracker.Api.Features.Budgets.Consts;

/// <summary>
/// Stan budżetu widoczny na liście w ustawieniach. Trzy wykluczające się stany, w kolejności
/// od „normalnego" — kolumna „Status" w makiecie pokazuje dokładnie te trzy.
/// </summary>
/// <remarks>
/// ⚠️ Wartości trafiają na front jako tekst (`"active"`), nie liczba — zmiana nazwy zmienia
/// kontrakt. Tekst, bo `status === 1` w szablonie nie mówi nikomu, o który stan chodzi.
/// </remarks>
[JsonConverter(typeof(BudgetStatusConverter))]
public enum BudgetStatus
{
    Active = 1,
    Disabled = 2,
    Deleted = 3,
}
