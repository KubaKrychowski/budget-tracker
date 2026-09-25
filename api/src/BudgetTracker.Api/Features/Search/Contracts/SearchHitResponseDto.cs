namespace BudgetTracker.Api.Features.Search.Contracts;

/// <summary>
/// Jedno trafienie wyszukiwarki — DANE, nie gotowy wiersz ekranu.
/// </summary>
/// <param name="Id">BusinessId znalezionego bytu; po nim front buduje przejście do niego.</param>
/// <param name="Label">To, co się dopasowało: opis transakcji, nazwa kategorii, zlecenia albo budżetu.</param>
/// <param name="CategoryName">Kategoria trafienia, gdy ją ma — dopowiedzenie w drugiej linii.</param>
/// <param name="Date">Data transakcji; <c>null</c> dla bytów bez daty.</param>
/// <param name="Amount">
/// Kwota ZE ZNAKIEM, tak jak w bazie. Front decyduje, czy pokazać minus — tu nie zgadujemy za niego.
/// </param>
/// <param name="BudgetId">Budżet, w którym trafienie leży; <c>null</c> dla kategorii (są wspólne).</param>
/// <param name="BudgetName">Nazwa tego budżetu — przy szukaniu po wszystkich budżetach to jedyny sposób ich rozróżnienia.</param>
/// <remarks>
/// ⚠️ Żadnego pola z tekstem dla użytkownika ani z trasą. Serwer nie zna języka interfejsu (CLAUDE.md §10),
/// a trasy żyją w `app.routes.ts` — składanie ich tutaj rozjechałoby się przy pierwszej zmianie adresu.
/// </remarks>
public sealed record SearchHitResponseDto(
    Guid Id,
    string Label,
    string? CategoryName,
    DateOnly? Date,
    decimal? Amount,
    Guid? BudgetId,
    string? BudgetName);
