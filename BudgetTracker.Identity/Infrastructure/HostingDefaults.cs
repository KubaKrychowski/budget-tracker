namespace BudgetTracker.Identity.Infrastructure;

/// <summary>Ustalenia hostingowe, które muszą być takie same na każdej instancji serwera tożsamości.</summary>
public static class HostingDefaults
{
    /// <summary>
    /// Nazwa aplikacji dla Data Protection: instancje o tej samej nazwie i tych samych kluczach odczytają
    /// nawzajem swoje ciasteczka, zamiast wylogowywać użytkownika przy każdym przełączeniu na inną.
    /// </summary>
    public const string ApplicationName = "BudgetTracker.Identity";

    /// <summary>Katalog trwałych kluczy Data Protection — wymagany poza Development, bez wartości domyślnej.</summary>
    public const string DataProtectionKeysPathKey = "DataProtection:KeysPath";
}
