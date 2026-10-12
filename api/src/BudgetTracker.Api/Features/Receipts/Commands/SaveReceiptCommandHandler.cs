using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Receipts.Commands;

/// <summary>
/// Zapisuje poprawione pola paragonu i rozstrzyga, co z transakcją: przypina do wskazanej, zakłada nową albo zostawia paragon
/// bez przypięcia.
/// </summary>
/// <remarks>
/// <para>
/// Nowa transakcja z paragonu przechodzi TĘ SAMĄ ścieżkę co wiersz importu: kategoryzator (reguły, potem ML), próg pewności
/// decydujący o statusie, przypięcie do zleceń stałych i reguł transferu. Inaczej wydatek wpisany z paragonu wyglądałby w liście
/// inaczej niż identyczny z banku. Kwota jest UJEMNA (konwencja znaku — <see cref="Transaction.Amount"/>), a transakcja nie ma
/// partii importu ani numeru referencyjnego, jak każda dodana ręcznie.
/// </para>
/// <para>
/// ⚠️ Nic tu nie wykrywa duplikatu z późniejszym importem z banku: gdy ta sama płatność wpadnie potem z wyciągu, będzie dwa razy
/// (świadoma decyzja właściciela, 2026-10-12; patrz DECISIONS.md §17).
/// </para>
/// </remarks>
public sealed class SaveReceiptCommandHandler(
    AppDbContext db,
    ICategorizer categorizer,
    ImportConfidenceThreshold threshold,
    ImportBudgetLookup budgets,
    TransactionBudgetScope scope,
    StandingOrderMatcher standingOrders,
    SavingsTransferMatcher savingsTransfers,
    TimeProvider clock)
{
    public const int MaxMerchantLength = 200;

    /// <summary>Opis transakcji założonej z paragonu, gdy OCR nie odczytał sprzedawcy.</summary>
    public const string FallbackDescription = "Paragon";

    public async Task<ReceiptResponseDto> HandleAsync(Guid id, SaveReceiptRequestDto request, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.BusinessId == id, ct)
            ?? throw new ReceiptNotFoundException(id);

        var merchant = string.IsNullOrWhiteSpace(request.Merchant) ? null : request.Merchant.Trim();
        if (merchant?.Length > MaxMerchantLength || request.Total is <= 0)
        {
            throw new ReceiptFieldsInvalidException();
        }

        Guid? transactionId = request.TransactionId;
        if (transactionId is { } existing)
        {
            if (!await db.Transactions.AnyAsync(t => t.BusinessId == existing, ct))
            {
                throw new ReceiptTransactionInvalidException();
            }
        }
        else if (request.CreateTransaction)
        {
            if (request.Total is not { } total) throw new ReceiptFieldsInvalidException();

            transactionId = await CreateTransactionAsync(merchant, request.Date, total, request.BudgetId, ct);
        }

        receipt.Correct(merchant, request.Date, request.Total);
        receipt.AttachTo(transactionId);
        await db.SaveChangesAsync(ct);

        return ReceiptMapping.ToResponse(receipt);
    }

    private async Task<Guid> CreateTransactionAsync(
        string? merchant, DateOnly? date, decimal total, Guid? budgetId, CancellationToken ct)
    {
        var budget = await budgets.FindAcceptingAsync(await ResolveBudgetAsync(budgetId, ct), ct);
        var now = clock.GetUtcNow();
        var description = merchant ?? FallbackDescription;
        var amount = -total;

        var suggestion = await categorizer.CategorizeAsync(DescriptionNormalizer.Normalize(description), string.Empty, amount, ct);
        var status = suggestion.CategoryId is null || !threshold.IsConfident(suggestion.Confidence)
            ? TransactionStatus.PendingReview
            : TransactionStatus.AutoCategorized;

        var transaction = new Transaction(
            date ?? DateOnly.FromDateTime(now.UtcDateTime),
            amount,
            description,
            now,
            status,
            categoryId: suggestion.CategoryId,
            confidence: suggestion.Confidence,
            budgetBusinessId: budget.BusinessId,
            userId: budget.UserId);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        await standingOrders.PinAsync(budget.BusinessId, [transaction.BusinessId], ct);
        await savingsTransfers.PinAsync(budget, [transaction.BusinessId], ct);

        return transaction.BusinessId;
    }

    /// <summary>Budżet z żądania albo domyślny — ta sama reguła co lista transakcji (<see cref="TransactionBudgetScope"/>).</summary>
    private async Task<Guid> ResolveBudgetAsync(Guid? budgetId, CancellationToken ct)
    {
        var options = await scope.OptionsAsync(ct);
        return TransactionBudgetScope.Resolve(options, budgetId is { } id ? [id] : null, scope.Today())[0];
    }
}
