namespace BudgetTracker.Api.Domain;

/// <summary>
/// Miesięczny cel oszczędnościowy zadeklarowany przez użytkownika — kwota, którą chce odkładać.
/// </summary>
/// <remarks>
/// ⚠️ <b>Cel jest PODŁOGĄ, nie pułapem.</b> Sprawdzamy „czy odłożono <b>co najmniej</b> tyle",
/// a nie „czy nie przekroczono". To rozstrzyga starszy spór: oszczędności mogą spokojnie liczyć
/// się jako wydatek w pozostałych rachunkach aplikacji, bo śledzimy konto bieżące i przelew na
/// oszczędnościowe naprawdę z niego wychodzi. Skoro cel mierzy się własną miarą i w drugą stronę,
/// nic nikogo za odkładanie nie karze.
///
/// ⚠️ <b>Kwota nie jest wyliczana z danych.</b> Deklaruje ją człowiek. Duży wydatek jest testem
/// obciążeniowym DLA tej kwoty, a nie jej źródłem — pierwsza wersja planu miała to odwrotnie
/// (potencjał = minimum z kwalifikujących się miesięcy) i cały spór „minimum czy średnia" znikł
/// dopiero wtedy, gdy przestaliśmy cokolwiek wyprowadzać z danych.
/// </remarks>
public class SavingsGoal(Guid budgetBusinessId, decimal amount, DateOnly startedOn, DateTimeOffset createdAt)
    : Entity
{
    /// <summary>
    /// Budżet, którego dotyczy cel — publiczny identyfikator, tak samo jak w
    /// <see cref="Transaction.BudgetBusinessId"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ Świadomie BEZ relacji EF, tą samą decyzją co przy transakcji: to zwykła kolumna,
    /// łączona z <see cref="Budget.BusinessId"/> w kodzie zapytań, nigdy kluczem obcym.
    /// </remarks>
    public Guid BudgetBusinessId { get; protected set; } = budgetBusinessId;

    /// <summary>Kwota miesięczna, zawsze dodatnia. <c>numeric(18,2)</c> jak wszystkie kwoty.</summary>
    public decimal Amount { get; protected set; } = amount;

    /// <summary>
    /// Pierwszy dzień miesiąca, od którego cel obowiązuje.
    ///
    /// Miesięczny, nie dzienny, i to jest decyzja: werdykt dotyczy całego miesiąca, więc cel
    /// zmieniający się w jego połowie nie miałby jak zostać przypisany do wyniku.
    /// </summary>
    public DateOnly StartedOn { get; protected set; } = startedOn;

    /// <summary>
    /// Ostatni miesiąc obowiązywania (pierwszy dzień tego miesiąca); <c>null</c> = cel aktywny.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Zmiana kwoty KOŃCZY poprzedni cel datą i zakłada nowy — nie nadpisuje.</b>
    /// Bez tego historia dowodów odnosiłaby się do kwoty, której już nie ma: miesiąc, w którym
    /// odłożyłeś 1 200 zł przy celu 1 200 zł, po podniesieniu celu do 1 500 zł zamieniłby się
    /// wstecz w porażkę. Dowód opisuje przeszłość, więc musi pamiętać, jaka wtedy była miara.
    ///
    /// Rezygnacja z celu to też stempel tej daty, a nie kasowanie wiersza — z tego samego powodu.
    /// </remarks>
    public DateOnly? EndedOn { get; protected set; }

    /// <summary>Kiedy cel zadeklarowano — do kolejności przy równych datach obowiązywania.</summary>
    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Kończy cel wskazanym miesiącem — i przy zmianie kwoty, i przy rezygnacji.</summary>
    /// <remarks>
    /// Który to ma być miesiąc, rozstrzyga handler: podniesienie celu kończy poprzedni miesiącem
    /// poprzednim, obniżenie — bieżącym (inaczej dałoby się wyprodukować dowód wstecz),
    /// a rezygnacja bieżącym, bo cel obowiązywał przez większość tego miesiąca.
    /// </remarks>
    public void End(DateOnly lastMonth) => EndedOn = lastMonth;
}
