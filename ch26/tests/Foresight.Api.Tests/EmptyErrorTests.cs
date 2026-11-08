using System.Net;
using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>What a page shows for an error answer with no sentence.</summary>
[Trait("Chapter", "11")]
public sealed class EmptyErrorTests
{
    [Fact]
    public async Task AnEmpty500_NamesTheFailure_NotThePassword()
    {
        // What the incident's server sent: a 500 with no body at all.
        using var response =
            new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var text = await ApiError.ReadAsync(response);

        Assert.Contains("error 500", text);
        Assert.DoesNotContain("password", text);
    }
}
