namespace BudgetTracker.Api.Domain;

/// <summary>
/// Znacznik słowników — tabel z zamkniętą listą wartości, do których odwołują się inne tabele.
/// Konkretny słownik dziedziczy po <see cref="DictionaryEntity{TCode}"/>.
/// </summary>
/// <remarks>
/// Świadomie bez <see cref="Entity.BusinessId"/> i soft delete: kod jest jednocześnie kluczem
/// i identyfikatorem publicznym, a wartości słownika nie kasuje się — zmienia się je migracją.
/// Każdy słownik musi być seedowany przez <c>HasData</c>; pilnuje tego
/// <c>BusinessIdAndSoftDeleteTests.Every_entity_has_business_id_and_soft_delete</c>.
/// </remarks>
public abstract class DictionaryEntity;
