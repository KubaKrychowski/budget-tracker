namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Nazwy nazwanych filtrów zapytań EF Core. Jedno miejsce, bo nazwa łączy definicję filtra w
/// <see cref="AppDbContext"/> z każdym <c>IgnoreQueryFilters([...])</c>, który chce go wyłączyć —
/// literówka w którymś z nich nie wywali się przy kompilacji, tylko po cichu nie wyłączy nic.
/// </summary>
public static class QueryFilterNames
{
    /// <summary>Ukrywa wiersze skasowane miękko (<see cref="Domain.Entity.DeletedAt"/>).</summary>
    public const string SoftDelete = "SoftDelete";

    /// <summary>Ukrywa budżety innych użytkowników; wyłączony poza żądaniem HTTP (seedy, Hangfire, testy).</summary>
    public const string Owner = "Owner";
}
