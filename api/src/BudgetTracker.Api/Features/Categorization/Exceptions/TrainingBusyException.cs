namespace BudgetTracker.Api.Features.Categorization.Exceptions;

/// <summary>Trening odmówiony, bo trwa import. Żądanie jest poprawne, to stan na nie nie pozwala — 409.</summary>
public sealed class TrainingBusyException : Exception;
