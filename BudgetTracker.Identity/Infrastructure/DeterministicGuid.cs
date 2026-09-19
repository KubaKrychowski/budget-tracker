using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Identyfikator wyprowadzony z nazwy — ta sama nazwa zawsze daje ten sam Guid.
///
/// Kopia <c>BudgetTracker.Api.Infrastructure.DeterministicGuid</c> (ta sama przestrzeń nazw UUID,
/// ten sam algorytm) — potrzebna, żeby konta seedowane tutaj (<c>dev:user:owner</c>,
/// <c>demo:user:owner</c>) miały DOKŁADNIE ten sam identyfikator, co właściciel budżetów zaseedowanych
/// w API (<c>DevSeed</c>/<c>DemoSeed</c>). Bez tego zalogowany dev/demo user nie widziałby własnych,
/// zaseedowanych danych — filtr właściciela w <c>AppDbContext</c> API porównuje <c>sub</c> z tokenu
/// z <c>Budget.UserId</c>.
/// </summary>
public static class DeterministicGuid
{
    /// <summary>Ta sama przestrzeń nazw co w API — NIE zmieniaj, inaczej identyfikatory się rozjadą.</summary>
    private static readonly Guid Namespace = new("8f6d1e2c-3a4b-4c5d-9e7f-0a1b2c3d4e5f");

    public static Guid For(string name)
    {
        var namespaceBytes = ToBigEndian(Namespace);
        var nameBytes = Encoding.UTF8.GetBytes(name);

        var hash = SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(ToLittleEndian(guidBytes));
    }

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
