namespace BudgetTracker.Api.Features.Import.Contracts;

/// <summary>
/// Jeden wiersz podglądu — co system zrobiłby z tą pozycją, gdyby zapisać import.
/// </summary>
/// <param name="Index">
/// Numer porządkowy w obrębie podglądu. Wiersz nie ma jeszcze identyfikatora z bazy,
/// a front potrzebuje stabilnego klucza do zaznaczania, usuwania i korekty kategorii.
/// </param>
/// <param name="CategoryName">Nazwa dla człowieka; <c>null</c>, gdy kategorii nie ustalono pewnie.</param>
/// <param name="CategoryId">
/// Propozycja kategorii — także wtedy, gdy pewność jest PONIŻEJ progu.
///
/// ⚠️ Obecność kategorii NIE znaczy „gotowe": od tego jest <paramref name="NeedsReview"/>.
/// Wcześniej niepewna predykcja była wyrzucana i wiersz przychodził pusty, więc użytkownik
/// wybierał z 25 kategorii od zera — mimo że model miał zdanie, tylko niepewne. Podpowiedź,
/// którą wystarczy potwierdzić albo poprawić, jest tańsza i to ona napędza pętlę uczenia
/// (CLAUDE.md §3): poprawka trafia do zbioru treningowego, a potwierdzenie też.
/// </param>
/// <param name="NeedsReview">
/// Czy wiersz wymaga decyzji człowieka. Liczy to SERWER, bo to on zna próg — front, który
/// porównywałby pewność u siebie, rozjechałby się z zapisem przy pierwszej zmianie progu.
/// </param>
public sealed record PreviewRowResponseDto(
    int Index,
    DateOnly Date,
    decimal Amount,
    string Description,
    string TransactionType,
    string? ExternalReference,
    Guid? CategoryId,
    string? CategoryName,
    decimal? Confidence,
    bool NeedsReview,
    bool Duplicate);
