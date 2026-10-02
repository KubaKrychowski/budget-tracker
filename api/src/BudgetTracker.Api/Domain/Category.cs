using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>Kategoria wydatku albo wpływu — taksonomia, na której pracuje kategoryzacja i dashboard.</summary>
/// <remarks>
/// Parametry konstruktora mają wartości domyślne, bo seedy i testy tworzą zwykle kategorię wspólną i wydatkową;
/// nazwy muszą się zgadzać z właściwościami (EF wiąże po nazwach).
/// </remarks>
public class Category(string name, CategoryType type = CategoryType.Expense, Guid userId = default) : Entity
{
    /// <summary>Nazwa widoczna wszędzie w UI; unikalna wśród żywych kategorii tego samego właściciela (indeks częściowy).</summary>
    public string Name { get; protected set; } = name;

    /// <summary>Wydatek albo wpływ.</summary>
    public CategoryType Type { get; protected set; } = type;

    /// <summary>
    /// Właściciel kategorii: identyfikator konta (<c>sub</c> z tokenu). <see cref="SharedUserId"/> oznacza kategorię
    /// WSPÓLNĄ (bazową): widzą ją wszyscy, ale nikt jej nie zmieni ani nie skasuje.
    /// </summary>
    /// <remarks>
    /// ⚠️ Odczyt i zapis chroni RLS w Postgresie (migracja <c>CustomCategories</c>), tak samo jak filtr Owner w EF.
    /// Kategorie bazowe zakłada seed uruchamiany jako <c>budget_jobs</c> — zwykły użytkownik ma prawo tylko do własnych.
    /// </remarks>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Wartość <see cref="UserId"/> kategorii wspólnej (bazowej) — pusty identyfikator, żaden użytkownik go nie ma.</summary>
    public static readonly Guid SharedUserId = Guid.Empty;

    /// <summary>Kategoria wspólna: tylko do odczytu dla użytkowników.</summary>
    public bool IsShared => UserId == SharedUserId;

    /// <summary>Zmienia nazwę; walidacją (pustka, długość, unikalność) zajmuje się handler.</summary>
    public void Rename(string name) => Name = name;

    /// <summary>Zmienia typ; handler pilnuje, żeby kategoria nie była już używana.</summary>
    public void ChangeType(CategoryType type) => Type = type;
}
