namespace BudgetTracker.Api.Features.Savings.Contracts;

/// <summary>
/// Ustawienie albo zmiana celu.
/// </summary>
/// <remarks>
/// ⚠️ JEDEN budżet, nie lista — w odróżnieniu od odczytu. Cel należy do konkretnego budżetu,
/// więc „ustaw ten cel na trzech naraz" nie ma znaczenia, które dałoby się obronić.
/// </remarks>
/// <param name="Amount">Nowa kwota miesięczna. Dodatnia.</param>
/// <param name="StartedOn">
/// Od którego miesiąca cel obowiązuje. Honorowane WYŁĄCZNIE przy PIERWSZYM celu.
///
/// <para>
/// ⚠️ Ta asymetria jest sednem, nie niedoróbką. Bez wstecznej daty przy pierwszym celu cała
/// dotychczasowa historia zostaje „bez celu", więc dowód — czyli jedyny powód istnienia tego
/// ekranu — nie może powstać wcześniej niż za miesiąc. Użytkownik, który ma 20 miesięcy wyciągu,
/// zobaczyłby pustą tabelę i uznał funkcję za zepsutą.
/// </para>
///
/// <para>
/// Przy ZMIANIE istniejącego celu pole jest ignorowane i datę wylicza serwer — tam wsteczna
/// data byłaby furtką do wyprodukowania dowodu (obniż cel poniżej tego, co już odłożone,
/// z datą wsteczną). Przy pierwszym celu nie ma czego obniżać: to po prostu deklaracja,
/// od kiedy się mierzy.
/// </para>
/// </param>
public sealed record SetSavingsGoalRequestDto(decimal Amount, Guid? BudgetId, DateOnly? StartedOn = null);
