using Foresight.Telegram;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Channels;

/// <summary>The services of Part IV: Telegram and the two channels.</summary>
public static class ChannelSetup
{
    public static IServiceCollection AddChannels(
        this IServiceCollection services, IConfiguration config)
    {
        // The token comes only from the host. A wrong zone name stops the
        // API at start, not at the first post.
        services.AddOptions<TelegramOptions>()
            .Bind(config.GetSection(TelegramOptions.SectionName))
            .Validate(o => o.HasKnownZone,
                "Telegram:TimeZone is not a zone this machine knows.")
            .ValidateOnStart();

        // No request log for this client. The token is part of every
        // address it calls, and the log would keep every one of them.
        services.AddHttpClient(TelegramGateway.HttpClientName)
            .RemoveAllLoggers();
        services.AddSingleton(sp => new TelegramGateway(
            () => sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(TelegramGateway.HttpClientName),
            sp.GetRequiredService<IOptions<TelegramOptions>>().Value));
        services.AddSingleton<ITelegramSender>(
            sp => sp.GetRequiredService<TelegramGateway>());

        // The bot also keeps the premium channel's door (chapter 17).
        services.AddSingleton<IChannelMembers>(
            sp => sp.GetRequiredService<TelegramGateway>());
        services.AddSingleton<ChannelMembership>();
        return services;
    }
}
