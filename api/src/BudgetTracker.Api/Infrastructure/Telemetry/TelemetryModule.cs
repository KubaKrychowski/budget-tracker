using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BudgetTracker.Api.Infrastructure.Telemetry;

/// <summary>Rejestracja OpenTelemetry z eksportem do Azure Application Insights.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Eksport włącza się wyłącznie, gdy jest <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> albo
/// <c>ApplicationInsights:ConnectionString</c>. Bez niego (lokalny Rider, testy) nie rejestrujemy niczego — dystrybucja
/// Azure Monitor rzuca wyjątek przy starcie, gdy nie ma connection stringa.</item>
/// <item>Poza żądaniami HTTP, HttpClient i runtime .NET zbieramy zapytania do Postgresa (źródło i metryki
/// <c>Npgsql</c>, wbudowane w sterownik) oraz własne źródło <see cref="AppTelemetry"/>.</item>
/// <item>⚠️ Zapytania SQL trafiają do spanów jako tekst z parametrami zastąpionymi znakami zapytania — Npgsql nie
/// wysyła wartości parametrów. Nie włączaj ich ręcznie: w parametrach są kwoty i opisy z wyciągów.</item>
/// <item>Domyślne próbkowanie dystrybucji Azure Monitor zostaje bez zmian. Rachunek rośnie od ilości danych
/// (GB w Log Analytics) — limit dzienny ustawia Terraform (<c>daily_quota_gb</c>).</item>
/// </list>
/// </remarks>
public static class TelemetryModule
{
    private const string ConnectionStringKey = "ApplicationInsights:ConnectionString";

    public static IServiceCollection AddTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey]
            ?? configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString)) return services;

        services.AddOpenTelemetry()
            .UseAzureMonitor(options => options.ConnectionString = connectionString)
            .WithTracing(tracing => tracing
                .AddSource("Npgsql")
                .AddSource(AppTelemetry.Name))
            .WithMetrics(metrics => metrics
                .AddMeter("Npgsql")
                .AddMeter(AppTelemetry.Name)
                .AddRuntimeInstrumentation());

        return services;
    }
}
