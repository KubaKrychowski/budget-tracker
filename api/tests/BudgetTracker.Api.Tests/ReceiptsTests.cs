using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Services;
using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Services;
using BudgetTracker.Api.Features.Import.Services;
using BudgetTracker.Api.Features.Receipts.Commands;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Models;
using BudgetTracker.Api.Features.Receipts.Queries;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Features.StandingOrders.Services;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Paragony: wgranie z odczytem OCR, zapis z przypięciem do transakcji, kasowanie pliku i kandydaci z importu.
///
/// Silnik OCR jest atrapą (<see cref="FakeReceiptReader"/>) — płatna usługa Azure nie jest w testach, ale plik trafia do
/// PRAWDZIWEGO Azurite, tak jak model kategoryzacji. Opisy i kwoty są zmyślone. Wymaga `docker compose up -d db azurite`.
/// </summary>
public sealed class ReceiptsTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_receipts_test;Username=budget;Password=budget_dev_only";

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _userId = Guid.CreateVersion7();

    private AppDbContext _db = null!;
    private Budget _budget = null!;

    public async Task InitializeAsync()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(_clock))
            .Options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        _budget = new Budget("Domowy", new DateOnly(2026, 10, 1), 0m, _clock.GetUtcNow());
        _db.Add(_budget);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestBlobs.DropContainerAsync(UserBlobContainer.NameFor(_userId));
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private static ReceiptReading Reading(
        string? merchant = "Sklep Przykład", float? merchantConfidence = 0.97f,
        DateOnly? date = null, float? dateConfidence = 0.95f, decimal? total = 20.60m, float? totalConfidence = 0.5f,
        IReadOnlyList<ReceiptLine>? items = null) =>
        new(merchant, merchantConfidence, date ?? new DateOnly(2026, 10, 8), dateConfidence, total, totalConfidence, items);

    /// <summary>Handler zapisu z kategoryzatorem podstawianym przez test (domyślnie bez podpowiedzi).</summary>
    private SaveReceiptCommandHandler Save(CategorySuggestion? suggestion = null) => new(
        _db,
        new FakeCategorizer(suggestion ?? CategorySuggestion.None),
        new ImportConfidenceThreshold(Options.Create(new CategorizationOptions { ConfidenceThreshold = 0.7m })),
        new ImportBudgetLookup(_db),
        new TransactionBudgetScope(_db, _clock),
        new StandingOrderMatcher(_db),
        new SavingsTransferMatcher(_db),
        _clock);

    private UploadReceiptCommandHandler Upload(IReceiptReader reader) =>
        new(_db, reader, new ReceiptFiles(TestBlobs.Client()), new FakeCurrentUserAccessor(_userId), _clock);

    private Task<ReceiptReadResponseDto> UploadJpegAsync(IReceiptReader reader, string contentType = "image/jpeg") =>
        Upload(reader).HandleAsync(new MemoryStream(Jpeg), "paragon.jpg", contentType, Jpeg.Length, CancellationToken.None);

    private async Task<Transaction> AddExpenseAsync(decimal amount, DateOnly date)
    {
        var tx = new Transaction(date, amount, "ZAKUP KARTA", _clock.GetUtcNow(), TransactionStatus.Imported, budgetBusinessId: _budget.BusinessId);
        _db.Add(tx);
        await _db.SaveChangesAsync();
        return tx;
    }

    [Fact]
    public async Task Upload_stores_the_file_and_returns_the_fields_with_uncertainty_flags()
    {
        var result = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        var row = await _db.Receipts.SingleAsync(r => r.BusinessId == result.Id);
        Assert.True(await TestBlobs.ExistsAsync(UserBlobContainer.NameFor(_userId), row.BlobName));
        Assert.Equal(("Sklep Przykład", 20.60m), (result.Merchant, result.Total));
        // Suma ma pewność 0,5, więc ma dostać „sprawdź”; sprzedawca i data są pewne.
        Assert.Equal((false, false, true), (result.MerchantUncertain, result.DateUncertain, result.TotalUncertain));
    }

    [Fact]
    public async Task A_field_the_reader_could_not_find_is_returned_empty_and_flagged()
    {
        var result = await UploadJpegAsync(new FakeReceiptReader(Reading(total: null, totalConfidence: null)));

        Assert.Null(result.Total);
        Assert.True(result.TotalUncertain);
    }

    [Fact]
    public async Task Upload_rejects_an_unsupported_type_before_calling_the_reader()
    {
        var reader = new FakeReceiptReader(Reading());

        await Assert.ThrowsAsync<ReceiptFileInvalidException>(() => UploadJpegAsync(reader, "text/plain"));

        // Łapie błąd, w którym płatny odczyt leciał na pliku, który i tak zostanie odrzucony.
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task When_the_reader_is_unavailable_nothing_is_stored()
    {
        await Assert.ThrowsAsync<ReceiptReaderUnavailableException>(() => UploadJpegAsync(new FakeReceiptReader(null)));

        // Łapie osierocony wiersz po nieudanym odczycie (plik zapisujemy dopiero po udanym odczycie).
        Assert.Empty(await _db.Receipts.ToListAsync());
    }

    [Fact]
    public async Task Save_attaches_the_receipt_to_the_transaction_and_keeps_the_corrected_fields()
    {
        var tx = await AddExpenseAsync(-20.60m, new DateOnly(2026, 10, 8));
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        var saved = await Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(tx.BusinessId, " Poprawiony ", new DateOnly(2026, 10, 8), 20.60m),
            CancellationToken.None);

        var listed = await new GetTransactionReceiptsQueryHandler(_db).HandleAsync(tx.BusinessId, CancellationToken.None);
        Assert.Equal("Poprawiony", saved.Merchant);
        Assert.Equal(uploaded.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task Save_with_a_transaction_that_does_not_exist_is_rejected()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Assert.ThrowsAsync<ReceiptTransactionInvalidException>(() => Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(Guid.NewGuid(), null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Save_without_a_transaction_keeps_the_receipt_unattached()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, 20.60m), CancellationToken.None);

        Assert.Null((await _db.Receipts.SingleAsync(r => r.BusinessId == uploaded.Id)).TransactionBusinessId);
    }

    private static readonly ReceiptLine[] Products =
    [
        new("Chleb pszenny", 1m, 4.50m, 4.50m),
        new("Ser żółty", null, null, 12.90m),
    ];

    [Fact]
    public async Task Upload_keeps_the_products_and_returns_them_with_the_fields()
    {
        var result = await UploadJpegAsync(new FakeReceiptReader(Reading(items: Products)));

        var stored = (await _db.Receipts.SingleAsync(r => r.BusinessId == result.Id)).Items;
        Assert.Equal(["Chleb pszenny", "Ser żółty"], result.Items.Select(i => i.Name));
        // Łapie produkty, które wracały z odpowiedzi, ale nie zapisywały się w jsonb (znikały po odświeżeniu listy).
        Assert.Equal(Products, stored);
    }

    [Fact]
    public async Task Products_come_back_with_the_receipts_of_a_transaction()
    {
        var tx = await AddExpenseAsync(-17.40m, new DateOnly(2026, 10, 8));
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading(items: Products)));
        await Save().HandleAsync(uploaded.Id, new SaveReceiptRequestDto(tx.BusinessId, "Sklep", null, 17.40m), CancellationToken.None);

        var receipts = await new GetTransactionReceiptsQueryHandler(_db).HandleAsync(tx.BusinessId, CancellationToken.None);

        Assert.Equal(2, Assert.Single(receipts).Items.Count);
    }

    [Fact]
    public async Task Creating_a_transaction_from_a_receipt_books_a_negative_amount_in_the_default_budget()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading(items: Products)));

        var saved = await Save().HandleAsync(
            uploaded.Id,
            new SaveReceiptRequestDto(null, "Sklep Żabka", new DateOnly(2026, 10, 11), 8.99m, CreateTransaction: true),
            CancellationToken.None);

        var tx = await _db.Transactions.SingleAsync(t => t.BusinessId == saved.TransactionId);
        // Znak: wydatek jest ujemny, paragon niesie sumę dodatnią.
        Assert.Equal((-8.99m, "Sklep Żabka", new DateOnly(2026, 10, 11), _budget.BusinessId), (tx.Amount, tx.Description, tx.Date, tx.BudgetBusinessId));
        Assert.Null(tx.ImportBatchId);
    }

    [Fact]
    public async Task A_transaction_created_without_a_category_suggestion_waits_for_review()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        var saved = await Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, 20.60m, CreateTransaction: true), CancellationToken.None);

        var tx = await _db.Transactions.SingleAsync(t => t.BusinessId == saved.TransactionId);
        Assert.Equal(TransactionStatus.PendingReview, tx.Status);
    }

    [Fact]
    public async Task A_confident_category_suggestion_is_applied_to_the_created_transaction()
    {
        var category = new Category("Jedzenie");
        _db.Add(category);
        await _db.SaveChangesAsync();
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        var saved = await Save(new CategorySuggestion(category.Id, 0.95m)).HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, 20.60m, CreateTransaction: true), CancellationToken.None);

        var tx = await _db.Transactions.SingleAsync(t => t.BusinessId == saved.TransactionId);
        Assert.Equal((category.Id, TransactionStatus.AutoCategorized), (tx.CategoryId, tx.Status));
    }

    [Fact]
    public async Task Creating_a_transaction_without_a_total_is_rejected_and_creates_nothing()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Assert.ThrowsAsync<ReceiptFieldsInvalidException>(() => Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, null, CreateTransaction: true), CancellationToken.None));

        Assert.Empty(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Choosing_an_existing_transaction_never_creates_another_one()
    {
        var tx = await AddExpenseAsync(-20.60m, new DateOnly(2026, 10, 8));
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(tx.BusinessId, "Sklep", null, 20.60m, CreateTransaction: true), CancellationToken.None);

        // Łapie duplikat: flaga „utwórz” przy wskazanej transakcji ma być ignorowana.
        Assert.Single(await _db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Save_rejects_a_non_positive_total()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Assert.ThrowsAsync<ReceiptFieldsInvalidException>(() => Save().HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, 0m), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_the_file_from_storage_and_the_row_from_the_list()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));
        var blobName = (await _db.Receipts.SingleAsync(r => r.BusinessId == uploaded.Id)).BlobName;

        await new DeleteReceiptCommandHandler(_db, new ReceiptFiles(TestBlobs.Client())).HandleAsync(uploaded.Id, CancellationToken.None);

        // Zdjęcie paragonu to dana osobowa: ma zniknąć z magazynu od razu, nie dopiero po soft delete.
        Assert.False(await TestBlobs.ExistsAsync(UserBlobContainer.NameFor(_userId), blobName));
        Assert.Empty(await _db.Receipts.ToListAsync());
    }

    [Fact]
    public async Task Deleting_an_unknown_receipt_is_not_found()
    {
        await Assert.ThrowsAsync<ReceiptNotFoundException>(() =>
            new DeleteReceiptCommandHandler(_db, new ReceiptFiles(TestBlobs.Client())).HandleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Candidates_come_from_the_requested_budget_within_the_date_window()
    {
        var match = await AddExpenseAsync(-20.60m, new DateOnly(2026, 10, 9));
        await AddExpenseAsync(-20.60m, new DateOnly(2026, 9, 1));

        var handler = new GetReceiptCandidatesQueryHandler(new TransactionBudgetScope(_db, _clock));
        var result = await handler.HandleAsync([_budget.BusinessId], 20.60m, new DateOnly(2026, 10, 8), CancellationToken.None);

        Assert.Equal(match.BusinessId, Assert.Single(result).Id);
    }

    [Fact]
    public async Task Candidates_without_a_total_or_date_are_empty()
    {
        await AddExpenseAsync(-20.60m, new DateOnly(2026, 10, 8));

        var handler = new GetReceiptCandidatesQueryHandler(new TransactionBudgetScope(_db, _clock));

        Assert.Empty(await handler.HandleAsync([_budget.BusinessId], null, new DateOnly(2026, 10, 8), CancellationToken.None));
        Assert.Empty(await handler.HandleAsync([_budget.BusinessId], 20.60m, null, CancellationToken.None));
    }

    private sealed class FakeCategorizer(CategorySuggestion suggestion) : ICategorizer
    {
        public Task<CategorySuggestion> CategorizeAsync(
            string normalizedDescription, string transactionType, decimal amount, CancellationToken ct) =>
            Task.FromResult(suggestion);
    }

    private sealed class FakeReceiptReader(ReceiptReading? reading) : IReceiptReader
    {
        public int Calls { get; private set; }

        public Task<ReceiptReading> ReadAsync(BinaryData content, CancellationToken ct)
        {
            Calls++;
            return reading is null
                ? throw new ReceiptReaderUnavailableException()
                : Task.FromResult(reading);
        }
    }
}
