using Foresight.Shared;

namespace Foresight.Api.Tests;

/// <summary>What counts as the visitor's country.</summary>
[Trait("Chapter", "2")]
public sealed class CountryCodeTests
{
    [Theory]
    [InlineData("DE", "DE")]
    [InlineData("de", "DE")]
    [InlineData("XX", null)]
    [InlineData("T1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("DEU", null)]
    [InlineData("D1", null)]
    [InlineData("ÄÖ", null)]
    public void OnlyTwoLetters_AreACountry(string? header, string? country) =>
        Assert.Equal(country, CountryCode.Normalize(header));
}
