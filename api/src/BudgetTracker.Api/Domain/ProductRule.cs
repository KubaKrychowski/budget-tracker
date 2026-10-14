namespace BudgetTracker.Api.Domain;

/// <summary>Reguła kategoryzacji produktów: wzorzec w nazwie produktu → kategoria produktu.</summary>
/// <remarks>
/// <para>
/// Dwa rodzaje wierszy w jednej tabeli: reguły wspólne (bazowe słowa kluczowe, <see cref="SharedUserId"/>, priorytet
/// <see cref="SeedPriority"/>) i reguły NAUCZONE z poprawek użytkownika (własne, priorytet <see cref="LearnedPriority"/>).
/// Nauczone wygrywają z bazowymi, bo użytkownik wie lepiej niż lista słów kluczowych.
/// </para>
/// <para>
/// Wzorzec dopasowuje się do nazwy po <c>ProductNameNormalizer</c> (małe litery, bez liczb i jednostek), nigdy do surowego tekstu z paragonu.
/// Reguła nauczona to pełne dopasowanie <c>^nazwa$</c> z <see cref="System.Text.RegularExpressions.Regex.Escape(string)"/>.
/// </para>
/// </remarks>
public class ProductRule(int productCategoryId, string pattern, int priority, Guid userId) : Entity
{
    /// <summary>Priorytet reguł nauczonych od użytkownika — sprawdzane przed bazowymi.</summary>
    public const int LearnedPriority = 0;

    /// <summary>Priorytet reguł bazowych.</summary>
    public const int SeedPriority = 100;

    /// <summary>Wyrażenie regularne dopasowywane do znormalizowanej nazwy produktu.</summary>
    public string Pattern { get; protected set; } = pattern;

    public int ProductCategoryId { get; protected set; } = productCategoryId;

    /// <summary>Kolejność sprawdzania, rosnąco; pierwsza pasująca wygrywa.</summary>
    public int Priority { get; protected set; } = priority;

    /// <summary>Właściciel reguły; <see cref="SharedUserId"/> oznacza regułę wspólną (bazową, tylko do odczytu).</summary>
    public Guid UserId { get; protected set; } = userId;

    /// <summary>Wartość <see cref="UserId"/> reguły wspólnej.</summary>
    public static readonly Guid SharedUserId = Guid.Empty;

    /// <summary>Reguła wspólna: tylko do odczytu dla użytkowników.</summary>
    public bool IsShared => UserId == SharedUserId;

    /// <summary>Przestawia regułę na inną kategorię (użytkownik poprawił poprzedni wybór).</summary>
    public void ChangeCategory(int productCategoryId) => ProductCategoryId = productCategoryId;
}
