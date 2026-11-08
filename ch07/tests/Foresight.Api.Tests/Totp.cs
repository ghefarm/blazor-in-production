using System.Security.Cryptography;

namespace Foresight.Api.Tests;

/// <summary>
/// What an authenticator app does: a six-digit code from the shared key
/// and the current 30-second step (RFC 6238, with HMAC-SHA1).
/// </summary>
internal static class Totp
{
    public static string Code(string groupedKey, DateTimeOffset? at = null)
    {
        var key = Base32(groupedKey.Replace(" ", "").ToUpperInvariant());
        var step = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counter);

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var number = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (number % 1_000_000).ToString("D6");
    }

    private static byte[] Base32(string text)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in text.TrimEnd('='))
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }
        return bytes.ToArray();
    }
}
