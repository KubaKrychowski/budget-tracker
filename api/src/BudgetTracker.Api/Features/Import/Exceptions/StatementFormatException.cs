namespace BudgetTracker.Api.Features.Import.Exceptions;

/// <summary>
/// Plik nie jest tym, za co się podaje — zły nagłówek, nieczytelne kodowanie, pusty plik.
/// Endpoint tłumaczy to na 400 z komunikatem z zasobów, nigdy na 500.
/// </summary>
public sealed class StatementFormatException(string resourceKey) : Exception(resourceKey)
{
    /// <summary>Klucz do <c>SharedResource</c> — sam wyjątek nie niesie tekstu dla użytkownika.</summary>
    public string ResourceKey { get; } = resourceKey;
}
