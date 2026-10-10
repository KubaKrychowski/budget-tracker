namespace BudgetTracker.Api.Features.Receipts.Exceptions;

/// <summary>Odczyt paragonu jest niedostępny: nie skonfigurowano usługi OCR albo usługa odrzuciła żądanie.</summary>
public sealed class ReceiptReaderUnavailableException(Exception? inner = null)
    : Exception("Usługa odczytu paragonów jest niedostępna.", inner);
