using Foresight.Shared;

namespace Foresight.Api.Defenses;

public sealed partial class TrustedCallers
{
    // The website may name the visitor a call is for, and nothing more:
    // it is limited like everyone else and never spends the reserve.
    public bool IsWebsite(HttpRequest request) =>
        Carries(request, ApiHeaders.WebsiteKey, _options.WebsiteKey);
}
