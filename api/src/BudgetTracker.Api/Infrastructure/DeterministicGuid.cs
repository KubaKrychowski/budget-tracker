using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Identyfikator wyprowadzony z nazwy — ta sama nazwa zawsze daje ten sam Guid.
///
/// Służy WYŁĄCZNIE danym seedowanym. Kryterium: ponowne uruchomienie seeda musi dać te same
/// `BusinessId`, inaczej wszystko, co odwołuje się do zaseedowanej kategorii, wskazuje po
/// re-seedzie na nic.
///
/// Dlaczego wyliczane, a nie 28 literałów wpisanych w kod: literały trzeba pilnować ręcznie
/// przy każdej nowej kategorii, a pomyłka (skopiowany duplikat) wychodzi dopiero jako błąd
/// unikalnego indeksu w runtime. Tu nazwa JEST identyfikatorem i nie da się ich rozjechać.
///
/// To UUID v5 z RFC 4122: SHA-1 z przestrzeni nazw i nazwy. SHA-1 nie pełni tu funkcji
/// kryptograficznej — jest algorytmem wskazanym przez specyfikację dla tej wersji UUID-a.
/// </summary>
public static class DeterministicGuid
{
    /// <summary>
    /// Przestrzeń nazw tego projektu. Zmiana tej wartości przestawia WSZYSTKIE identyfikatory
    /// danych seedowanych — nie ruszaj jej.
    /// </summary>
    private static readonly Guid Namespace = new("8f6d1e2c-3a4b-4c5d-9e7f-0a1b2c3d4e5f");

    public static Guid For(string name)
    {
        var namespaceBytes = ToBigEndian(Namespace);
        var nameBytes = Encoding.UTF8.GetBytes(name);

        var hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);   // wersja 5
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);   // wariant RFC 4122

        return new Guid(ToLittleEndian(guidBytes));
    }

    /// <summary>
    /// .NET układa pierwsze trzy pola Guida po little-endian, a RFC wymaga big-endian.
    /// Bez tej zamiany ten sam „identyfikator" liczyłby się inaczej niż w innych systemach.
    /// </summary>
    private static byte[] ToBigEndian(Guid value)
    {
        var bytes = value.ToByteArray();
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return bytes;
    }

    private static byte[] ToLittleEndian(byte[] bytes)
    {
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
        return bytes;
    }
}
