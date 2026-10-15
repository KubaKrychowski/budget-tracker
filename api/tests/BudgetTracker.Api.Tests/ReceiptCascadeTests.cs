using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Commands;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Features.Transactions.Commands;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Usunięcie transakcji (lista, reset, usunięcie i purge budżetu) kasuje jej paragony: plik w magazynie i wiersz w bazie.
///
/// Paragon łączy się z transakcją samą kolumną, a bez istniejącej transakcji nie ma żadnego ekranu, na którym dałoby się go usunąć —
/// wiersz niesie sprzedawcę, produkty i nazwę pliku. Wymaga `docker compose up -d db azurite`.
/// </summary>
public sealed class ReceiptCascadeTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_receiptcascade_test;Username=budget;Password=budget_dev_only;Pooling=false";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _userId = Guid.CreateVersion7();

    private AppDbContext _db = null!;
    private Budget _budget = null!;
    private Budget _other = null!;

    public async Task InitializeAsync()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _budget = new Budget("Domowy", new DateOnly(2026, 10, 1), 0m, _clock.GetUtcNow());
        _other = new Budget("Wakacje", new DateOnly(2026, 10, 1), 0m, _clock.GetUtcNow());
        _db.AddRange(_budget, _other);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestBlobs.DropContainerAsync(UserBlobContainer.NameFor(_userId));
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private static ReceiptFiles Files() => new(TestBlobs.Client());

    private ReceiptCleaner Cleaner() => new(_db, Files());

    private async Task<Transaction> AddTransactionAsync(Budget budget)
    {
        var tx = new Transaction(
            new DateOnly(2026, 10, 8), -20.60m, "ZAKUP KARTA", _clock.GetUtcNow(), TransactionStatus.Imported,
            budgetBusinessId: budget.BusinessId);
        _db.Add(tx);
        await _db.SaveChangesAsync();
        return tx;
    }

    /// <summary>Paragon z prawdziwym plikiem w Azurite, przypięty do <paramref name="transaction"/>.</summary>
    private async Task<Receipt> AddReceiptAsync(Transaction transaction)
    {
        var blobName = ReceiptFiles.NewBlobName("image/jpeg");
        await Files().SaveAsync(_userId, blobName, BinaryData.FromBytes([0xFF, 0xD8, 0xFF, 0xE0]), "image/jpeg", default);

        var receipt = new Receipt(_userId, blobName, "paragon.jpg", "image/jpeg", 4, _clock.GetUtcNow(), "Sklep Przykład");
        receipt.AttachTo(transaction.BusinessId);
        _db.Add(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    private async Task<bool> ReceiptExistsAsync(Receipt receipt) =>
        await _db.Receipts.IgnoreQueryFilters().AnyAsync(r => r.BusinessId == receipt.BusinessId);

    private Task<bool> FileExistsAsync(Receipt receipt) => TestBlobs.ExistsAsync(UserBlobContainer.NameFor(_userId), receipt.BlobName);

    private BulkDeleteTransactionsCommandHandler BulkDelete() =>
        new(_db, new TransactionSelectionResolver(new TransactionBudgetScope(_db, _clock), new TransactionFilters(_db)), Cleaner(), _clock);

    private BudgetPurger Purger() => new(_db, Files());

    [Fact]
    public async Task Deleting_a_transaction_from_the_list_deletes_its_receipt_file_and_row_but_not_other_receipts()
    {
        var doomed = await AddTransactionAsync(_budget);
        var kept = await AddTransactionAsync(_budget);
        var doomedReceipt = await AddReceiptAsync(doomed);
        var keptReceipt = await AddReceiptAsync(kept);

        var selection = new TransactionSelectionRequestDto(
            [doomed.BusinessId],
            new TransactionFilterRequestDto([_budget.BusinessId], null, null, null, false, TransactionDirection.All, null, null, null, null));
        await BulkDelete().HandleAsync(new BulkDeleteRequestDto(selection), default);

        Assert.False(await ReceiptExistsAsync(doomedReceipt));
        Assert.False(await FileExistsAsync(doomedReceipt));
        Assert.True(await ReceiptExistsAsync(keptReceipt));
        Assert.True(await FileExistsAsync(keptReceipt));
    }

    [Fact]
    public async Task Resetting_a_budget_deletes_the_receipts_of_its_transactions_only()
    {
        var inBudget = await AddReceiptAsync(await AddTransactionAsync(_budget));
        var elsewhere = await AddReceiptAsync(await AddTransactionAsync(_other));

        await new ResetBudgetCommandHandler(
            _db, new BudgetLookup(_db), new BudgetChildren(_db), Cleaner(), new BudgetListItemReader(_db, _clock), _clock)
            .HandleAsync(_budget.BusinessId, default);

        Assert.False(await ReceiptExistsAsync(inBudget));
        Assert.False(await FileExistsAsync(inBudget));
        Assert.True(await ReceiptExistsAsync(elsewhere));
        Assert.True(await FileExistsAsync(elsewhere));
    }

    [Fact]
    public async Task Deleting_a_budget_deletes_the_receipts_of_its_transactions()
    {
        var receipt = await AddReceiptAsync(await AddTransactionAsync(_budget));

        await new DeleteBudgetCommandHandler(
            _db, new BudgetLookup(_db), new BudgetChildren(_db), Cleaner(), new SavingsTransferMatcher(_db), _clock)
            .HandleAsync(_budget.BusinessId, default);

        Assert.False(await ReceiptExistsAsync(receipt));
        Assert.False(await FileExistsAsync(receipt));
    }

    [Fact]
    public async Task Purging_a_budget_also_deletes_receipts_left_behind_by_transactions_deleted_earlier()
    {
        // Błąd, który to łapie: transakcja skasowana przed kaskadą zostawiała paragon, a purge usuwał transakcję
        // fizycznie i po tym nie dało się już ustalić, że paragon w ogóle miał transakcję.
        var tx = await AddTransactionAsync(_budget);
        var receipt = await AddReceiptAsync(tx);
        _db.Remove(tx);
        _budget.MarkDeleted(_clock.GetUtcNow());
        await _db.SaveChangesAsync();

        await Purger().PurgeAsync(null, default);

        Assert.False(await ReceiptExistsAsync(receipt));
        Assert.False(await FileExistsAsync(receipt));
    }
}
