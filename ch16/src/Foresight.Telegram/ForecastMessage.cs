using System.Globalization;
using System.Text;
using Foresight.Shared;

namespace Foresight.Telegram;

/// <summary>
/// The text of one Telegram post: one forecast, in Telegram's classic
/// Markdown. The admin page and the scheduler both use it, so a post looks
/// the same whoever sends it.
/// </summary>
public static class ForecastMessage
{
    public const string TestBanner = "TEST, please ignore";

    public static string For(
        ForecastDto f, TimeZoneInfo zone, string? banner = null)
    {
        var starts = TimeZoneInfo.ConvertTimeFromUtc(f.StartsUtc, zone);
        var offset = Label(zone.GetUtcOffset(f.StartsUtc));
        var text = new StringBuilder();
        if (banner is not null)
            text.Append(Escape(banner)).Append("\n\n");

        // Bold only around words we wrote. Telegram allows no escape
        // inside bold, so a title in bold could never be made safe.
        text.Append(f.IsPremium ? "*Premium forecast*" : "*Forecast*");
        text.Append('\n').Append(Escape(f.Title)).Append(", starts ")
            .Append(starts.ToString("HH:mm", CultureInfo.InvariantCulture))
            .Append(" (").Append(offset).Append(')');
        text.Append('\n').Append(Escape(f.Outcome)).Append(": ")
            .Append(Math.Round(f.Probability * 100)
                .ToString(CultureInfo.InvariantCulture))
            .Append('%');
        return text.ToString();
    }

    // Classic Markdown reads these four characters as markup, and a title
    // with one of them alone would be refused. A backslash before each one
    // makes it plain text again.
    public static string Escape(string text)
    {
        var safe = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '_' or '*' or '`' or '[')
                safe.Append('\\');
            safe.Append(c);
        }
        return safe.ToString();
    }

    // "UTC+1", "UTC-5" or "UTC+5:30": a reader abroad can work out their
    // own time from it, which a zone name such as "CET" does not promise.
    private static string Label(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var size = offset.Duration();
        var format = size.Minutes == 0 ? "%h" : @"h\:mm";
        return "UTC" + sign
            + size.ToString(format, CultureInfo.InvariantCulture);
    }
}
