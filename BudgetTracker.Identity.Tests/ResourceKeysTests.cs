using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BudgetTracker.Identity.Tests;

/// <summary>
/// Strażnik tłumaczeń: brakujący klucz w <c>IStringLocalizer</c> nie rzuca wyjątku, tylko po cichu pokazuje
/// użytkownikowi nazwę klucza (<c>Login_Submit</c>) — tego nie wyłapie ani kompilator, ani przeglądanie ekranów
/// w jednym języku.
/// </summary>
public sealed partial class ResourceKeysTests
{
    private static readonly string IdentityDir = FindIdentityDir();

    /// <summary>
    /// Ścieżka z czasu kompilacji ([CallerFilePath]), a nie katalog wyjściowy: buduje się do katalogów tymczasowych
    /// (Rider blokuje bin/), więc od pliku wykonywalnego nie da się dojść do źródeł.
    /// </summary>
    private static string FindIdentityDir([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "BudgetTracker.Identity"));
        if (!File.Exists(Path.Combine(dir, "Resources", "SharedResource.resx")))
            throw new DirectoryNotFoundException($"Nie znaleziono zasobów Identity w {dir}.");

        return dir;
    }

    private static Dictionary<string, string> ReadResx(string fileName) =>
        XDocument.Load(Path.Combine(IdentityDir, "Resources", fileName))
            .Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Element("value")!);

    [Fact]
    public void Polish_and_english_resources_have_exactly_the_same_keys()
    {
        var en = ReadResx("SharedResource.resx").Keys.ToHashSet();
        var pl = ReadResx("SharedResource.pl.resx").Keys.ToHashSet();

        Assert.Empty(en.Except(pl));
        Assert.Empty(pl.Except(en));
    }

    [Fact]
    public void No_translation_is_empty()
    {
        foreach (var (file, texts) in new[] { ("en", ReadResx("SharedResource.resx")), ("pl", ReadResx("SharedResource.pl.resx")) })
        {
            var empty = texts.Where(t => string.IsNullOrWhiteSpace(t.Value)).Select(t => t.Key).ToList();
            Assert.True(empty.Count == 0, $"Puste tłumaczenia ({file}): {string.Join(", ", empty)}");
        }
    }

    [Fact]
    public void Every_key_used_in_code_and_views_exists_in_the_resources()
    {
        var known = ReadResx("SharedResource.resx").Keys.ToHashSet();
        var used = new HashSet<string>();

        foreach (var file in Directory.EnumerateFiles(IdentityDir, "*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml"))
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in LocalizerCallPattern().Matches(text)) used.Add(m.Groups[1].Value);
            foreach (Match m in ErrorMessagePattern().Matches(text)) used.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(used);
        var missing = used.Where(k => !known.Contains(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"Klucze użyte w kodzie, których nie ma w SharedResource.resx: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Dynamic_key_families_are_complete()
    {
        var known = ReadResx("SharedResource.resx").Keys.ToHashSet();

        var required = new[]
        {
            // AccountEmailService buduje klucz z prefiksu: Mail_<Confirm|Reset|Code>_<część>.
            "Mail_Confirm_Subject", "Mail_Confirm_Heading", "Mail_Confirm_Body", "Mail_Confirm_Button", "Mail_Confirm_Footer",
            "Mail_Reset_Subject", "Mail_Reset_Heading", "Mail_Reset_Body", "Mail_Reset_Button", "Mail_Reset_Footer",
            "Mail_Code_Subject", "Mail_Code_Heading", "Mail_Code_Body", "Mail_Code_Hint", "Mail_Code_Footer",
            // Authorize.cshtml: Scope_<zakres> dla zakresów, o które prosi SPA.
            "Scope_email", "Scope_profile", "Scope_budgettracker_api",
            // AdminController przekazuje klucze komunikatów jako argument (FlashAndRedirect), a widoki wybierają
            // klucz warunkiem (L[cond ? "A" : "B"]) — regex powyżej ich nie widzi, więc są tu wprost.
            "Admin_Flash_Deleted", "Admin_Flash_NotFound", "Admin_Flash_CannotDeleteSelf", "Admin_Flash_LastAdmin",
            "Admin_Flash_DataUnavailable", "Admin_Flash_OrphanGone", "Admin_Flash_Assigned", "Admin_Flash_OrphansDeleted",
            "Admin_Status_Confirmed", "Admin_Status_Unconfirmed", "Admin_Orphans_EmptyOwnerNote", "Admin_Orphans_UnknownOwnerNote",
            "Admin_ConfirmMismatch", "Admin_TargetRequired", "DeleteAccount_WrongPassword",
            // LocalizedIdentityErrorDescriber: Identity_<kod błędu>.
            "Identity_DefaultError", "Identity_PasswordTooShort", "Identity_PasswordRequiresNonAlphanumeric",
            "Identity_PasswordRequiresDigit", "Identity_PasswordRequiresUpper", "Identity_DuplicateEmail",
        };

        var missing = required.Where(k => !known.Contains(k)).ToList();
        Assert.True(missing.Count == 0, $"Brakuje kluczy: {string.Join(", ", missing)}");
    }

    [GeneratedRegex(@"(?:\bL|localizer)\[\s*""([A-Za-z0-9_]+)""")]
    private static partial Regex LocalizerCallPattern();

    [GeneratedRegex(@"ErrorMessage\s*=\s*""([A-Za-z0-9_]+)""")]
    private static partial Regex ErrorMessagePattern();
}
