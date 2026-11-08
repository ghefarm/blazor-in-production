using Microsoft.AspNetCore.Mvc.Testing;

namespace Foresight.Api.Tests;

/// <summary>The real API, started in memory for the tests.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
}

/// <summary>One API for every test class, started once.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
