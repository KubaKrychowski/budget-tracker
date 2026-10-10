using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BudgetTracker.ErrorReporting;

/// <summary>Kolejka zgłoszeń 500. Wrzucenie do niej nie blokuje odpowiedzi użytkownikowi.</summary>
public sealed class ErrorReportQueue
{
    private readonly Channel<ErrorReport> _channel =
        Channel.CreateBounded<ErrorReport>(new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropWrite });

    public bool TryEnqueue(ErrorReport report) => _channel.Writer.TryWrite(report);

    public ChannelReader<ErrorReport> Reader => _channel.Reader;
}

/// <summary>
/// Tło: bierze zgłoszenia z <see cref="ErrorReportQueue"/> i zakłada issue na GitHubie.
/// </summary>
/// <remarks>
/// ⚠️ Deduplikacja ma dwa poziomy: pamięć procesu (okno <see cref="ErrorReportingOptions.CooldownMinutes"/>)
/// i wyszukanie OTWARTEGO issue ze znacznikiem odcisku. Samo wyszukiwanie nie wystarcza — indeks GitHuba
/// ma opóźnienie, więc burza identycznych 500 zdążyłaby założyć wiele issues.
/// Awaria zgłaszania nigdy nie wraca do użytkownika ani nie wywala hosta — jest tylko logowana.
/// </remarks>
public sealed class GitHubIssueReporter(
    ErrorReportQueue queue,
    IHttpClientFactory httpFactory,
    IOptions<ErrorReportingOptions> options,
    ILogger<GitHubIssueReporter> logger) : BackgroundService
{
    public const string HttpClientName = "github-error-reporting";

    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var report in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ReportAsync(report, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nie udało się założyć issue o błędzie 500");
            }
        }
    }

    public async Task ReportAsync(ErrorReport report, CancellationToken ct)
    {
        var opt = options.Value;
        var fingerprint = ErrorFingerprint.Compute(report.Exception);
        var now = DateTimeOffset.UtcNow;

        if (_recent.TryGetValue(fingerprint, out var last) && now - last < TimeSpan.FromMinutes(opt.CooldownMinutes))
            return;
        _recent[fingerprint] = now;

        var http = httpFactory.CreateClient(HttpClientName);

        if (await OpenIssueExistsAsync(http, opt, fingerprint, ct)) return;

        using var response = await http.PostAsJsonAsync(
            $"repos/{opt.Repository}/issues",
            new
            {
                title = IssueBuilder.Title(report, fingerprint),
                body = IssueBuilder.Body(report, fingerprint),
                labels = opt.Labels,
            },
            ct);

        if (!response.IsSuccessStatusCode)
        {
            _recent.TryRemove(fingerprint, out _);
            logger.LogWarning("GitHub odrzucił założenie issue: {Status}", (int)response.StatusCode);
        }
    }

    private static async Task<bool> OpenIssueExistsAsync(
        HttpClient http, ErrorReportingOptions opt, string fingerprint, CancellationToken ct)
    {
        var query = Uri.EscapeDataString($"repo:{opt.Repository} is:issue is:open in:body \"{IssueBuilder.Marker(fingerprint)}\"");
        using var response = await http.GetAsync($"search/issues?q={query}&per_page=1", ct);
        if (!response.IsSuccessStatusCode) return false;

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return json.RootElement.TryGetProperty("total_count", out var count) && count.GetInt32() > 0;
    }
}
