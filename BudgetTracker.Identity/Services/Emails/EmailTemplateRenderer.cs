using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace BudgetTracker.Identity.Services.Emails;

/// <summary>
/// Składa mail z szablonów HTML (<c>Emails/*.html</c>) i arkusza <c>Emails/email.css</c>, wbudowanych w assembly.
/// Szablon fragmentu (treść) trafia do <c>Layout.html</c> w miejsce <c>{{Content}}</c>, a CSS do <c>{{Css}}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Każda wartość podstawiana za <c>{{Nazwa}}</c> jest HTML-enkodowana — adres z tokenem i tekst z zasobów
/// nie mogą wstrzyknąć znaczników. Bez enkodowania są TYLKO fragment i CSS, które pochodzą z naszych
/// plików, nie z danych użytkownika.
/// </para>
/// <para>
/// ⚠️ Nierozwiązany znacznik rzuca wyjątek zamiast zostać w mailu jako <c>{{Cos}}</c> — literówka w
/// szablonie wyszłaby inaczej do użytkownika, a w teście wychodzi od razu.
/// </para>
/// </remarks>
public sealed partial class EmailTemplateRenderer
{
    public const string LayoutTemplate = "Layout";
    public const string LinkMessageTemplate = "LinkMessage";
    public const string TwoFactorCodeTemplate = "TwoFactorCode";

    private const string CssResource = "email.css";
    private const string ContentSlot = "Content";
    private const string CssSlot = "Css";

    /// <summary>
    /// Enkoduje znaczniki HTML (<c>&lt; &gt; &amp; "</c>), ale zostawia polskie litery w spokoju — domyślny enkoder
    /// zamieniłby je na encje numeryczne, poprawne, lecz nieczytelne w źródle maila.
    /// </summary>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly Dictionary<string, string> cache = [];
    private readonly Lock cacheLock = new();

    /// <summary>
    /// Renderuje <paramref name="template"/> w <c>Layout.html</c>. <paramref name="values"/> zasilają zarówno
    /// fragment, jak i układ (<c>Lang</c>, <c>Title</c>, <c>AppName</c>, <c>Footer</c>).
    /// </summary>
    public string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var content = Substitute(Load(template + ".html"), values, raw: null);

        return Substitute(
            Load(LayoutTemplate + ".html"),
            values,
            raw: new Dictionary<string, string>
            {
                [ContentSlot] = content,
                [CssSlot] = Load(CssResource),
            });
    }

    private static string Substitute(
        string template, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string>? raw) =>
        PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value;

            if (raw is not null && raw.TryGetValue(name, out var rawValue)) return rawValue;
            if (values.TryGetValue(name, out var value)) return Encoder.Encode(value);

            throw new InvalidOperationException($"Szablon maila nie ma wartości dla znacznika {{{{{name}}}}}.");
        });

    private string Load(string fileName)
    {
        lock (cacheLock)
        {
            if (cache.TryGetValue(fileName, out var cached)) return cached;

            var resourceName = $"Emails.{fileName}";
            using var stream = typeof(EmailTemplateRenderer).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Brak wbudowanego zasobu {resourceName}.");
            using var reader = new StreamReader(stream);

            return cache[fileName] = reader.ReadToEnd();
        }
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}
