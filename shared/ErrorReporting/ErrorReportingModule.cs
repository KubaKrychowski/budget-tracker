using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BudgetTracker.ErrorReporting;

public static class ErrorReportingModule
{
    /// <summary>
    /// Rejestruje zakładanie issues przy 500. Wołaj PO <c>AddExceptionHandler&lt;DomainExceptionHandler&gt;()</c>
    /// (API) — kolejność rejestracji to kolejność wykonania handlerów.
    /// </summary>
    public static IServiceCollection AddErrorReporting(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.Configure<ErrorReportingOptions>(configuration.GetSection(ErrorReportingOptions.SectionName));
        services.PostConfigure<ErrorReportingOptions>(o =>
        {
            if (o.Service == "api") o.Service = serviceName;
        });

        services.AddSingleton<RecentLogBuffer>();
        services.AddSingleton<ErrorReportQueue>();
        services.AddSingleton<ILoggerProvider, RecentLogProvider>();

        services.AddHttpClient(GitHubIssueReporter.HttpClientName, (sp, client) =>
        {
            var token = sp.GetRequiredService<IOptions<ErrorReportingOptions>>().Value.Token;
            client.BaseAddress = new Uri("https://api.github.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("budget-tracker-error-reporting");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        });

        services.AddHostedService<GitHubIssueReporter>();
        services.AddExceptionHandler<ErrorReportingExceptionHandler>();
        return services;
    }
}
