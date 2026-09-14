using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Domain.Consts;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Transactions.Commands;
using BudgetTracker.Api.Features.Transactions.Consts;
using BudgetTracker.Api.Features.Transactions.Contracts;
using BudgetTracker.Api.Features.Transactions.Exceptions;
using BudgetTracker.Api.Features.Transactions.Queries;
using BudgetTracker.Api.Features.Transactions.Services;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Testy jadą na PRAWDZIWYM Postgresie — patrz uzasadnienie w DashboardHandlerTests.
/// Wymaga `docker compose up -d db`.
/// </summary>
public sealed class TransactionsFeatureTests : IAsyncLifetime
{
    private const string TestConnection =
        "Host=localhost;Port=5432;Database=budgettracker_transactions_test;Username=budget;Password=budget_dev_only";

    private AppDbContext _db = null!;
    private FakeTimeProvider _clock = null!;

    private static readonly DateOnly Today = new(2026, 3, 31);

    private Guid _budgetId;
    private Guid _jedzenieId;
    private Guid _transportId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options;
        _db = new AppDbContext(options);
        await TestDatabase.ResetAsync(TestConnection);
        await _db.Database.EnsureCreatedAsync();

        var jedzenie = new Category("Jedzenie");
        var transport = new Category("Transport");
        _db.Categories.AddRange(jedzenie, transport);
        await _db.SaveChangesAsync();
        _jedzenieId = jedzenie.BusinessId;
        _transportId = transport.BusinessId;

        var budget = new Budget("Testowy", new DateOnly(2026, 3, 1), 0m, default);
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();
        _budgetId = budget.BusinessId;

        var now = DateTimeOffset.UtcNow;
        _db.Transactions.AddRange(
            New(new DateOnly(2026, 3, 2), -100m, "BIEDRONKA", jedzenie.Id, TransactionStatus.Confirmed, 1),
            New(new DateOnly(2026, 3, 5), -50m, "LIDL", jedzenie.Id, TransactionStatus.AutoCategorized, 2),
            New(new DateOnly(2026, 3, 10), -400m, "SERWIS", transport.Id, TransactionStatus.Confirmed, 3),
            New(new DateOnly(2026, 3, 12), -25m, "NIEZNANE", null, TransactionStatus.PendingReview, 4),
            New(new DateOnly(2026, 3, 15), 5000m, "WYNAGRODZENIE", null, TransactionStatus.Confirmed, 5));
        await _db.SaveChangesAsync();

        _clock = new FakeTimeProvider(new DateTimeOffset(Today.Year, Today.Month, Today.Day, 0, 0, 0, TimeSpan.Zero));
    }

    private Transaction New(DateOnly date, decimal amount, string desc,
        int? categoryId, TransactionStatus status, int minute) =>
        new(
            date,
            amount,
            desc,
            new DateTimeOffset(2026, 3, 1, 0, minute, 0, TimeSpan.Zero),
            status,
            categoryId: categoryId,
            budgetBusinessId: _budgetId);

    public async Task DisposeAsync()
    {
        await TestDatabase.DropAsync(TestConnection);
        await _db.DisposeAsync();
    }

    private GetTransactionsListQueryHandler ListHandler(AppDbContext? db = null)
    {
        var context = db ?? _db;
        return new GetTransactionsListQueryHandler(
            new TransactionBudgetScope(context, _clock),
            new TransactionFilters(context),
            new TransactionListItemReader(context));
    }

    private UpdateTransactionsCommandHandler UpdateHandler() =>
        new(_db, new TransactionBudgetScope(_db, _clock), new TransactionListItemReader(_db));

    private TransactionSelectionResolver Selection() =>
        new(new TransactionBudgetScope(_db, _clock), new TransactionFilters(_db));

    private BulkDeleteTransactionsCommandHandler BulkDeleteHandler() => new(_db, Selection(), _clock);

    private BulkSetTransactionCategoryCommandHandler BulkSetCategoryHandler() => new(_db, Selection());


    private static TransactionFilterRequestDto EmptyFilter(params Guid[] budgetIds) => new(
        budgetIds.Length == 0 ? null : budgetIds,
        null, null, null, false, TransactionDirection.All, null, null, null, null);

    [Fact]
    public async Task Filters_by_category()
    {
        var filter = EmptyFilter(_budgetId) with { CategoryId = _jedzenieId };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(2, r.Total);
        Assert.All(r.Items, i => Assert.Equal("Jedzenie", i.CategoryName));
    }

    [Fact]
    public async Task Uncategorized_filter_returns_only_rows_without_a_category()
    {
        var filter = EmptyFilter(_budgetId) with { Uncategorized = true };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(2, r.Total); // NIEZNANE + WYNAGRODZENIE
        Assert.All(r.Items, i => Assert.Null(i.CategoryId));
    }

    [Fact]
    public async Task Direction_filter_separates_expenses_from_income()
    {
        var expenses = await ListHandler().HandleAsync(
            EmptyFilter(_budgetId) with { Direction = TransactionDirection.Expense }, 1, 20, null, true, default);
        var income = await ListHandler().HandleAsync(
            EmptyFilter(_budgetId) with { Direction = TransactionDirection.Income }, 1, 20, null, true, default);

        Assert.Equal(4, expenses.Total);
        Assert.Equal(1, income.Total);
        Assert.Equal("WYNAGRODZENIE", income.Items.Single().Description);
    }

    [Fact]
    public async Task Amount_range_matches_absolute_value_regardless_of_sign()
    {
        // 400 (SERWIS, wydatek) i 5000 (WYNAGRODZENIE, przychod) leza poza 40..200;
        // w oknie 40..200 zostaja tylko BIEDRONKA (100) i LIDL (50).
        var filter = EmptyFilter(_budgetId) with { AmountFrom = 40m, AmountTo = 200m };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(2, r.Total);
        Assert.All(r.Items, i => Assert.InRange(Math.Abs(i.Amount), 40m, 200m));
    }

    [Fact]
    public async Task Search_matches_description_case_insensitively()
    {
        var filter = EmptyFilter(_budgetId) with { Search = "biedronk" };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal("BIEDRONKA", r.Items.Single().Description);
    }

    [Fact]
    public async Task Status_filter_accepts_multiple_values()
    {
        var filter = EmptyFilter(_budgetId) with
        {
            Status = [TransactionStatus.PendingReview, TransactionStatus.AutoCategorized],
        };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(2, r.Total); // NIEZNANE + LIDL
    }

    [Fact]
    public async Task Summary_covers_the_whole_filtered_set_not_just_the_current_page()
    {
        // Strona 1 z 3 (2 wiersze z 5) — kafle maja mowic o calym zestawie, nie o oknie.
        var page1 = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 2, "date", true, default);
        var page3 = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 3, 2, "date", true, default);

        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page3.Items);

        Assert.Equal(575m, page1.Summary.TotalExpenses); // 100+50+400+25
        Assert.Equal(5000m, page1.Summary.TotalIncome);
        Assert.Equal(4425m, page1.Summary.Balance);
        Assert.Equal(400m, page1.Summary.LargestExpenseAmount); // dodatnia, tak jak w UI

        // Ta sama odpowiedz na ostatniej stronie — inaczej kafle skakalyby przy przewijaniu.
        Assert.Equal(page1.Summary, page3.Summary);
    }

    [Fact]
    public async Task Summary_follows_the_filters()
    {
        var filter = EmptyFilter(_budgetId) with { CategoryId = _jedzenieId };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(150m, r.Summary.TotalExpenses); // 100+50, bez SERWISU i NIEZNANEGO
        Assert.Equal(0m, r.Summary.TotalIncome);
        Assert.Equal(-150m, r.Summary.Balance);
        Assert.Equal(100m, r.Summary.LargestExpenseAmount);
    }

    [Fact]
    public async Task Summary_of_a_set_without_expenses_reports_zeros_not_a_negated_null()
    {
        var filter = EmptyFilter(_budgetId) with { Direction = TransactionDirection.Income };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        Assert.Equal(0m, r.Summary.TotalExpenses);
        Assert.Equal(5000m, r.Summary.TotalIncome);
        Assert.Equal(5000m, r.Summary.Balance);
        Assert.Equal(0m, r.Summary.LargestExpenseAmount); // Min po pustym zbiorze, nie -0 ani wyjatek
    }

    [Fact]
    public async Task Sorting_by_date_has_a_stable_tie_break_so_no_row_repeats_or_vanishes_across_pages()
    {
        var page1 = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 2, "date", true, default);
        var page2 = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 2, 2, "date", true, default);
        var page3 = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 3, 2, "date", true, default);

        var seen = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(i => i.Id).ToList();
        Assert.Equal(5, seen.Distinct().Count());
    }

    [Fact]
    public async Task Defaults_to_the_same_budget_selection_rule_as_the_dashboard()
    {
        _db.Budgets.Add(new Budget("Przyszly", new DateOnly(2026, 12, 1), 0m, default));
        await _db.SaveChangesAsync();

        var r = await ListHandler().HandleAsync(EmptyFilter(), 1, 20, null, true, default);

        // Marcowy budzet juz sie zaczal wzgledem "dzis" (31 marca); grudniowy jeszcze nie.
        Assert.Equal(_budgetId, Assert.Single(r.SelectedBudgetIds));
    }

    [Fact]
    public async Task Unknown_explicit_budget_id_throws_not_found_instead_of_falling_back()
    {
        await Assert.ThrowsAsync<BudgetNotFoundException>(() =>
            ListHandler().HandleAsync(EmptyFilter(Guid.NewGuid()), 1, 20, null, true, default));
    }

    [Fact]
    public async Task No_budget_at_all_reports_the_first_empty_state()
    {
        await using var freshDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnection)
            .AddInterceptors(new SoftDeleteInterceptor(TimeProvider.System))
            .Options);
        await TestDatabase.ResetAsync(TestConnection);
        await freshDb.Database.EnsureCreatedAsync();

        var handler = ListHandler(freshDb);
        var r = await handler.HandleAsync(EmptyFilter(), 1, 20, null, true, default);

        Assert.Empty(r.SelectedBudgetIds);
        Assert.False(r.HasAnyTransactions);
        Assert.Empty(r.Items);
    }

    [Fact]
    public async Task Budget_with_transactions_but_no_filter_matches_reports_the_third_empty_state()
    {
        var filter = EmptyFilter(_budgetId) with { Search = "nie ma takiego opisu" };
        var r = await ListHandler().HandleAsync(filter, 1, 20, null, true, default);

        // Odroznia sie od "budzet bez transakcji": HasAnyTransactions musi zostac true.
        Assert.True(r.HasAnyTransactions);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public async Task Editing_the_category_sets_manually_categorized_and_clears_confidence()
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "LIDL");
        target.Recategorize(target.CategoryId, target.Status, 0.42m);
        await _db.SaveChangesAsync();

        var edit = new TransactionEditRequestDto(
            target.BusinessId, target.Date, target.Description, target.Amount, _transportId);
        var result = await UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default);

        var saved = Assert.Single(result);
        Assert.Equal("ManuallyCategorized", saved.Status);
        Assert.Null(saved.Confidence);
        Assert.Equal(_transportId, saved.CategoryId);
    }

    [Fact]
    public async Task Clearing_the_category_sends_the_row_back_to_pending_review()
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "BIEDRONKA");

        var edit = new TransactionEditRequestDto(
            target.BusinessId, target.Date, target.Description, target.Amount, null);
        var result = await UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default);

        Assert.Equal("PendingReview", Assert.Single(result).Status);
    }

    [Fact]
    public async Task Bulk_update_is_atomic_one_unknown_id_rolls_back_all_edits()
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "LIDL");
        var originalAmount = target.Amount;

        var edits = new List<TransactionEditRequestDto>
        {
            new(target.BusinessId, target.Date, target.Description, 999m, _jedzenieId),
            new(Guid.NewGuid(), Today, "nieistniejaca", 1m, null),
        };

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto(edits, [_budgetId]), default));

        var reloaded = await _db.Transactions
            .AsNoTracking()
            .FirstAsync(t => t.BusinessId == target.BusinessId);
        Assert.Equal(originalAmount, reloaded.Amount); // nic sie nie zapisalo
    }

    [Fact]
    public async Task Bulk_delete_soft_deletes_the_selected_rows_and_they_drop_out_of_the_list()
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "SERWIS");
        var selection = new TransactionSelectionRequestDto([target.BusinessId], EmptyFilter(_budgetId));

        var result = await BulkDeleteHandler().HandleAsync(new BulkDeleteRequestDto(selection), default);
        Assert.Equal(1, result.Affected);

        var r = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 20, null, true, default);
        Assert.DoesNotContain(r.Items, i => i.Id == target.BusinessId);
    }

    [Fact]
    public async Task Bulk_set_category_applies_the_manual_correction_rule_to_every_row()
    {
        var ids = await _db.Transactions
            .Where(t => t.Description == "BIEDRONKA" || t.Description == "NIEZNANE")
            .Select(t => t.BusinessId)
            .ToListAsync();
        var selection = new TransactionSelectionRequestDto(ids, EmptyFilter(_budgetId));

        var result = await BulkSetCategoryHandler().HandleAsync(
            new BulkSetCategoryRequestDto(selection, _transportId), default);
        Assert.Equal(2, result.Affected);

        // AsNoTracking: ExecuteUpdateAsync pisze bezpośrednio do bazy, mijając tracker. Te same
        // encje siedzą już w `_db` z czasu setupu (identity resolution zwróciłaby stare wartości
        // z pamięci zamiast świeżych z bazy), więc weryfikacja musi ominąć tracker.
        var transportKey = await _db.Categories
            .Where(c => c.BusinessId == _transportId)
            .Select(c => c.Id)
            .SingleAsync();
        var updated = await _db.Transactions
            .AsNoTracking()
            .Where(t => ids.Contains(t.BusinessId))
            .ToListAsync();
        Assert.All(updated, t =>
        {
            Assert.Equal(transportKey, t.CategoryId);
            Assert.Equal(TransactionStatus.ManuallyCategorized, t.Status);
            Assert.Null(t.Confidence);
        });
    }

    [Fact]
    public async Task Whole_filter_scope_selection_resolves_ids_from_the_filter_not_an_explicit_list()
    {
        // "zaznacz wszystkie N pasujacych": Ids puste, Filter niesie kryteria.
        var selection = new TransactionSelectionRequestDto(
            null, EmptyFilter(_budgetId) with { Direction = TransactionDirection.Expense });

        var result = await BulkSetCategoryHandler().HandleAsync(
                new BulkSetCategoryRequestDto(selection, _transportId), default);
        Assert.Equal(4, result.Affected); // wszystkie 4 wydatki, nie tylko jedna strona

        // Piata transakcja (WYNAGRODZENIE, przychod) zostaje nietknieta — filtr mial
        // Direction=Expense, wiec zasieg wyszedl z kryteriow, a nie z listy identyfikatorow.
        var salary = await _db.Transactions.AsNoTracking().SingleAsync(t => t.Description == "WYNAGRODZENIE");
        Assert.NotEqual(TransactionStatus.ManuallyCategorized, salary.Status);
    }

    // ── Granice stronicowania ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Page_below_one_is_clamped_instead_of_reaching_postgres(int page)
    {
        // `page = 0` dawalo `Skip(-pageSize)`, a na to Postgres odpowiada
        // "OFFSET must not be negative" — czyli 500 na adresie, ktory user moze wpisac recznie.
        var r = await ListHandler().HandleAsync(EmptyFilter(_budgetId), page, 2, "date", true, default);

        Assert.Equal(1, r.Page);
        Assert.Equal(2, r.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Page_size_below_one_is_clamped_instead_of_reaching_postgres(int pageSize)
    {
        // Ten sam problem od drugiej strony: "LIMIT must not be negative".
        var r = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, pageSize, "date", true, default);

        Assert.Equal(1, r.PageSize);
        Assert.Single(r.Items);
    }

    [Fact]
    public async Task Absurd_page_size_is_capped_so_one_request_cannot_pull_the_whole_budget()
    {
        var r = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 1_000_000, "date", true, default);

        Assert.Equal(200, r.PageSize);
        Assert.Equal(5, r.Total); // sam limit nie zmienia tego, ILE wierszy pasuje
    }

    [Fact]
    public async Task Empty_selection_with_neither_ids_nor_filter_touches_nothing()
    {
        var result = await BulkDeleteHandler().HandleAsync(
            new BulkDeleteRequestDto(new TransactionSelectionRequestDto(null, null)), default);

        Assert.Equal(0, result.Affected);
    }

    // ── Zasieg budzetu: identyfikatory od klienta nie moga wyjsc poza budzet ─────────────

    /// <summary>Drugi budzet z wlasna transakcja — material na proby wyjscia poza zasieg.</summary>
    private async Task<Guid> SeedOtherBudgetTransactionAsync()
    {
        var other = new Budget("Obcy", new DateOnly(2026, 2, 1), 0m, default);
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();

        var foreign = new Transaction(
                          new DateOnly(2026, 2, 10),
                          -999m,
                          "OBCA",
                          new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
                          TransactionStatus.Confirmed,
                          budgetBusinessId: other.BusinessId);
        _db.Transactions.Add(foreign);
        await _db.SaveChangesAsync();

        return foreign.BusinessId;
    }

    [Fact]
    public async Task Bulk_delete_refuses_an_id_from_another_budget_instead_of_silently_deleting_it()
    {
        var foreignId = await SeedOtherBudgetTransactionAsync();

        // Zasieg to filtr (budzet "Testowy"), a id wskazuje wiersz z budzetu "Obcy".
        var selection = new TransactionSelectionRequestDto([foreignId], EmptyFilter(_budgetId));

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            BulkDeleteHandler().HandleAsync(new BulkDeleteRequestDto(selection), default));

        var stillThere = await _db.Transactions.AsNoTracking()
            .AnyAsync(t => t.BusinessId == foreignId);
        Assert.True(stillThere);
    }

    [Fact]
    public async Task Bulk_action_with_an_unknown_id_reports_404_not_a_quiet_zero()
    {
        // Spojnosc z UpdateAsync: skoro klient NAZWAL wiersze, rozbieznosc jest bledem.
        var selection = new TransactionSelectionRequestDto([Guid.NewGuid()], EmptyFilter(_budgetId));

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            BulkSetCategoryHandler().HandleAsync(
                new BulkSetCategoryRequestDto(selection, _transportId), default));
    }

    [Fact]
    public async Task Edit_refuses_a_row_from_another_budget()
    {
        var foreignId = await SeedOtherBudgetTransactionAsync();
        var edit = new TransactionEditRequestDto(
            foreignId, new DateOnly(2026, 2, 10), "PRZEJETA", -1m, null);

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default));

        var untouched = await _db.Transactions.AsNoTracking()
            .FirstAsync(t => t.BusinessId == foreignId);
        Assert.Equal("OBCA", untouched.Description);
        Assert.Equal(-999m, untouched.Amount);
    }

    /// <summary>
    /// Kolumna `BudgetBusinessId` nie ma klucza obcego (swiadomie — patrz Transaction.cs), wiec
    /// baza nie broni przed wskazaniem nieistniejacego budzetu. Ten test jest jedyna siatka
    /// bezpieczenstwa, jaka po tej decyzji zostala: osierocony wiersz ma byc NIEWIDOCZNY,
    /// a nie wysadzac ekran.
    /// </summary>
    [Fact]
    public async Task A_dangling_budget_reference_is_invisible_and_does_not_blow_up_the_list()
    {
        _db.Transactions.Add(new Transaction(
                                 new DateOnly(2026, 3, 20),
                                 -7m,
                                 "SIEROTA",
                                 new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
                                 TransactionStatus.Confirmed,
                                 budgetBusinessId: Guid.NewGuid()));
        await _db.SaveChangesAsync();

        var r = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 20, null, true, default);

        Assert.Equal(5, r.Total);
        Assert.DoesNotContain(r.Items, i => i.Description == "SIEROTA");
        Assert.Equal(575m, r.Summary.TotalExpenses); // bez tych 7 zl
    }

    // ── Wyszukiwarka: fraza to dane, nie wzorzec ────────────────────────────────────────

    [Fact]
    public async Task Search_treats_percent_as_a_literal_character_not_a_wildcard()
    {
        _db.Transactions.AddRange(
            New(new DateOnly(2026, 3, 21), -10m, "ODSETKI 5% ROCZNIE", null, TransactionStatus.Confirmed, 6),
            New(new DateOnly(2026, 3, 22), -20m, "OPLATA 15 PLN", null, TransactionStatus.Confirmed, 7));
        await _db.SaveChangesAsync();

        // Bez ucieczki wzorzec "%5%%" znaczy po prostu "zawiera 5" — i wpada tu "15 PLN".
        var r = await ListHandler().HandleAsync(
            EmptyFilter(_budgetId) with { Search = "5%" }, 1, 20, null, true, default);

        Assert.Equal("ODSETKI 5% ROCZNIE", Assert.Single(r.Items).Description);
    }

    [Fact]
    public async Task Search_treats_underscore_as_a_literal_character_not_a_single_char_wildcard()
    {
        _db.Transactions.AddRange(
            New(new DateOnly(2026, 3, 23), -10m, "REF_123", null, TransactionStatus.Confirmed, 8),
            New(new DateOnly(2026, 3, 24), -20m, "REFX123", null, TransactionStatus.Confirmed, 9));
        await _db.SaveChangesAsync();

        var r = await ListHandler().HandleAsync(
            EmptyFilter(_budgetId) with { Search = "REF_1" }, 1, 20, null, true, default);

        Assert.Equal("REF_123", Assert.Single(r.Items).Description);
    }

    // ── Walidacja opisu ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Edit_with_a_blank_description_is_refused(string description)
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "LIDL");
        var edit = new TransactionEditRequestDto(
            target.BusinessId, target.Date, description, target.Amount, _jedzenieId);

        await Assert.ThrowsAsync<TransactionDescriptionRequiredException>(() =>
            UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default));

        var reloaded = await _db.Transactions.AsNoTracking()
            .FirstAsync(t => t.BusinessId == target.BusinessId);
        Assert.Equal("LIDL", reloaded.Description);
    }

    [Fact]
    public async Task Edit_with_an_overlong_description_is_refused_before_it_reaches_the_column()
    {
        // Bez tego varchar(500) rzuca DbUpdateException, ktorego nikt nie mapuje — czyli 500.
        var target = await _db.Transactions.FirstAsync(t => t.Description == "LIDL");
        var tooLong = new string('x', TransactionLimits.DescriptionMaxLength + 1);
        var edit = new TransactionEditRequestDto(
            target.BusinessId, target.Date, tooLong, target.Amount, _jedzenieId);

        await Assert.ThrowsAsync<TransactionDescriptionTooLongException>(() =>
            UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default));
    }

    [Fact]
    public async Task Edit_trims_the_description_so_padding_does_not_survive_the_save()
    {
        var target = await _db.Transactions.FirstAsync(t => t.Description == "LIDL");
        var edit = new TransactionEditRequestDto(
            target.BusinessId, target.Date, "  LIDL CENTRUM  ", target.Amount, _jedzenieId);

        var saved = Assert.Single(
            await UpdateHandler().HandleAsync(new UpdateTransactionsRequestDto([edit], [_budgetId]), default));

        Assert.Equal("LIDL CENTRUM", saved.Description);
    }

    // ── Kontrakt JSON ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enumy w CIELE zadania musza czytac sie z NAZW. Aplikacja nie rejestruje globalnie
    /// JsonStringEnumConverter, wiec bez atrybutu na typie filtr z akcji masowej ("direction":
    /// "All") konczyl sie 400 — a testy handlera tego nie widza, bo buduja obiekty wprost.
    /// </summary>
    [Fact]
    public void Selection_filter_deserialises_enums_from_their_names_not_numbers()
    {
        const string json = """
            {
              "ids": null,
              "filter": {
                "budgetId": null, "from": null, "to": null,
                "categoryId": null, "uncategorized": false,
                "direction": "Expense",
                "status": ["PendingReview", "Confirmed"],
                "amountFrom": null, "amountTo": null, "search": null
              }
            }
            """;

        // Te same opcje, ktorych uzywa minimalne API (camelCase z JsonSerializerDefaults.Web).
        var options = new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.Web);

        var selection = System.Text.Json.JsonSerializer
            .Deserialize<TransactionSelectionRequestDto>(json, options);

        Assert.NotNull(selection);
        Assert.Equal(TransactionDirection.Expense, selection.Filter!.Direction);
        Assert.Equal(
            [TransactionStatus.PendingReview, TransactionStatus.Confirmed],
            selection.Filter.Status!);
    }

    // ── Wiele budzetow naraz ─────────────────────────────────────────────────────────────

    /// <summary>Drugi budzet z wlasna transakcja, ale ZWROCONY razem z identyfikatorem budzetu.</summary>
    private async Task<(Guid BudgetId, Guid TransactionId)> SeedSecondBudgetAsync()
    {
        var other = new Budget("Drugi", new DateOnly(2026, 2, 1), 0m, default);
        _db.Budgets.Add(other);
        await _db.SaveChangesAsync();

        var row = new Transaction(
                      new DateOnly(2026, 2, 10),
                      -50m,
                      "Z DRUGIEGO",
                      new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
                      TransactionStatus.Confirmed,
                      budgetBusinessId: other.BusinessId);
        _db.Transactions.Add(row);
        await _db.SaveChangesAsync();

        return (other.BusinessId, row.BusinessId);
    }

    [Fact]
    public async Task Wiele_budzetow_liczy_sie_RAZEM()
    {
        var (secondBudgetId, _) = await SeedSecondBudgetAsync();

        var onlyFirst = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 50, null, true, default);
        var both = await ListHandler().HandleAsync(EmptyFilter(_budgetId, secondBudgetId), 1, 50, null, true, default);

        Assert.Equal(onlyFirst.Total + 1, both.Total);
        Assert.Equal(2, both.SelectedBudgetIds.Count);
    }

    [Fact]
    public async Task Kafle_tez_sumuja_po_wszystkich_wybranych_budzetach()
    {
        // Wiersz z drugiego budzetu to wydatek 50 zl — bez niego kafel wydatkow bylby o tyle nizszy.
        var (secondBudgetId, _) = await SeedSecondBudgetAsync();

        var onlyFirst = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 50, null, true, default);
        var both = await ListHandler().HandleAsync(EmptyFilter(_budgetId, secondBudgetId), 1, 50, null, true, default);

        Assert.Equal(onlyFirst.Summary.TotalExpenses + 50m, both.Summary.TotalExpenses);
    }

    [Fact]
    public async Task Powtorzony_identyfikator_nie_liczy_tych_samych_transakcji_dwa_razy()
    {
        // Adres da sie recznie zepsuc (?budgetId=X&budgetId=X). Podwojenie kafli byloby wtedy
        // bledem, ktorego nie widac — liczby wygladaja na prawdziwe, tylko sa dwa razy za duze.
        var once = await ListHandler().HandleAsync(EmptyFilter(_budgetId), 1, 50, null, true, default);
        var twice = await ListHandler().HandleAsync(EmptyFilter(_budgetId, _budgetId), 1, 50, null, true, default);

        Assert.Equal(once.Total, twice.Total);
        Assert.Equal(once.Summary.TotalExpenses, twice.Summary.TotalExpenses);
        Assert.Single(twice.SelectedBudgetIds);
    }

    [Fact]
    public async Task Nieznany_budzet_wsrod_znanych_to_404__nie_ciche_pominiecie()
    {
        // ⚠️ Przy wielu identyfikatorach ciche pominiecie jednego jest GORSZE niz przy jednym:
        // odpowiedz wyglada na kompletna, tylko brakuje w niej pieniedzy.
        var filter = EmptyFilter(_budgetId, Guid.NewGuid());

        await Assert.ThrowsAsync<BudgetNotFoundException>(
            () => ListHandler().HandleAsync(filter, 1, 20, null, true, default));
    }

    [Fact]
    public async Task Lista_niesie_budzety_do_multiselecta()
    {
        // Razem z lista, nie osobnym zadaniem: ekran i tak nie narysuje sie bez obu.
        await SeedSecondBudgetAsync();

        var r = await ListHandler().HandleAsync(EmptyFilter(), 1, 20, null, true, default);

        Assert.Equal(2, r.Budgets.Count);
        Assert.Contains(r.Budgets, b => b.Name == "Drugi");
    }

    [Fact]
    public async Task Akcja_masowa_obejmuje_wszystkie_wybrane_budzety()
    {
        // Zasieg akcji masowych MUSI isc tym samym filtrem co lista — inaczej "zaznacz
        // wszystkie pasujace" obejmowaloby inny zestaw, niz uzytkownik ma przed oczami.
        var (secondBudgetId, foreignId) = await SeedSecondBudgetAsync();

        var selection = new TransactionSelectionRequestDto(
            [foreignId], EmptyFilter(_budgetId, secondBudgetId));

        var result = await BulkSetCategoryHandler().HandleAsync(
                new BulkSetCategoryRequestDto(selection, _transportId), default);

        Assert.Equal(1, result.Affected);
    }

    [Fact]
    public async Task Akcja_masowa_NIE_siega_poza_wybrane_budzety()
    {
        var (_, foreignId) = await SeedSecondBudgetAsync();

        // Drugi budzet celowo POZA filtrem — identyfikator z niego ma byc nie do ruszenia.
        var selection = new TransactionSelectionRequestDto([foreignId], EmptyFilter(_budgetId));

        await Assert.ThrowsAsync<TransactionNotFoundException>(
            () => BulkSetCategoryHandler().HandleAsync(
                new BulkSetCategoryRequestDto(selection, _transportId), default));
    }
}
