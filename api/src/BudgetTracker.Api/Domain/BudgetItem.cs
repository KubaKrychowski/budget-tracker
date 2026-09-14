namespace BudgetTracker.Api.Domain;

/// <summary>Limit wydatków budżetu na jedną kategorię, obowiązujący od danego miesiąca (Etap 1).</summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Limit ma HISTORIĘ</b> — tak jak cel oszczędnościowy. Zmiana kwoty nie nadpisuje wiersza, tylko kończy
/// poprzedni limit miesiąc wcześniej i zakłada nowy. Bez tego sierpień oglądany we wrześniu pokazywałby
/// wrześniowy limit i „przekroczenie", którego w sierpniu nie było (albo odwrotnie).
/// </para>
/// <para>
/// Miesięczny limit budżetu to SUMA limitów kategorii obowiązujących w danym miesiącu — nie osobne pole.
/// </para>
/// </remarks>
public class BudgetItem(Guid budgetBusinessId, int categoryId, decimal limit, DateOnly validFrom, int warningThreshold)
    : Entity
{
    /// <summary>
    /// Budżet, do którego należy limit — publiczny identyfikator, tak samo jak
    /// <see cref="Transaction.BudgetBusinessId"/>. Świadomie bez relacji EF: zwykła kolumna,
    /// łączona z <see cref="Budget.BusinessId"/> w zapytaniach.
    /// </summary>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    public int CategoryId { get; protected set; } = categoryId;

    /// <summary>Limit wydatków na kategorię w miesiącu, jako wartość dodatnia.</summary>
    public decimal Limit { get; protected set; } = limit;

    /// <summary>Pierwszy dzień pierwszego miesiąca, w którym limit obowiązuje.</summary>
    public DateOnly ValidFrom { get; protected set; } = validFrom;

    /// <summary>Pierwszy dzień OSTATNIEGO miesiąca, w którym limit obowiązuje; <c>null</c> = bez końca.</summary>
    public DateOnly? ValidTo { get; protected set; }

    /// <summary>Od ilu procent limitu pasek ostrzega — ustawiane przy każdym limicie osobno (domyślnie 80).</summary>
    public int WarningThreshold { get; protected set; } = warningThreshold;

    /// <summary>Czy limit obowiązuje w miesiącu zaczynającym się <paramref name="month"/>.</summary>
    public bool AppliesTo(DateOnly month) => ValidFrom <= month && (ValidTo is null || ValidTo >= month);

    /// <summary>Kończy limit na miesiącu <paramref name="lastMonth"/> (pierwszy dzień miesiąca, włącznie).</summary>
    public void End(DateOnly lastMonth) => ValidTo = lastMonth;

    /// <summary>Limit znów obowiązuje bez końca — gdy zmiana, która go kończyła, została wycofana, zanim weszła.</summary>
    public void Reopen() => ValidTo = null;

    /// <summary>
    /// Poprawka limitu, który jeszcze nie objął żadnego zamkniętego miesiąca — kwota i próg w miejscu,
    /// bez cięcia historii na kawałki, z których żaden niczego nie zmienia.
    /// </summary>
    public void Correct(decimal limit, int warningThreshold)
    {
        Limit = limit;
        WarningThreshold = warningThreshold;
    }
}
