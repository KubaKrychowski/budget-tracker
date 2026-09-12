namespace BudgetTracker.Api.Features.Budgets.Contracts;

/// <summary>
/// Zmiana danych budżetu. Makieta ma w modalu nazwę i bilans początkowy — waluty nie pokazuje,
/// więc jej tu nie ma; miesiąc jest nieedytowalny z założenia.
/// </summary>
/// <param name="InitialBalance">
/// ⚠️ Zmiana przelicza CAŁY budżet: bilans na każdy dzień jest liczony od tej wartości, więc
/// przesuwa się cały wykres, a nie jedna liczba. UI musi o tym uprzedzić.
/// </param>
public sealed record UpdateBudgetRequestDto(string Name, decimal InitialBalance);
