namespace BudgetTracker.Api.Features.Receipts.Exceptions;

/// <summary>Paragon nie ma pozycji o wskazanym w adresie indeksie. Warstwa HTTP tłumaczy to na 404.</summary>
public sealed class ReceiptItemNotFoundException : Exception;
