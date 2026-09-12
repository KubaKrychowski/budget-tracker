namespace BudgetTracker.Api.Resources;

/// <summary>
/// Znacznik dla <c>IStringLocalizer&lt;SharedResource&gt;</c> — sam w sobie nic nie robi,
/// wskazuje tylko plik zasobów. Trzymamy jeden wspólny zestaw, bo aplikacja jest mała;
/// gdy urośnie, dziel per feature (SharedResource -> DashboardResource itd.).
/// </summary>
public sealed class SharedResource;
