namespace BudgetTracker.Api.Domain;

public class ModelVersion(
    Guid userId,
    bool active,
    string name,
    int trainingSet,
    int categoriesCount,
    decimal accuracy,
    decimal averageAccuracy,
    string eTag,
    string? jobId = null) : Entity
{
    public Guid UserId { get; protected set; } = userId;

    public bool Active { get; protected set; } = active;

    public string Name { get; protected set; } = name;

    public int TrainingSet { get; protected set; } = trainingSet;

    public int CategoriesCount { get; protected set; } = categoriesCount;

    public decimal Accuracy { get; protected set; } = accuracy;

    public decimal AverageAccuracy { get; protected set; } = averageAccuracy;

    public string ETag { get; protected set; } = eTag;

    /// <summary>Zgłoszenie treningu, które wyprodukowało tę wersję; <c>null</c> przy treningu z CLI.</summary>
    /// <remarks>
    /// ⚠️ To jedyne powiązanie między zakolejkowanym zadaniem a jego wynikiem. Bez niego ekran, który pyta
    /// „czy mój trening się skończył”, musiałby zgadywać po najnowszej aktywnej wersji — a ta bywa cudzym
    /// wynikiem przywrócenia starszego modelu.
    /// </remarks>
    public string? JobId { get; protected set; } = jobId;
    public DateTimeOffset? DeletedAt { get; protected set; }

    public DateTimeOffset? CreatedAt { get; protected set; } = TimeProvider.System.GetUtcNow();

    /// <summary>Stempluje encję jako skasowaną logicznie.</summary>
    /// <remarks>
    /// Wołane przez <c>SoftDeleteInterceptor</c> (zamiast fizycznego <c>DELETE</c>) i przez handlery,
    /// które kasują dzieci razem z rodzicem. ⚠️ Znacznik musi być dla rodzica i dzieci TEN SAM —
    /// przywracanie dopasowuje je po równości tej daty, więc drugie odczytanie zegara rozjeżdża
    /// kasowanie z przywracaniem.
    /// </remarks>
    public void MarkDeleted(DateTimeOffset at) => DeletedAt = at;

    public void Activate()
    {
        Active = true;
    }

    public void Deactivate()
    {
        Active = false;
    }
}