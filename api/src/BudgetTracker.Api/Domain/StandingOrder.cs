using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>
/// Zlecenie stałe — nazwane, powtarzalne zobowiązanie („Czynsz”, „Kredyt”) z regułą, która rozpoznaje jego
/// transakcje w imporcie.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Zlecenie <b>tylko się przypina</b> do transakcji (<see cref="Transaction.StandingOrderBusinessId"/>) — NIE
/// zmienia jej kategorii (decyzja użytkownika). Kategorię dalej nadają reguły kategoryzacji i model; zlecenie mówi
/// wyłącznie „to jest czynsz”.
/// </para>
/// <para>
/// Reguła to „tytuł zawiera” plus ZAKRES kwoty, a nie jedna kwota: czynsz po podwyżce musi dalej być czynszem.
/// Zlecenie jest zasadą budżetu, jak cel oszczędnościowy — przeżywa reset budżetu, znika z jego usunięciem.
/// </para>
/// </remarks>
public class StandingOrder(
    Guid budgetBusinessId,
    string name,
    decimal expectedAmount,
    StandingOrderRhythm rhythm,
    int? dueMonth,
    string titlePattern,
    decimal amountFrom,
    decimal amountTo,
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

    /// <summary>Fraza, którą musi ZAWIERAĆ opis transakcji (bez rozróżniania wielkości liter). To dane, nie wzorzec LIKE.</summary>
    public string TitlePattern { get; protected set; } = titlePattern;

    /// <summary>Dolna granica kwoty wydatku (wartość bezwzględna, włącznie).</summary>
    public decimal AmountFrom { get; protected set; } = amountFrom;

    /// <summary>Górna granica kwoty wydatku (wartość bezwzględna, włącznie).</summary>
    public decimal AmountTo { get; protected set; } = amountTo;

    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Zmiana zlecenia — nazwa, kwota, rytm i reguła ruszają się razem, bo razem decydują o przypięciach.</summary>
    public void Change(
        string name, decimal expectedAmount, StandingOrderRhythm rhythm, int? dueMonth,
        string titlePattern, decimal amountFrom, decimal amountTo)
    {
        Name = name;
        ExpectedAmount = expectedAmount;
        Rhythm = rhythm;
        DueMonth = dueMonth;
        TitlePattern = titlePattern;
        AmountFrom = amountFrom;
        AmountTo = amountTo;
    }

    /// <summary>Czy zlecenie ma zejść w miesiącu zaczynającym się <paramref name="month"/>.</summary>
    public bool IsDueIn(DateOnly month) => Rhythm switch
    {
        StandingOrderRhythm.Monthly => true,
        StandingOrderRhythm.Quarterly => DueMonth is { } q && (month.Month - q + 12) % 3 == 0,
        StandingOrderRhythm.Yearly => DueMonth == month.Month,
        _ => false,
    };
}
