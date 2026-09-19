namespace BudgetTracker.Api.Domain;

/// <summary>
/// Ślad jednego importu wyciągu. Issue nazywa to `ImportCSV`.
///
/// Istnieje po to, żeby dało się odpowiedzieć „skąd wzięła się ta transakcja” i żeby
/// podsumowanie z kroku 4 steppera miało pokrycie w danych, a nie tylko w odpowiedzi HTTP.
/// </summary>
public class ImportBatch(
    int budgetId, string bank, string fileName, int rowCount, DateTimeOffset importedAt, Guid userId = default)
    : Entity
{
    /// <summary>Właściciel — powielony z <see cref="Budget.UserId"/>, wyłącznie pod RLS w Postgresie.</summary>
    /// <remarks>Domyślne <c>default</c> z tego samego powodu co <see cref="Budget.UserId"/>.</remarks>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Moment importu — timestamptz, w odróżnieniu od <c>Transaction.Date</c>, które jest datą księgowania.</summary>
    public DateTimeOffset ImportedAt { get; protected set; } = importedAt;

    /// <summary>
    /// Budżet wskazany w kroku 1 steppera. Zastąpił konto: makieta kroku 1 pyta
    /// o bank i budżet, a nie o konto (decyzja użytkownika z 2026-09-02).
    /// </summary>
    public int BudgetId { get; protected set; } = budgetId;

    /// <summary>Klucz banku, z którego pochodził wyciąg (np. „pko") — który parser go czytał.</summary>
    public string Bank { get; protected set; } = bank;

    /// <summary>Nazwa wgranego pliku — jedyny ślad po źródle, bo samego pliku nie przechowujemy.</summary>
    public string FileName { get; protected set; } = fileName;

    /// <summary>Wierszy w pliku po scaleniu duplikatów wewnętrznych.</summary>
    public int RowCount { get; protected set; } = rowCount;

    public int ImportedCount { get; protected set; }

    public int PendingReviewCount { get; protected set; }

    /// <summary>Ile wierszy pominięto, bo już były w bazie (klucz: referencja + kwota + data).</summary>
    public int SkippedDuplicateCount { get; protected set; }

    /// <summary>Liczniki rosną po jednym wierszu, w trakcie zapisu importu.</summary>
    /// <remarks>
    /// Trzy osobne metody, nie jedna z parametrem: licznik wybiera się tu na podstawie STATUSU
    /// wiersza, a pomyłka byłaby niewidoczna — podsumowanie importu nadal by się zsumowało,
    /// tylko nie do tego, co naprawdę się stało.
    /// </remarks>
    public void CountImported() => ImportedCount++;

    /// <inheritdoc cref="CountImported"/>
    public void CountPendingReview() => PendingReviewCount++;

    /// <inheritdoc cref="CountImported"/>
    public void CountSkippedDuplicate() => SkippedDuplicateCount++;
}
