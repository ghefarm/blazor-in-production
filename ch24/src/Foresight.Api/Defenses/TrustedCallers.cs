using System.Security.Cryptography;
using System.Text;
using Foresight.Shared;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Defenses;

/// <summary>
/// The "TrustedCallers" section: the keys of our own callers. Each comes
/// only from the host, like every secret. An empty key means that no
/// caller is that one.
/// </summary>
public sealed class TrustedCallerOptions
{
    public const string SectionName = "TrustedCallers";

    // Sent by the scheduler as X-Scheduler-Key (chapter 15).
    public string SchedulerKey { get; set; } = "";

    // Sent by the website as X-Website-Key (chapter 22).
    public string WebsiteKey { get; set; } = "";
}

/// <summary>
/// Knows our own callers by their keys, in one place and in one way. A
/// caller with a key has no account and no password: the key is all it
/// shows, and it opens only what that caller needs.
/// </summary>
public sealed partial class TrustedCallers
{
    // A short key could be guessed. The API refuses one when it starts.
    public const int MinKeyLength = 32;

    private readonly TrustedCallerOptions _options;

    public TrustedCallers(IOptions<TrustedCallerOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>Empty, which lets nobody in, or long enough.</summary>
    public static bool IsLongEnough(string key) =>
        key.Length is 0 or >= MinKeyLength;

    public bool IsScheduler(HttpRequest request) =>
        Carries(request, ApiHeaders.SchedulerKey, _options.SchedulerKey);

    private static bool Carries(
        HttpRequest request, string header, string expected)
    {
        // A key that is not set lets nobody in, not even a caller who
        // sends an empty header.
        if (expected.Length == 0
            || request.Headers[header].ToString() is not { Length: > 0 } sent)
            return false;

        // The same time for every wrong key, so timing tells a guesser
        // nothing about how much of the key was right.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(expected));
    }
}

/// <summary>Registers <see cref="TrustedCallers"/> and checks its keys.
/// </summary>
public static class TrustedCallerSetup
{
    public static IServiceCollection AddTrustedCallers(
        this IServiceCollection services, IConfiguration config)
    {
        // Checked when the API starts, so a short key stops the deploy and
        // not the scheduler's first request, hours later.
        services.AddOptions<TrustedCallerOptions>()
            .Bind(config.GetSection(TrustedCallerOptions.SectionName))
            .Validate(o => TrustedCallers.IsLongEnough(o.SchedulerKey)
                    && TrustedCallers.IsLongEnough(o.WebsiteKey),
                $"Each key in {TrustedCallerOptions.SectionName} must be "
                + $"empty or {TrustedCallers.MinKeyLength} characters long "
                + "at least.")
            .ValidateOnStart();
        return services.AddSingleton<TrustedCallers>();
    }
}
