namespace BudgetTracker.Api.Domain;

/// <summary>Kategoria produktu z paragonu (Nabiał, Pieczywo, Chemia domowa…) — OSOBNY słownik od <see cref="Category"/>.</summary>
/// <remarks>
/// <para>
/// ⚠️ To nie jest kategoria budżetowa: nie ma typu, limitu ani wpływu na kwoty. Kategoria transakcji (odbiorca) zostaje źródłem
/// sumy na wykresie wydatków, a kategoria produktu tylko ROZBIJA tę sumę na podkategorie (DECISIONS.md §17, REWIZJA 2026-10-14).
/// Gdyby oba słowniki były jednym, wykres rozbijałby „Jedzenie” na „Jedzenie” i podkategorie o tej samej nazwie.
/// </para>
/// <para>
/// Właściciel działa jak przy <see cref="Category"/>: <see cref="SharedUserId"/> to kategoria wspólna (bazowa, tylko do odczytu),
/// pozostałe są własne. Izolację pilnuje RLS (migracja <c>AddProductCategories</c>) i filtr Owner w EF.
/// </para>
/// </remarks>
public class ProductCategory(string name, Guid userId = default) : Entity
{
    /// <summary>Nazwa widoczna w UI; unikalna wśród żywych kategorii tego samego właściciela (indeks częściowy).</summary>
    public string Name { get; protected set; } = name;

    /// <summary>Właściciel kategorii; <see cref="SharedUserId"/> oznacza kategorię wspólną.</summary>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Wartość <see cref="UserId"/> kategorii wspólnej — pusty identyfikator, żaden użytkownik go nie ma.</summary>
    public static readonly Guid SharedUserId = Guid.Empty;

    /// <summary>Kategoria wspólna: tylko do odczytu dla użytkowników.</summary>
    public bool IsShared => UserId == SharedUserId;

    /// <summary>Najdłuższa dopuszczalna nazwa.</summary>
    public const int MaxNameLength = 100;
}
