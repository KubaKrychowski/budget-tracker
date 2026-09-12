namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>Wskazana wersja modelu nie istnieje na dysku. Warstwa HTTP tłumaczy to na 404.</summary>
public sealed class ModelVersionNotFoundException(string version)
    : Exception($"Wersja modelu {version} nie istnieje.")
{
    public string Version { get; } = version;
}
