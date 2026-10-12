namespace BudgetTracker.Api.Domain;

/// <summary>Zdjęcie albo PDF paragonu wraz z polami odczytanymi przez OCR — załącznik do transakcji z wyciągu.</summary>
/// <remarks>
/// <para>
/// Sam plik leży w kontenerze blob użytkownika (<see cref="BlobName"/>), a w bazie zostaje tylko wiersz z tym, co
/// wiadomo o paragonie. Paragon NIE jest transakcją i nie zmienia żadnej kwoty: transakcja zostaje najmniejszą
/// jednostką danych (README: „Czego tu celowo nie ma”), paragon jest jej dowodem.
/// </para>
/// <para>
/// ⚠️ Pola <see cref="Merchant"/>, <see cref="ReceiptDate"/> i <see cref="Total"/> to dane Z OCR poprawione przez
/// użytkownika, więc mogą być puste (OCR czegoś nie znalazł) i nigdy nie są źródłem prawdy o transakcji — służą
/// wyłącznie do dopasowania i do podglądu. Transakcja przypięta przez <see cref="TransactionBusinessId"/> to zwykła
/// kolumna z publicznym identyfikatorem, bez relacji EF (jak <see cref="Transaction.StandingOrderBusinessId"/>).
/// </para>
/// </remarks>
public class Receipt(
    Guid userId,
    string blobName,
    string fileName,
    string contentType,
    long sizeBytes,
    DateTimeOffset createdAt,
    string? merchant = null,
    DateOnly? receiptDate = null,
    decimal? total = null) : Entity
{
    /// <summary>Właściciel — wyłącznie pod RLS w Postgresie, jak przy strategii.</summary>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Transakcja, do której przypięto paragon; <c>null</c> = paragon czeka bez przypięcia.</summary>
    public Guid? TransactionBusinessId { get; protected set; }

    /// <summary>Nazwa blobu w kontenerze użytkownika (<c>UserBlobContainer</c>) — generowana przez serwer, nigdy z pliku.</summary>
    public string BlobName { get; protected set; } = blobName;

    /// <summary>Oryginalna nazwa pliku — tylko do wyświetlenia przy pobieraniu.</summary>
    public string FileName { get; protected set; } = fileName;

    public string ContentType { get; protected set; } = contentType;

    public long SizeBytes { get; protected set; } = sizeBytes;

    /// <summary>Sprzedawca z paragonu; <c>null</c>, gdy OCR go nie odczytał.</summary>
    public string? Merchant { get; protected set; } = merchant;

    /// <summary>Data zakupu z paragonu; <c>null</c>, gdy OCR jej nie odczytał.</summary>
    public DateOnly? ReceiptDate { get; protected set; } = receiptDate;

    /// <summary>Suma z paragonu, dodatnia; <c>null</c>, gdy OCR jej nie odczytał.</summary>
    public decimal? Total { get; protected set; } = total;

    public DateTimeOffset CreatedAt { get; protected set; } = createdAt;

    /// <summary>Zapisuje pola po poprawkach użytkownika z ekranu weryfikacji.</summary>
    public void Correct(string? merchant, DateOnly? receiptDate, decimal? total)
    {
        Merchant = merchant;
        ReceiptDate = receiptDate;
        Total = total;
    }

    /// <summary>Przypina paragon do transakcji; <c>null</c> odpina.</summary>
    public void AttachTo(Guid? transactionBusinessId) => TransactionBusinessId = transactionBusinessId;
}
