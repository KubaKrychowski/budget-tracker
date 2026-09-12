using System.Globalization;
using System.Text.Json;
using BudgetTracker.Api.Features.Categorization.Contracts;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Categorization.Models;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Api.Features.Categorization.Services;

/// <summary>
/// Modele na dysku: wersjonowanie, atomowa podmiana aktywnego pliku i historia treningów.
/// </summary>
/// <remarks>
/// Singleton, bo trzyma BLOKADĘ (patrz <see cref="TryBeginTraining"/>) — stan współdzielony
/// między żądaniami, którego nie da się odtworzyć per scope.
///
/// Historia i pliki żyją na dysku, nie w bazie: przy single-user to wystarcza (plan,
/// „Out of scope"), a model i tak jest artefaktem, nie źródłem.
///
/// ⚠️ <b>Blokada na czas importu.</b> <see cref="MlCategorizer"/> czyta model przez <c>File.OpenRead</c>
/// w trakcie kategoryzacji importu, a trening zapisuje pod tę samą ścieżkę. Sama atomowa podmiana chroni
/// czytelnika przed plikiem zapisanym w połowie, ale nie chroni SENSU importu: połowa wyciągu
/// skategoryzowana starym modelem, połowa nowym, bez żadnego śladu, że tak się stało.
/// Dlatego trening w trakcie importu jest ODMAWIANY (409), a nie kolejkowany.
/// </remarks>
public sealed class ModelStore(IOptions<CategorizationOptions> options, TimeProvider clock)
{
    private readonly string _activePath = options.Value.ModelPath;

    /// <summary>Katalog na wersje — obok aktywnego modelu, żeby cała historia była w jednym miejscu.</summary>
    private string VersionsDirectory =>
        Path.Combine(Path.GetDirectoryName(_activePath) ?? ".", "models");

    private string HistoryPath => Path.Combine(VersionsDirectory, "history.json");

    private string PathForVersion(string version) =>
        Path.Combine(VersionsDirectory, $"category-model-{version}.zip");

    /// <summary>Format nazwy wersji — sortowanie leksykalne = sortowanie chronologiczne.</summary>
    private const string VersionFormat = "yyyyMMddHHmmss";

    private int _importsInFlight;
    private int _trainingInFlight;

    /// <summary>
    /// Znacznik „trwa kategoryzacja importu". Wołany z <c>GetImportPreviewQueryHandler</c> wokół całej pętli,
    /// nie wokół pojedynczej predykcji — chodzi o spójność całego pliku, nie jednego wiersza.
    /// </summary>
    /// <remarks>
    /// ⚠️ Blokada jest JEDNOSTRONNA i to nie jest niedopatrzenie: import ROZPOCZĘTY w trakcie
    /// treningu jest bezpieczny, bo <c>MlCategorizer</c> jest scope'owany i wczytuje model raz
    /// na żądanie (<c>EnsureLoadedAsync</c> zapamiętuje silnik). Podmiana pliku w trakcie takiego
    /// importu nie ma jak go dosięgnąć — czyta z pamięci, nie z dysku. Odwrotny kierunek trzeba
    /// blokować, bo tam import mógłby wczytać model dopiero PO podmianie.
    /// </remarks>
    public IDisposable BeginImport()
    {
        Interlocked.Increment(ref _importsInFlight);
        return new Scope(() => Interlocked.Decrement(ref _importsInFlight));
    }

    /// <summary>
    /// <c>null</c>, gdy trening jest w tej chwili niedopuszczalny — trwa import albo inny
    /// trening. Wynik trzeba zwolnić (<c>using</c>), inaczej blokada zostaje na zawsze.
    /// </summary>
    /// <remarks>
    /// Zwrócony <see cref="ModelWriteLease"/> jest jednocześnie PRZEPUSTKĄ do
    /// <see cref="Publish"/> i <see cref="Activate"/> — obie żądają go jako argumentu,
    /// więc nie da się ich wywołać bez trzymanej blokady. To celowe: bez tego jedyną
    /// ochroną byłaby pamięć autora następnego wywołania.
    ///
    /// Import mógł wystartować między sprawdzeniem a przejęciem blokady. Kolejność
    /// „przejmij, sprawdź jeszcze raz, w razie czego oddaj" zamyka to okno bez zamka.
    /// </remarks>
    public ModelWriteLease? TryBeginTraining()
    {
        if (Volatile.Read(ref _importsInFlight) > 0) return null;
        if (Interlocked.CompareExchange(ref _trainingInFlight, 1, 0) != 0) return null;

        if (Volatile.Read(ref _importsInFlight) > 0)
        {
            Volatile.Write(ref _trainingInFlight, 0);
            return null;
        }

        return new ModelWriteLease(this, () => Volatile.Write(ref _trainingInFlight, 0));
    }

    /// <summary>
    /// Sprawdza, że przepustka jest ważna i wystawiona przez TEN store. Bez tego
    /// „przepustka" byłaby wyłącznie dekoracją w sygnaturze: dałoby się ją zwolnić
    /// i dalej nią machać, albo podłożyć cudzą.
    /// </summary>
    private void RequireLease(ModelWriteLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!ReferenceEquals(lease.Owner, this) || lease.Released)
        {
            throw new InvalidOperationException(
                "Zapis modelu wymaga aktualnej blokady z TryBeginTraining tego samego ModelStore.");
        }
    }

    /// <summary>Znacznik importu zwalniany dokładnie raz, także przy podwójnym <c>Dispose</c>.</summary>
    private sealed class Scope(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose();
        }
    }

    /// <summary>
    /// Ścieżka pliku tymczasowego, do którego trening ma pisać. Nigdy nie jest to plik
    /// aktywny — zapis „w miejscu" zostawiłby po awarii model ucięty w połowie, a aplikacja
    /// wczytałaby go przy następnym imporcie.
    /// </summary>
    public string CreateStagingPath()
    {
        Directory.CreateDirectory(VersionsDirectory);
        return Path.Combine(VersionsDirectory, $"staging-{Guid.NewGuid():N}.zip");
    }

    /// <summary>
    /// Archiwizuje świeżo wytrenowany plik jako nową wersję i podmienia nim model aktywny.
    /// Podmiana idzie przez <c>File.Move(overwrite: true)</c> — na tym samym wolumenie jest
    /// atomowa, więc czytelnik widzi albo cały stary model, albo cały nowy, nigdy połowę.
    /// </summary>
    /// <remarks>
    /// ⚠️ NAJPIERW zabezpiecza to, co jest teraz aktywne (<see cref="ArchiveActiveIfUnknown"/>). Model sprzed
    /// wprowadzenia wersjonowania nie ma swojego pliku w <c>models/</c>, więc bez tego kroku pierwszy trening
    /// podmieniłby go bezpowrotnie — czyli dokładnie ta nieodwracalność, dla której to wersjonowanie powstało.
    /// </remarks>
    public TrainingHistoryEntry Publish(ModelWriteLease lease, string stagingPath, TrainingReportResponseDto report)
    {
        RequireLease(lease);

        ArchiveActiveIfUnknown();

        var now = clock.GetUtcNow();
        var version = now.UtcDateTime.ToString(VersionFormat, CultureInfo.InvariantCulture);
        var versionPath = PathForVersion(version);

        Directory.CreateDirectory(VersionsDirectory);
        File.Move(stagingPath, versionPath, overwrite: true);

        ActivateFile(versionPath);

        var entry = new TrainingHistoryEntry(version, now, report);
        AppendHistory(entry);
        return entry;
    }

    /// <summary>
    /// Odkłada aktywny model jako wersję, jeśli nie odpowiada mu żaden znany plik.
    /// </summary>
    /// <remarks>
    /// Dotyczy modelu, który powstał PRZED wprowadzeniem wersjonowania (albo został podłożony
    /// ręcznie): pliku w <c>models/</c> nie ma, więc z punktu widzenia historii on nie istnieje —
    /// a jest tym, który właśnie działa. Wpis historii nie powstaje, bo nie wiadomo, na czym
    /// się uczył; wersja bez raportu jest uczciwsza niż zmyślone metryki.
    ///
    /// Nazwa wersji bierze się z czasu MODYFIKACJI pliku, nie z „teraz": to jest data powstania tamtego
    /// modelu, a nie chwili, w której go zauważyliśmy.
    /// </remarks>
    private void ArchiveActiveIfUnknown()
    {
        if (!File.Exists(_activePath)) return;
        if (ResolveActiveVersion() is not null) return;

        Directory.CreateDirectory(VersionsDirectory);

        var version = File.GetLastWriteTimeUtc(_activePath)
            .ToString(VersionFormat, CultureInfo.InvariantCulture);

        var target = PathForVersion(version);
        if (!File.Exists(target)) File.Copy(_activePath, target);
    }

    /// <summary>Przywraca wskazaną wersję jako aktywną. Nic nie kasuje — powrót działa w obie strony.</summary>
    public void Activate(ModelWriteLease lease, string version)
    {
        RequireLease(lease);

        var path = PathForVersion(version);
        if (!File.Exists(path)) throw new ModelVersionNotFoundException(version);

        ActivateFile(path);
    }

    /// <summary>Kopiuje wersję do pliku obok aktywnego, potem podmienia go atomowo.</summary>
    /// <remarks>
    /// Kopiowanie WPROST na ścieżkę aktywnego modelu dałoby okno, w którym plik aktywny jest niekompletny.
    /// </remarks>
    private void ActivateFile(string versionPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_activePath)!);

        var temp = _activePath + ".swap";
        File.Copy(versionPath, temp, overwrite: true);
        File.Move(temp, _activePath, overwrite: true);
    }

    /// <summary>
    /// Wszystko, czego ekran potrzebuje o modelach, w JEDNYM przejściu po dysku.
    /// </summary>
    /// <remarks>
    /// ⚠️ Rozdzielone <c>ListVersions()</c> i <c>ActiveTraining()</c> liczyły to samo dwa razy na jedno
    /// żądanie: każde z nich niezależnie hashowało cały aktywny model i przeglądało cały
    /// katalog wersji. Katalog nigdy nie jest sprzątany, więc ten koszt rósł z każdym treningiem
    /// — i był płacony podwójnie za każdym wejściem na zakładkę.
    ///
    /// ⚠️ <see cref="ModelCatalog.ActiveTraining"/> to trening, który wyprodukował AKTYWNY model — nie najnowszy
    /// w historii. Różnica ujawnia się po przywróceniu wcześniejszej wersji: najnowszy wpis opisuje wtedy model,
    /// który LEŻY na dysku, ale nie działa. Liczenie „przybyło N poprawek" od tamtej chwili mówiłoby
    /// „model jest aktualny" o modelu, który tych poprawek nie widział.
    /// </remarks>
    public ModelCatalog ReadCatalog()
    {
        if (!Directory.Exists(VersionsDirectory)) return new ModelCatalog([], null);

        var history = ReadHistory().ToDictionary(h => h.Version);
        var activeVersion = ResolveActiveVersion();

        var versions = Directory
            .EnumerateFiles(VersionsDirectory, "category-model-*.zip")
            .Select(path => Path.GetFileNameWithoutExtension(path)["category-model-".Length..])
            .Where(v => v.Length == VersionFormat.Length)
            .OrderByDescending(v => v, StringComparer.Ordinal)
            .Select(v => new ModelVersionResponseDto(
                v,
                history.TryGetValue(v, out var h) ? h.TrainedAt : ParseVersion(v),
                v == activeVersion,
                history.TryGetValue(v, out var r) ? r.Report : null))
            .ToList();

        var activeTraining = activeVersion is not null && history.TryGetValue(activeVersion, out var a)
            ? a
            : null;

        return new ModelCatalog(versions, activeTraining);
    }

    /// <summary>
    /// Która wersja jest aktywna — po ZAWARTOŚCI, nie po zapamiętanym wskaźniku. Plik aktywny
    /// da się podmienić spoza aplikacji, a wtedy zapamiętana nazwa kłamałaby na ekranie.
    /// </summary>
    /// <remarks>
    /// Skrót aktywnego pliku i jego rozmiar liczymy RAZ, przed pętlą — wcześniej <c>FileInfo</c>
    /// na nim powstawał od nowa dla każdego kandydata. Kandydaci są najpierw porównywani rozmiarem
    /// (jedno <c>stat</c>), dopiero potem skrótem — pliki różnej długości nie mają po co być czytane w całości.
    /// </remarks>
    private string? ResolveActiveVersion()
    {
        if (!File.Exists(_activePath)) return null;

        var activeLength = new FileInfo(_activePath).Length;
        var activeHash = HashOf(_activePath);

        return Directory
            .EnumerateFiles(VersionsDirectory, "category-model-*.zip")
            .Where(p => new FileInfo(p).Length == activeLength)
            .FirstOrDefault(p => HashOf(p) == activeHash)
            is { } match
            ? Path.GetFileNameWithoutExtension(match)["category-model-".Length..]
            : null;
    }

    private static string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
    }

    private static DateTimeOffset ParseVersion(string version) =>
        DateTimeOffset.TryParseExact(
            version, VersionFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    /// <summary>Historia treningów z dysku.</summary>
    /// <remarks>
    /// Uszkodzona historia nie może zablokować ekranu ani treningu — to metadane, nie dane — więc zamiast
    /// wyjątku zwracana jest pusta lista. Pliki modeli i tak zostają, więc lista wersji będzie kompletna.
    /// </remarks>
    private List<TrainingHistoryEntry> ReadHistory()
    {
        if (!File.Exists(HistoryPath)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<TrainingHistoryEntry>>(
                File.ReadAllText(HistoryPath)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void AppendHistory(TrainingHistoryEntry entry)
    {
        var history = ReadHistory();
        history.Add(entry);

        Directory.CreateDirectory(VersionsDirectory);
        var temp = HistoryPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(history));
        File.Move(temp, HistoryPath, overwrite: true);
    }
}
