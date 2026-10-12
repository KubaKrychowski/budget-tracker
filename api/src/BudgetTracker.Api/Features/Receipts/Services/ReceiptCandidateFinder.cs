using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Receipts.Contracts;

namespace BudgetTracker.Api.Features.Receipts.Services;

/// <summary>Reguły dopasowania paragonu do transakcji z importu — czysta funkcja, bez bazy.</summary>
/// <remarks>
/// ⚠️ Dopasowujemy WYDATKI (kwota ujemna) po wartości bezwzględnej: paragon dokumentuje zakup. Okno dat jest symetryczne,
/// bo bank księguje płatność kartą z opóźnieniem, ale zdarza się też data operacji wcześniejsza niż na paragonie.
/// Nic tu nie przypina automatycznie — kandydaci trafiają na ekran, a wybiera człowiek.
/// </remarks>
public static class ReceiptCandidateFinder
{
    /// <summary>Okno dat wokół daty z paragonu, w dniach, w jedną i w drugą stronę.</summary>
    public const int DateWindowDays = 3;

    /// <summary>Ile najlepszych kandydatów pokazujemy.</summary>
    public const int MaxCandidates = 5;

    private const decimal ExactTolerance = 0.005m;
    private const decimal PossibleToleranceAbsolute = 2.00m;
    private const decimal PossibleToleranceRatio = 0.05m;

    public static IReadOnlyList<ReceiptCandidateResponseDto> Rank(
        IEnumerable<Transaction> transactions, decimal total, DateOnly date) =>
        [.. transactions
            .Where(t => t.Amount < 0)
            .Select(t => (Transaction: t, Diff: Math.Abs(-t.Amount - total), Days: Math.Abs(t.Date.DayNumber - date.DayNumber)))
            .Where(x => x.Days <= DateWindowDays && x.Diff <= Math.Max(PossibleToleranceAbsolute, total * PossibleToleranceRatio))
            .OrderBy(x => x.Diff > ExactTolerance)
            .ThenBy(x => x.Diff)
            .ThenBy(x => x.Days)
            .Take(MaxCandidates)
            .Select(x => new ReceiptCandidateResponseDto(
                x.Transaction.BusinessId, x.Transaction.Date, x.Transaction.Description, x.Transaction.Amount,
                x.Diff <= ExactTolerance ? "Exact" : "Possible"))];
}
