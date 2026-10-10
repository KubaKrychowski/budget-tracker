using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BudgetTracker.ErrorReporting;

/// <summary>
/// Zgłasza nieobsłużony wyjątek (HTTP 500) do kolejki i ZAWSZE przepuszcza go dalej (<c>false</c>),
/// żeby odpowiedź zbudował standardowy mechanizm.
/// </summary>
/// <remarks>
/// ⚠️ Rejestruj PO <c>DomainExceptionHandler</c>: handlery idą w kolejności rejestracji, a wyjątki domenowe
/// (4xx) są obsłużone wcześniej i tu nie docierają — inaczej każde „złe wejście" zakładałoby issue.
/// Przerwane żądanie (<see cref="OperationCanceledException"/> przy <c>RequestAborted</c>) to nie awaria.
/// </remarks>
public sealed class ErrorReportingExceptionHandler(
    ErrorReportQueue queue,
    RecentLogBuffer logs,
    IOptions<ErrorReportingOptions> options) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var opt = options.Value;
        if (!opt.Enabled) return ValueTask.FromResult(false);
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return ValueTask.FromResult(false);

        var traceId = Activity.Current?.TraceId.ToString();

        queue.TryEnqueue(new ErrorReport(
            DateTimeOffset.UtcNow,
            opt.Service,
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            traceId,
            exception,
            logs.For(traceId, opt.MaxLogLines)));

        return ValueTask.FromResult(false);
    }
}
