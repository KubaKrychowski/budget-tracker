using BudgetTracker.Api.Features.Categorization;
using BudgetTracker.Api.Features.Categorization.Commands;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using BudgetTracker.Api.Features.Categorization.Queries;
using BudgetTracker.Api.Features.Categorization.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Wersjonowanie modelu i blokada na czas importu. Bez tego trening, ktory wyszedl gorzej,
/// jest nieodwracalny — <c>ml.Model.Save</c> nadpisywalo aktywny plik bez kopii.
///
/// Testy jada na PLIKACH (katalog tymczasowy), nie na atrapach: sprawdzana rzecz to wlasnie
/// zachowanie systemu plikow — podmiana, kopia, przetrwanie poprzedniej wersji.
/// </summary>
public sealed class ModelStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _activePath;
    private readonly FakeTimeProvider _clock;
    private readonly ModelStore _store;

    public ModelStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"bt-models-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _activePath = Path.Combine(_directory, "category-model.zip");

        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero));
        _store = new ModelStore(
            Options.Create(new CategorizationOptions { ModelPath = _activePath }), _clock);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Model to dla tych testow zwykly plik — liczy sie jego TRESC, nie to, czy ML.NET go wczyta.</summary>
    private string Staged(string content)
    {
        var path = _store.CreateStagingPath();
        File.WriteAllText(path, content);
        return path;
    }

    private static TrainingReportResponseDto Report(int rows) => new(rows, 5, 0.9, 0.8);

    // ── Protokol zapisu ─────────────────────────────────────────────────────────────────
    //
    // Publikacja i przywracanie ida PRZEZ blokade, bo inaczej sie nie da: `Publish`/`Activate`
    // zadaja `ModelWriteLease` jako argumentu. Wczesniej byly to publiczne metody bez zadnego
    // zabezpieczenia, a te testy wolaly je bez blokady — czyli udowadnialy, ze ochrone da sie
    // ominac. Teraz przechodza ta sama droga co produkcja.

    private TrainingHistoryEntry Publish(string content, TrainingReportResponseDto report)
    {
        using var lease = _store.TryBeginTraining()!;
        return _store.Publish(lease, Staged(content), report);
    }

    private void Activate(string version)
    {
        using var lease = _store.TryBeginTraining()!;
        _store.Activate(lease, version);
    }

    private IReadOnlyList<ModelVersionResponseDto> Versions() => _store.ReadCatalog().Versions;

    private TrainingHistoryEntry? ActiveTraining() => _store.ReadCatalog().ActiveTraining;

    [Fact]
    public void Publishing_replaces_the_active_model_and_keeps_the_previous_version()
    {
        Publish("model-1", Report(100));

        _clock.Advance(TimeSpan.FromMinutes(5));
        Publish("model-2", Report(120));

        // Aktywny to najnowszy...
        Assert.Equal("model-2", File.ReadAllText(_activePath));

        // ...ale poprzedni NIE znika. To jest cala roznica wobec zapisu w miejscu.
        var versions = Versions();
        Assert.Equal(2, versions.Count);
        Assert.Equal(["20260906100500", "20260906100000"], versions.Select(v => v.Version));
    }

    [Fact]
    public void Activating_an_older_version_brings_back_exactly_that_model()
    {
        Publish("model-1", Report(100));
        var first = Versions().Single().Version;

        _clock.Advance(TimeSpan.FromMinutes(5));
        Publish("model-2", Report(120));
        Assert.Equal("model-2", File.ReadAllText(_activePath));

        Activate(first);

        Assert.Equal("model-1", File.ReadAllText(_activePath));
        // Powrot dziala w obie strony — nic nie zostalo skasowane.
        Assert.Equal(2, Versions().Count);
    }

    [Fact]
    public void The_active_flag_follows_the_file_contents_not_a_remembered_pointer()
    {
        Publish("model-1", Report(100));
        var first = Versions().Single().Version;

        _clock.Advance(TimeSpan.FromMinutes(5));
        Publish("model-2", Report(120));
        Activate(first);

        var versions = Versions();
        Assert.True(versions.Single(v => v.Version == first).IsActive);
        Assert.All(versions.Where(v => v.Version != first), v => Assert.False(v.IsActive));
    }

    [Fact]
    public void Activating_an_unknown_version_is_a_not_found_not_a_silent_no_op()
    {
        Assert.Throws<ModelVersionNotFoundException>(() => Activate("20990101000000"));
    }

    [Fact]
    public void Training_never_writes_to_the_active_model_path()
    {
        // ⚠️ To jest sedno bezpiecznej podmiany: MlCategorizer czyta aktywny plik w trakcie
        // importu, wiec trening nie moze go dotykac, dopoki nie ma kompletnego wyniku.
        var staging = _store.CreateStagingPath();

        Assert.NotEqual(Path.GetFullPath(_activePath), Path.GetFullPath(staging));
        Assert.False(File.Exists(_activePath));
    }

    [Fact]
    public void Publishing_leaves_no_staging_leftovers()
    {
        Publish("model-1", Report(100));

        var leftovers = Directory
            .EnumerateFiles(Path.Combine(_directory, "models"), "staging-*")
            .ToList();
        Assert.Empty(leftovers);
    }

    // ── Blokada ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Training_is_refused_while_an_import_is_categorising()
    {
        using var import = _store.BeginImport();

        Assert.Null(_store.TryBeginTraining());
    }

    [Fact]
    public void Training_is_allowed_again_once_the_import_finishes()
    {
        using (_store.BeginImport())
        {
            Assert.Null(_store.TryBeginTraining());
        }

        using var lease = _store.TryBeginTraining();
        Assert.NotNull(lease);
    }

    [Fact]
    public void Two_trainings_do_not_run_at_once()
    {
        using var first = _store.TryBeginTraining();
        Assert.NotNull(first);

        Assert.Null(_store.TryBeginTraining());
    }

    [Fact]
    public void Releasing_a_lease_twice_does_not_free_it_twice()
    {
        // Podwojne Dispose nie moze odblokowac czegos, co w miedzyczasie zajal ktos inny.
        var lease = _store.TryBeginTraining()!;
        lease.Dispose();

        using var second = _store.TryBeginTraining();
        Assert.NotNull(second);

        lease.Dispose();
        Assert.Null(_store.TryBeginTraining());
    }

    [Fact]
    public void History_survives_a_corrupted_file_the_versions_are_still_listed()
    {
        Publish("model-1", Report(100));
        File.WriteAllText(Path.Combine(_directory, "models", "history.json"), "{ to nie jest json");

        // Historia to METADANE — jej uszkodzenie nie moze zablokowac ekranu ani powrotu
        // do wczesniejszego modelu, bo pliki modeli sa nienaruszone.
        var versions = Versions();

        Assert.Single(versions);
        Assert.Null(versions[0].Report);
        Assert.Null(ActiveTraining());
    }

    [Fact]
    public void History_carries_the_metrics_of_the_training_that_produced_each_version()
    {
        Publish("model-1", new TrainingReportResponseDto(1224, 18, 0.91, 0.72));

        var version = Assert.Single(Versions());
        Assert.Equal(1224, version.Report!.Rows);
        Assert.Equal(0.72, version.Report.MacroAccuracy);
        Assert.Equal(_clock.GetUtcNow(), ActiveTraining()!.TrainedAt);
    }

    [Fact]
    public void The_model_that_predates_versioning_is_archived_before_the_first_publish()
    {
        // ⚠️ Bez tego pierwszy trening podmienilby model sprzed wersjonowania bezpowrotnie —
        // czyli dokladnie ta nieodwracalnosc, dla ktorej wersjonowanie powstalo.
        File.WriteAllText(_activePath, "model-sprzed-wersjonowania");

        Publish("model-nowy", Report(100));

        var versions = Versions();
        Assert.Equal(2, versions.Count);

        // Stary da sie odzyskac i jest DOKLADNIE tym, co bylo.
        var old = versions.Single(v => !v.IsActive);
        Activate(old.Version);
        Assert.Equal("model-sprzed-wersjonowania", File.ReadAllText(_activePath));
    }

    [Fact]
    public void An_archived_pre_history_model_has_no_invented_metrics()
    {
        File.WriteAllText(_activePath, "model-sprzed-wersjonowania");
        Publish("model-nowy", Report(100));

        // Nie wiadomo, na czym sie uczyl — wersja bez raportu jest uczciwsza niz zmyslone liczby.
        Assert.Null(Versions().Single(v => !v.IsActive).Report);
    }

    [Fact]
    public void Archiving_happens_once_not_on_every_publish()
    {
        File.WriteAllText(_activePath, "model-sprzed-wersjonowania");

        Publish("model-1", Report(100));
        _clock.Advance(TimeSpan.FromMinutes(5));
        Publish("model-2", Report(120));

        // Stary + dwa wytrenowane. Gdyby archiwizacja powtarzala sie przy kazdym zapisie,
        // katalog puchlby o kopie tego samego pliku.
        Assert.Equal(3, Versions().Count);
    }

    [Fact]
    public void After_a_rollback_the_reference_point_is_the_restored_model_not_the_newest_training()
    {
        // ⚠️ Roznica widoczna dopiero po cofnieciu: najnowszy wpis historii opisuje wtedy model,
        // ktory LEZY na dysku, ale nie dziala. Gdyby ekran liczyl "przybylo N poprawek" od tamtej
        // chwili, powiedzialby "model jest aktualny" o modelu, ktory tych poprawek nie widzial.
        Publish("model-1", new TrainingReportResponseDto(1000, 20, 0.8, 0.7));
        var first = Versions().Single().Version;

        _clock.Advance(TimeSpan.FromMinutes(5));
        Publish("model-2", new TrainingReportResponseDto(1267, 25, 0.9, 0.85));

        Assert.Equal(1267, ActiveTraining()!.Report.Rows);

        Activate(first);

        Assert.Equal(1000, ActiveTraining()!.Report.Rows);
    }

    [Fact]
    public void A_model_with_no_history_entry_reports_no_active_training()
    {
        // Model sprzed wersjonowania: plik jest, ale nie wiadomo, na czym sie uczyl —
        // wiec punktem odniesienia staje sie plik bazowy, a nie zmyslona data.
        File.WriteAllText(_activePath, "model-sprzed-wersjonowania");
        Publish("model-nowy", Report(100));
        Activate(Versions().Single(v => v.Report is null).Version);

        Assert.Null(ActiveTraining());
    }

    // ── Blokada wymuszona przez TYP, nie przez pamiec wolajacego ────────────────────────

    [Fact]
    public void Publishing_without_a_lease_does_not_compile_away__it_is_refused_at_runtime_too()
    {
        // ⚠️ Sygnatura wymusza przepustke, ale samo jej ISTNIENIE nie wystarczy: zwolniona
        // przepustka to juz nie blokada, a mimo to nadal jest obiektem, ktorym da sie machac.
        var lease = _store.TryBeginTraining()!;
        var staging = Staged("model-1");
        lease.Dispose();

        Assert.Throws<InvalidOperationException>(() => _store.Publish(lease, staging, Report(100)));
    }

    [Fact]
    public void A_lease_from_another_store_is_refused()
    {
        // Bez sprawdzenia wlasciciela „przepustka" bylaby dekoracja w sygnaturze — wystarczyloby
        // powolac obcy ModelStore i wziac blokade z niego.
        var otherDirectory = Path.Combine(Path.GetTempPath(), $"bt-other-{Guid.NewGuid():N}");
        Directory.CreateDirectory(otherDirectory);
        try
        {
            var other = new ModelStore(
                Options.Create(new CategorizationOptions
                {
                    ModelPath = Path.Combine(otherDirectory, "category-model.zip"),
                }),
                _clock);

            using var foreignLease = other.TryBeginTraining()!;
            var staging = _store.CreateStagingPath();
            File.WriteAllText(staging, "model-1");

            Assert.Throws<InvalidOperationException>(
                () => _store.Publish(foreignLease, staging, Report(100)));
        }
        finally
        {
            Directory.Delete(otherDirectory, recursive: true);
        }
    }

    [Fact]
    public void Restoring_with_a_released_lease_is_refused()
    {
        Publish("model-1", Report(100));
        var version = Versions().Single().Version;

        var lease = _store.TryBeginTraining()!;
        lease.Dispose();

        Assert.Throws<InvalidOperationException>(() => _store.Activate(lease, version));
    }

    [Fact]
    public void Reading_the_catalog_needs_no_lease__it_only_reads()
    {
        // Odczyt musi dzialac takze w trakcie treningu — ekran „Dane treningowe" nie moze
        // sie zablokowac tylko dlatego, ze ktos wlasnie douczyl model.
        Publish("model-1", Report(100));

        using var lease = _store.TryBeginTraining();
        Assert.NotNull(lease);

        Assert.Single(_store.ReadCatalog().Versions);
    }
}
