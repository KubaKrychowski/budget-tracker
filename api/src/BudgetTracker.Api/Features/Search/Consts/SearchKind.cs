namespace BudgetTracker.Api.Features.Search.Consts;

/// <summary>
/// Rodzaj trafienia — KLUCZ, nie tekst dla użytkownika. Nazwy grup tłumaczy front (CLAUDE.md §10).
/// </summary>
/// <remarks>
/// Kolejność stałych wyznacza kolejność grup w odpowiedzi, a przez to na ekranie. Najpierw rzeczy,
/// których szuka się najczęściej po nazwie (kategorie, zlecenia), potem transakcje — tych jest najwięcej
/// i bez tego zepchnęłyby resztę poza widok.
/// </remarks>
public static class SearchKind
{
    public const string Categories = "categories";
    public const string Budgets = "budgets";
    public const string StandingOrders = "standingOrders";
    public const string EpisodicOrders = "episodicOrders";
    public const string Transactions = "transactions";

    /// <summary>Kolejność grup w odpowiedzi.</summary>
    public static readonly IReadOnlyList<string> Order =
        [Categories, Budgets, StandingOrders, EpisodicOrders, Transactions];
}
