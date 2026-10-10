using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Receipts.Commands;
using BudgetTracker.Api.Features.Receipts.Contracts;
using BudgetTracker.Api.Features.Receipts.Exceptions;
using BudgetTracker.Api.Features.Receipts.Models;
using BudgetTracker.Api.Features.Receipts.Queries;
using BudgetTracker.Api.Features.Receipts.Services;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
        DateOnly? date = null, float? dateConfidence = 0.95f, decimal? total = 20.60m, float? totalConfidence = 0.5f) =>
        new(merchant, merchantConfidence, date ?? new DateOnly(2026, 10, 8), dateConfidence, total, totalConfidence);

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

        var saved = await new SaveReceiptCommandHandler(_db).HandleAsync(
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

        await Assert.ThrowsAsync<ReceiptTransactionInvalidException>(() => new SaveReceiptCommandHandler(_db).HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(Guid.NewGuid(), null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Save_without_a_transaction_keeps_the_receipt_unattached()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await new SaveReceiptCommandHandler(_db).HandleAsync(
            uploaded.Id, new SaveReceiptRequestDto(null, "Sklep", null, 20.60m), CancellationToken.None);

        Assert.Null((await _db.Receipts.SingleAsync(r => r.BusinessId == uploaded.Id)).TransactionBusinessId);
    }

    [Fact]
    public async Task Save_rejects_a_non_positive_total()
    {
        var uploaded = await UploadJpegAsync(new FakeReceiptReader(Reading()));

        await Assert.ThrowsAsync<ReceiptFieldsInvalidException>(() => new SaveReceiptCommandHandler(_db).HandleAsync(
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
