namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Przepustka do zapisu modelu, wystawiana przez <see cref="ModelStore.TryBeginTraining"/>.
/// </summary>
/// <remarks>
/// Istnieje po to, żeby <c>Publish</c> i <c>Activate</c> DAŁO SIĘ wywołać wyłącznie z trzymaną
/// blokadą — wcześniej były publicznymi metodami singletona, których nic nie broniło, a cała
/// gwarancja „nie ruszamy modelu w trakcie importu" opierała się na tym, że wołający pamiętał
/// o <c>TryBeginTraining</c>. Pierwsze nowe miejsce wywołania cofnęłoby całą tę ochronę.
/// </remarks>
public sealed class ModelWriteLease(ModelStore owner, Action onRelease) : IDisposable
{
    private int _released;

    internal ModelStore Owner { get; } = owner;

    internal bool Released => Volatile.Read(ref _released) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0) onRelease();
    }
}
