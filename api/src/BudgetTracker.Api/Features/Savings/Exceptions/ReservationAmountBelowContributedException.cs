namespace BudgetTracker.Api.Features.Savings.Exceptions;

/// <summary>Nowa kwota rezerwacji mniejsza niż już na nią wpłacono — najpierw trzeba wycofać nadwyżkę.</summary>
public sealed class ReservationAmountBelowContributedException : Exception;
