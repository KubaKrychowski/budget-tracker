namespace BudgetTracker.Identity.Options;

/// <summary>
/// Czy rejestracja jest otwarta dla każdego, czy tylko dla adresów z listy zaproszeń (zamknięta beta).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Domyślnie <c>true</c>, czyli ZAMKNIĘTA. Domyślne „otwarte" znaczyłoby, że pominięcie sekcji
/// w konfiguracji po cichu wpuszcza każdego — a pomyłka w drugą stronę najwyżej zablokuje zapisy, co widać
/// od razu i da się odkręcić jedną wartością. Zła strona pomyłki ma być tą bezpieczniejszą.
/// </para>
/// <para>
/// ⚠️ Ten przełącznik NIE jest w panelu administratora celowo. Otwarcie rejestracji dla całego internetu
/// to decyzja o wdrożeniu, nie operacja na koncie — wymaga dostępu do konfiguracji serwera, nie samego
/// ciasteczka z rolą. Panel pokazuje za to wprost, w którym stanie jest serwer, żeby ekran nie kłamał.
/// </para>
/// <para>
/// Przełącznik dotyczy WYŁĄCZNIE zakładania kont. Konta już istniejące logują się niezależnie od niego —
/// wyłączenie komuś zaproszenia nie odbiera mu dostępu (patrz <see cref="Models.BetaInvite"/>).
/// </para>
/// </remarks>
public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    public bool ClosedBeta { get; init; } = true;
}
