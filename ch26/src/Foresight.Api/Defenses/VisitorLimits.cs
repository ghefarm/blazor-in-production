using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Foresight.Shared;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Defenses;

/// <summary>
/// The "RateLimits" section: how many requests one visitor may make in one
/// minute.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    // All calls together. A page view makes a handful.
    public int PerMinute { get; set; } = 300;

    // Every forecast list costs one request of the feed's monthly limit.
    public int Forecasts { get; set; } = 30;

    // Nobody types new numbers into the what-if ten times a minute by
    // hand. A script does.
    public int WhatIf { get; set; } = 10;

    // The last requests of the feed's month, which only the scheduler may
    // spend (FeedReserve).
    public int FeedReserve { get; set; } = 50;
}

/// <summary>Rate limits per visitor, also behind our own website.</summary>
public static class VisitorLimits
{
    public const string Forecasts = "forecasts";
    public const string WhatIf = "what-if";

    public static IServiceCollection AddVisitorLimits(
        this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RateLimitOptions>(
            config.GetSection(RateLimitOptions.SectionName));
        services.AddSingleton<FeedReserve>();

        // A new random salt at every start. The limiter needs a key that
        // tells visitors apart, not their addresses: it keeps only a keyed
        // hash, which means nothing outside this process and nothing after
        // the next restart.
        var salt = RandomNumberGenerator.GetBytes(16);
        string KeyOf(string visitor) => Convert.ToHexString(
            HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(visitor)), 0, 8);

        // Who a request counts against. Called for every request, once for
        // the global limit and once for the endpoint's own policy.
        RateLimitPartition<string> Partition(
            HttpContext ctx, Func<RateLimitOptions, int> limitOf)
        {
            var callers = ctx.RequestServices
                .GetRequiredService<TrustedCallers>();
            if (callers.IsScheduler(ctx.Request))
                return RateLimitPartition.GetNoLimiter("scheduler");

            // Everyone else is their address. Behind Cloudflare, chapter 23
            // makes sure this is the visitor's address, not Cloudflare's.
            var visitor = ctx.Connection.RemoteIpAddress?.ToString() ?? "";

            // The website calls from one server for all its visitors, so
            // it names the visitor, and each one counts alone. The header
            // is believed only next to the website's key.
            if (callers.IsWebsite(ctx.Request))
            {
                visitor = ctx.Request.Headers[ApiHeaders.VisitorIp].ToString();

                // A website call made for no visitor: one bucket for all of
                // them would let one busy visitor lock out the rest.
                if (visitor.Length == 0)
                    return RateLimitPartition.GetNoLimiter("website");
            }

            var limit = limitOf(ctx.RequestServices
                .GetRequiredService<IOptions<RateLimitOptions>>().Value);
            return RateLimitPartition.GetFixedWindowLimiter(KeyOf(visitor),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit,
                    Window = TimeSpan.FromMinutes(1),
                });
        }

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    c => Partition(c, o => o.PerMinute));
            options.AddPolicy(Forecasts, c => Partition(c, o => o.Forecasts));
            options.AddPolicy(WhatIf, c => Partition(c, o => o.WhatIf));

            // A sentence, like every other error, so the page can show the
            // visitor why. An empty 429 would show as "error 429".
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) => new ValueTask(
                context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Too many requests. Please wait a "
                        + "minute and try again."), ct));
        });

        return services;
    }
}
