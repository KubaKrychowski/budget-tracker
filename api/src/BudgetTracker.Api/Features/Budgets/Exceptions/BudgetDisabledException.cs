namespace BudgetTracker.Api.Features.Budgets.Exceptions;

/// <summary>
/// Próba dopisania danych do wyłączonego budżetu. Warstwa HTTP tłumaczy to na <b>409 Conflict</b>,
/// nie na 400: żądanie jest poprawne, tylko stan zasobu na nie nie pozwala.
///
/// ⚠️ Dotyczy WYŁĄCZNIE nowych danych (import, dodawanie i edycja transakcji). Zarządzanie
/// budżetem — edycja, reset, usunięcie, przywrócenie — działa na wyłączonym tak samo jak na
/// aktywnym; patrz <see cref="Budget.DisabledAt"/>.
///
/// ⚠️ BEZ własnego tekstu. Zdanie, które zobaczy użytkownik, żyje w `Resources/*.resx`
/// pod kluczem `Import_BudgetDisabled` (CLAUDE.md §5) — a to samo zdanie zapisane
/// dodatkowo tutaj byłoby drugim źródłem prawdy, które rozjedzie się przy pierwszej
/// poprawce tekstu. Do logu wystarczy nazwa typu i <see cref="BusinessId"/>.
/// </summary>
public sealed class BudgetDisabledException(Guid businessId) : Exception
{
    public Guid BusinessId { get; } = businessId;
}
