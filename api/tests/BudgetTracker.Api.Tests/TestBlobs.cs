using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace BudgetTracker.Api.Tests;

/// <summary>
/// Kontenery testowe w Azurite — jedna droga dla wszystkich klas testowych.
/// </summary>
/// <remarks>
/// <para>
/// Testy jadą na PRAWDZIWYM emulatorze, tak samo jak na prawdziwym Postgresie: model i zbiór treningowy
/// żyją dziś w blobie, więc atrapa klienta sprawdzałaby wyłącznie to, że kod woła metody, które kod woła.
/// Wymaga wstającego <c>docker compose up -d azurite</c>.
/// </para>
/// <para>
/// ⚠️ Adres to <c>localhost</c>, nie <c>127.0.0.1</c>: kontener chodzi po HTTPS z certyfikatem deweloperskim
/// .NET, wystawionym na nazwę, nie na adres IP. Z IP połączenie odpada na weryfikacji nazwy w certyfikacie.
/// </para>
/// <para>
/// Klucz konta jest stałą emulatora opublikowaną w dokumentacji Microsoftu — nie jest sekretem
/// i nie otwiera niczego poza lokalnym kontenerem.
/// </para>
/// </remarks>
internal static class TestBlobs
{
    private const string ConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=https://localhost:10000/devstoreaccount1;";

    public static BlobServiceClient Client() => new(ConnectionString);

    /// <summary>Tworzy pusty kontener o unikalnej nazwie i zwraca ją.</summary>
    /// <remarks>
    /// Nazwa per klasa testowa, bo kontenery nie mogą się przenikać między przypadkami — ta sama zasada
    /// co przy bazach <c>budgettracker_&lt;cos&gt;_test</c>. Reguły Azure: małe litery, cyfry i myślniki.
    /// </remarks>
    public static async Task<string> CreateContainerAsync(string prefix)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}";
        await Client().GetBlobContainerClient(name).CreateIfNotExistsAsync();
        return name;
    }

    /// <summary>Kontener o z gory znanej nazwie — <c>ModelStore</c> adresuje go identyfikatorem uzytkownika.</summary>
    public static async Task CreateNamedContainerAsync(string name) =>
        await Client().GetBlobContainerClient(name).CreateIfNotExistsAsync();

    public static async Task DropContainerAsync(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            await Client().GetBlobContainerClient(name).DeleteIfExistsAsync();
        }
        catch (RequestFailedException)
        {
            // Sprzątanie po teście nie ma prawa przesłonić wyniku samego testu.
        }
    }

    public static async Task UploadTextAsync(string container, string blobName, string content)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await Client().GetBlobContainerClient(container)
            .GetBlobClient(blobName)
            .UploadAsync(stream, overwrite: true);
    }

    /// <summary>Wgrywa bajty i zwraca ETag — tozsamosc wersji, ktora trafia do wiersza ModelVersion.</summary>
    public static async Task<string> UploadStreamAsync(string container, string blobName, Stream content)
    {
        content.Position = 0;
        var response = await Client().GetBlobContainerClient(container)
            .GetBlobClient(blobName)
            .UploadAsync(content, overwrite: true);

        return response.Value.ETag.ToString();
    }

    /// <summary>Strumien z zawartoscia blobu — do testow, ktore ogladaja sam model, z pominieciem kodu aplikacji.</summary>
    public static async Task<Stream> OpenReadAsync(string container, string blobName) =>
        (await Client().GetBlobContainerClient(container).GetBlobClient(blobName).DownloadContentAsync())
            .Value.Content.ToStream();

    public static async Task<bool> ExistsAsync(string container, string blobName) =>
        await Client().GetBlobContainerClient(container).GetBlobClient(blobName).ExistsAsync();

    /// <summary>Konfiguracja wskazująca kodowi kontener i nazwy blobów, których ma użyć w teście.</summary>
    public static IConfiguration Configuration(string container, string trainingSetName = "training-set.csv") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:SharedContainerName"] = container,
                ["Storage:TrainingSetName"] = trainingSetName,
                ["Formats:Version"] = "yyyyMMddHHmmss",
            })
            .Build();
}
