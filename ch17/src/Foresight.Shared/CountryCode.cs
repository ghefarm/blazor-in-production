namespace Foresight.Shared;

/// <summary>The visitor's country, as two letters, or null.</summary>
public static class CountryCode
{
    /// <summary>
    /// Two ASCII letters in upper case, or null. Cloudflare sends XX when it
    /// does not know the country and T1 for Tor; neither is a country, and
    /// anything else a client could send in the header is not trusted.
    /// </summary>
    public static string? Normalize(string? code)
    {
        if (code is not { Length: 2 }
            || !char.IsAsciiLetter(code[0]) || !char.IsAsciiLetter(code[1]))
            return null;

        var upper = code.ToUpperInvariant();
        return upper == "XX" ? null : upper;
    }
}
