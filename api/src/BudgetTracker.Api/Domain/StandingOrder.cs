using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>
/// Zlecenie stałe — nazwane, powtarzalne zobowiązanie („Czynsz”, „Kredyt”) z regułami, które rozpoznają jego
/// transakcje w imporcie.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Zlecenie <b>tylko się przypina</b> do transakcji (<see cref="Transaction.StandingOrderBusinessId"/>) — NIE
/// zmienia jej kategorii (decyzja użytkownika). Kategorię dalej nadają reguły kategoryzacji i model; zlecenie mówi
/// wyłącznie „to jest czynsz”.
/// </para>
/// <para>
/// Transakcja należy do zlecenia, gdy pasuje do KTÓREJKOLWIEK reguły — ten sam czynsz bywa opisany różnie
/// (zmiana zarządcy, inny tytuł przelewu). Zlecenie jest zasadą budżetu, jak cel oszczędnościowy — przeżywa reset
/// budżetu, znika z jego usunięciem.
/// </para>
/// <para>
/// Zakończenie (<see cref="EndMonth"/>) to nie usunięcie: historia i przypięcia zostają, zlecenie przestaje być
/// oczekiwane i przypinać nowe transakcje po ostatnim miesiącu.
/// </para>
/// </remarks>
public class StandingOrder(
    Guid budgetBusinessId,
    string name,
    decimal expectedAmount,
    StandingOrderRhythm rhythm,
    int? dueMonth,
    DateTimeOffset createdAt) : Entity
{
    /// <summary>Budżet zlecenia — zwykła kolumna z publicznym identyfikatorem, bez relacji EF (jak przy transakcji).</summary>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    public string Name { get; protected set; } = name;

    /// <summary>Zwykła kwota zlecenia, dodatnia. Inna kwota w miesiącu to informacja, nie błąd.</summary>
    public decimal ExpectedAmount { get; protected set; } = expectedAmount;

    public StandingOrderRhythm Rhythm { get; protected set; } = rhythm;

    /// <summary>
    /// Miesiąc (1–12), w którym schodzi zlecenie roczne, albo pierwszy miesiąc cyklu kwartalnego.
    /// <c>null</c> przy zleceniu miesięcznym.
    /// </summary>
    public int? DueMonth { get; protected set; } = dueMonth;

    /// <summary>Reguły dopasowania, co najmniej jedna. Łączy je „lub”.</summary>
    public List<StandingOrderRule> Rules { get; protected set; } = [];

    /// <summary>Pierwszy dzień OSTATNIEGO miesiąca zlecenia; <c>null</c> = trwa.</summary>
    public DateOnly? EndMonth { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Zmiana zlecenia — nazwa, kwota, rytm i reguły ruszają się razem, bo razem decydują o przypięciach.</summary>
    public void Change(
        string name, decimal expectedAmount, StandingOrderRhythm rhythm, int? dueMonth,
        IReadOnlyCollection<StandingOrderRule> rules)
    {
        Name = name;
        ExpectedAmount = expectedAmount;
        Rhythm = rhythm;
        DueMonth = dueMonth;
        ReplaceRules(rules);
    }

    /// <summary>Podmienia reguły w całości — przy zakładaniu i przy zmianie.</summary>
    /// <remarks>Nowa lista, nie edycja w miejscu: EF porównuje kolumnę JSON jako całość.</remarks>
    public void ReplaceRules(IReadOnlyCollection<StandingOrderRule> rules) => Rules = [.. rules];

    /// <summary>Kończy zlecenie na miesiącu, w którym leży <paramref name="lastMonth"/>.</summary>
    public void End(DateOnly lastMonth) => EndMonth = new DateOnly(lastMonth.Year, lastMonth.Month, 1);

    /// <summary>Wznawia zakończone zlecenie — znów oczekiwane i przypinające nowe transakcje.</summary>
    public void Resume() => EndMonth = null;

    /// <summary>Czy zlecenie jest zakończone przed miesiącem zaczynającym się <paramref name="month"/>.</summary>
    public bool IsEndedBefore(DateOnly month) => EndMonth is { } end && end < month;

    /// <summary>Czy zlecenie ma zejść w miesiącu zaczynającym się <paramref name="month"/>.</summary>
    public bool IsDueIn(DateOnly month) => !IsEndedBefore(month) && Rhythm switch
    {
        StandingOrderRhythm.Monthly => true,
        StandingOrderRhythm.Quarterly => DueMonth is { } q && (month.Month - q + 12) % 3 == 0,
        StandingOrderRhythm.Yearly => DueMonth == month.Month,
        _ => false,
    };
}
