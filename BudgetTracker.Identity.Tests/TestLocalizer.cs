using System.Globalization;
using BudgetTracker.Identity.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace BudgetTracker.Identity.Tests;

/// <summary>Prawdziwy localizer na prawdziwych plikach <c>SharedResource*.resx</c> — testy sprawdzają realne teksty.</summary>
internal static class TestLocalizer
{
    public static IStringLocalizer<SharedResource> Create() =>
        new StringLocalizer<SharedResource>(
            new ResourceManagerStringLocalizerFactory(MsOptions.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    /// <summary>Wykonuje <paramref name="action"/> w danym języku i przywraca poprzedni — kultura jest globalna dla wątku.</summary>
    public static T InCulture<T>(string culture, Func<T> action)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
