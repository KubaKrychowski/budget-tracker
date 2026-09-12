namespace BudgetTracker.Api.Features.Transactions.Consts;

/// <summary>
/// Granice, których pilnują OBIE strony — front, żeby dało się je pokazać przy polu, i handler,
/// żeby żadna inna droga zapisu ich nie ominęła.
/// </summary>
/// <remarks>
/// Muszą zgadzać się z konfiguracją EF (<c>AppDbContext</c>: <c>Description</c> ma <c>HasMaxLength(500)</c>):
/// gdyby front puścił dłuższy tekst, Postgres rzuciłby <c>DbUpdateException</c>, którego nie zna
/// <c>DomainExceptionHandler</c> — czyli 500 zamiast komunikatu przy polu.
/// </remarks>
public static class TransactionLimits
{
    public const int DescriptionMaxLength = 500;
}
