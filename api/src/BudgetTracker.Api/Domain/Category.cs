namespace BudgetTracker.Api.Domain;

/// <summary>Kategoria wydatku albo wpływu — taksonomia, na której pracuje kategoryzacja i dashboard.</summary>
public class Category(string name) : Entity
{
    /// <summary>Nazwa widoczna wszędzie w UI; unikalna wśród żywych (indeks częściowy).</summary>
    public string Name { get; protected set; } = name;
}
