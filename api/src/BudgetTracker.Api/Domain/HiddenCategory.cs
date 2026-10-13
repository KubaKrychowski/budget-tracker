namespace BudgetTracker.Api.Domain;

/// <summary>Kategoria wspólna (bazowa) ukryta przez jedno konto — „nie używam jej, nie pokazuj mi jej na listach”.</summary>
/// <remarks>
/// <para>
/// ⚠️ Ukrycie dotyczy WYŁĄCZNIE tego konta i niczego nie kasuje ani nie zmienia: kategoria wspólna jest współdzielona przez wszystkie
/// konta, więc flaga na samej kategorii ukryłaby ją wszystkim. Istniejące transakcje, limity i reguły zostają w tej kategorii i dalej
/// ją pokazują. Ukryta kategoria znika z list wyboru oraz z podpowiedzi kategoryzacji (reguły i ML) — patrz
/// <c>HybridCategorizer</c>.
/// </para>
/// <para>
/// Kategoria to zwykły klucz bez relacji EF (jak <see cref="Transaction.CategoryId"/>). Dotyczy tylko kategorii wspólnych: własnej
/// kategorii nie ma po co ukrywać, można ją usunąć albo przemianować. Wiersz chroni RLS (<c>owner_isolation</c>) jak inne dane konta.
/// </para>
/// </remarks>
public class HiddenCategory(Guid userId, int categoryId) : Entity
{
    /// <summary>Konto, które ukryło kategorię — wyłącznie pod RLS w Postgresie.</summary>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Klucz ukrytej kategorii wspólnej.</summary>
    public int CategoryId { get; protected set; } = categoryId;
}
