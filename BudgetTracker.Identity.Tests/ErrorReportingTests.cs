using System.Net;
using System.Text;
using BudgetTracker.ErrorReporting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Zakładanie issues przy 500: maskowanie danych, odcisk do deduplikacji, treść issue i zachowanie handlera.
/// Kod jest współdzielony z API (<c>shared/ErrorReporting</c>), więc testy żyją tu — bez bazy i bez Dockera.
/// </summary>
public sealed class ErrorReportingTests
{
    private static Exception Thrown(string message = "boom")
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception ex) { return ex; }
    }

    private static ErrorReport Report(Exception ex, IReadOnlyList<LogLine>? logs = null) =>
        new(DateTimeOffset.UtcNow, "api", "POST", "/api/import", "trace1", ex, logs ?? []);

    [Theory]
    [InlineData("jan.kowalski@example.com", "jan.kowalski@example.com")]
    [InlineData("Authorization: Bearer abc.def-123", "abc.def-123")]
    [InlineData("Host=db;Password=tajne123;Port=5432", "tajne123")]
    [InlineData("token=sekret", "sekret")]
    [InlineData("konto PL61 1090 1014 0000 0712 1981 2874", "1090 1014")]
    [InlineData("karta 4111111111111111", "4111111111111111")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln", "eyJhbGciOiJIUzI1NiJ9")]
    public void Sanitize_removes_sensitive_values(string input, string leaked)
    {
        var result = ErrorMasker.Sanitize(input);

        Assert.DoesNotContain(leaked, result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void Sanitize_keeps_ordinary_text()
    {
        const string text = "Nie znaleziono budżetu 42 w żądaniu importu";

        Assert.Equal(text, ErrorMasker.Sanitize(text));
    }

    [Fact]
    public void Fingerprint_ignores_the_exception_message()
    {
        Assert.Equal(ErrorFingerprint.Compute(Thrown("a")), ErrorFingerprint.Compute(Thrown("b")));
    }

    [Fact]
    public void Fingerprint_differs_for_different_exception_types()
    {
        Exception other;
        try { throw new ArgumentException("x"); }
        catch (Exception ex) { other = ex; }

        Assert.NotEqual(ErrorFingerprint.Compute(Thrown()), ErrorFingerprint.Compute(other));
    }

    [Fact]
    public void Issue_contains_stack_trace_logs_and_marker_but_no_secrets()
    {
        var ex = Thrown("zły wpis dla jan@example.com");
        var logs = new[]
        {
            new LogLine(DateTimeOffset.UtcNow, LogLevel.Warning, "Import", "token=sekret123", "trace1"),
        };
        var fp = ErrorFingerprint.Compute(ex);

        var body = IssueBuilder.Body(Report(ex, logs), fp);
        var title = IssueBuilder.Title(Report(ex, logs), fp);

        Assert.Contains("InvalidOperationException", body);
        Assert.Contains(IssueBuilder.Marker(fp), body);
        Assert.Contains("POST /api/import", body);
        Assert.DoesNotContain("jan@example.com", body + title);
        Assert.DoesNotContain("sekret123", body);
        Assert.True(title.Length <= 130);
    }

    [Fact]
    public void Triple_backticks_in_a_log_line_cannot_close_the_code_block()
    {
        var logs = new[] { new LogLine(DateTimeOffset.UtcNow, LogLevel.Error, "X", "``` ## wstrzyknięte", null) };

        var body = IssueBuilder.Body(Report(Thrown(), logs), "abcdef123456");

        Assert.Equal(4, body.Split("```").Length - 1);
    }

    [Fact]
    public void Log_buffer_returns_only_lines_of_the_requested_trace()
    {
        var buffer = new RecentLogBuffer();
        buffer.Add(new LogLine(DateTimeOffset.UtcNow, LogLevel.Information, "A", "moje", "t1"));
        buffer.Add(new LogLine(DateTimeOffset.UtcNow, LogLevel.Information, "A", "cudze", "t2"));

        var lines = buffer.For("t1", 10);

        Assert.Single(lines);
        Assert.Equal("moje", lines[0].Message);
    }

    [Fact]
    public void Log_buffer_drops_the_oldest_lines_beyond_capacity()
    {
        var buffer = new RecentLogBuffer();
        for (var i = 0; i < RecentLogBuffer.Capacity + 10; i++)
            buffer.Add(new LogLine(DateTimeOffset.UtcNow, LogLevel.Warning, "A", $"m{i}", null));

        var lines = buffer.For(null, int.MaxValue);

        Assert.DoesNotContain(lines, l => l.Message == "m0");
    }

    private static ErrorReportingExceptionHandler Handler(ErrorReportQueue queue, string? token) =>
        new(queue, new RecentLogBuffer(), MsOptions.Create(new ErrorReportingOptions { Token = token }));

    [Fact]
    public async Task Handler_enqueues_the_report_and_lets_the_exception_pass_on()
    {
        var queue = new ErrorReportQueue();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/x";
        context.Request.QueryString = new QueryString("?secret=1");

        var handled = await Handler(queue, "tok").TryHandleAsync(context, Thrown(), default);

        Assert.False(handled);
        Assert.True(queue.Reader.TryRead(out var report));
        Assert.Equal("/api/x", report.Path);
    }

    [Fact]
    public async Task Handler_does_nothing_without_a_token()
    {
        var queue = new ErrorReportQueue();

        await Handler(queue, null).TryHandleAsync(new DefaultHttpContext(), Thrown(), default);

        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Handler_ignores_a_request_aborted_by_the_client()
    {
        var queue = new ErrorReportQueue();
        using var cts = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };
        await cts.CancelAsync();

        await Handler(queue, "tok").TryHandleAsync(context, new OperationCanceledException(), default);

        Assert.False(queue.Reader.TryRead(out _));
    }

    private sealed class FakeGitHub(int existingOpenIssues) : HttpMessageHandler
    {
        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                Posts.Add(await request.Content!.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"total_count\":{existingOpenIssues}}}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.github.com/") };
    }

    private static GitHubIssueReporter Reporter(FakeGitHub github) =>
        new(new ErrorReportQueue(), new FakeFactory(github),
            MsOptions.Create(new ErrorReportingOptions { Token = "tok" }),
            NullLogger<GitHubIssueReporter>.Instance);

    [Fact]
    public async Task Reporter_creates_one_issue_for_a_repeated_error()
    {
        var github = new FakeGitHub(existingOpenIssues: 0);
        var reporter = Reporter(github);
        var ex = Thrown();

        await reporter.ReportAsync(Report(ex), default);
        await reporter.ReportAsync(Report(ex), default);

        Assert.Single(github.Posts);
    }

    [Fact]
    public async Task Reporter_skips_an_error_that_already_has_an_open_issue()
    {
        var github = new FakeGitHub(existingOpenIssues: 1);

        await Reporter(github).ReportAsync(Report(Thrown()), default);

        Assert.Empty(github.Posts);
    }
}
