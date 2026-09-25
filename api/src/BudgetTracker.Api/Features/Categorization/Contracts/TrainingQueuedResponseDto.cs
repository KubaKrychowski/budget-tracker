namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Potwierdzenie zgłoszenia treningu do kolejki.</summary>
/// <param name="JobId">
/// Identyfikator zadania Hangfire — ten sam, który widać w panelu <c>/hangfire</c>. Służy do
/// diagnostyki; ekran rozpoznaje zakończony trening po nowej aktywnej wersji modelu, nie po tym numerze.
/// </param>
public sealed record TrainingQueuedResponseDto(string JobId);
