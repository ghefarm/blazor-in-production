using Foresight.Shared;

namespace Foresight.Web.Services;

/// <summary>
/// The visitor's country, and whether premium is offered there. Scoped:
/// one per circuit. The country is read once, from the header Cloudflare
/// adds to the first request, and is not stored with any account.
/// </summary>
/// <remarks>
/// The excluded countries are the setting Subscription:ExcludedCountries.
/// A visitor whose country is not known is not excluded. It is a soft gate:
/// a VPN gets round it, which is fine for "not offered here for now".
/// Someone who already subscribed can always manage and cancel.
/// </remarks>
public sealed class VisitorRegion
{
    public const string NotOfferedMessage =
        "Premium is not offered in your region at the moment.";

    private readonly HashSet<string> _excluded;

    public VisitorRegion(IConfiguration config)
    {
        _excluded = (config.GetSection("Subscription:ExcludedCountries")
                .Get<string[]>() ?? [])
            .Select(CountryCode.Normalize)
            .OfType<string>()
            .ToHashSet();
    }

    public string? Country { get; private set; }

    /// <summary>Keeps a country; null never replaces a known one.</summary>
    public void Capture(string? country)
    {
        // The circuit has no request, so it can only ever hand over null,
        // and the layout is created again now and then within one circuit.
        if (CountryCode.Normalize(country) is { } code)
            Country = code;
    }

    public bool OffersPremium =>
        Country is null || !_excluded.Contains(Country);
}
