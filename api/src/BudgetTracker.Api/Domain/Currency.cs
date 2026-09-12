namespace BudgetTracker.Api.Domain;

/// <summary>
/// Słownik walut. Kodem jest kod ISO 4217 (np. <c>PLN</c>) — międzynarodowy, więc się go nie tłumaczy.
/// </summary>
public class Currency(string code) : DictionaryEntity<string>(code);
