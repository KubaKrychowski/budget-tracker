namespace BudgetTracker.Identity.Options;

/// <summary>
/// Originy strony projektu (landing), która wysyła prośby o dostęp do bety. Bez wartości domyślnych w kodzie —
/// dev/demo mają je w <c>appsettings.Development.json</c>, pozostałe środowiska muszą je podać same.
/// </summary>
/// <remarks>
/// ⚠️ Pusta lista = brak CORS dla formularza, czyli przeglądarka odrzuci każdą prośbę. To bezpieczna awaria:
/// domyślne „każdy origin" pozwoliłoby dowolnej stronie zapisywać adresy na listę oczekujących.
/// </remarks>
public sealed class LandingClientOptions
{
    public const string SectionName = "Clients:Landing";

    public string[] Origins { get; init; } = [];
}
