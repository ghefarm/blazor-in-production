using Microsoft.AspNetCore.Mvc.Testing;

namespace Foresight.Api.Tests;

/// <summary>
/// The real API with its own data folder. Settings come from environment
/// variables, because Program.cs reads some of them before the test host
/// could change them.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataFolder = Path.Combine(
        Path.GetTempPath(), "foresight-tests", Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        Set("DataFolder", _dataFolder);
        Set("Feed__MonthlyLimit", "100000");
    }

    private static void Set(string name, string value) =>
        Environment.SetEnvironmentVariable(name, value);
}

/// <summary>One API for every test class, started once.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
