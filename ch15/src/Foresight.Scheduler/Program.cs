using Foresight.Scheduler;
using Foresight.Telegram;
using Microsoft.Extensions.Configuration;

// Foresight's scheduler. The Task Scheduler starts it, it does one job,
// and it ends.
//   --plan   in the morning: write today's schedule file
//   --due    every five minutes: post the events that have come due
//   --send   really post; without it, --due is a dry run that only prints
//   --test   put a TEST banner on every post (use test channels)
var plan = args.Contains("--plan");
var due = args.Contains("--due");
var send = args.Contains("--send");
var test = args.Contains("--test");

if (plan == due)
{
    Console.Error.WriteLine("Give one of --plan or --due.");
    return 2;
}

var settings = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Production.json", optional: true)
    .AddEnvironmentVariables("FORESIGHT_")
    .Build()
    .Get<SchedulerSettings>() ?? new SchedulerSettings();

if (settings.ApiBaseUrl.Length == 0 || settings.ApiKey.Length == 0)
{
    Console.Error.WriteLine("ApiBaseUrl and ApiKey must be set.");
    return 1;
}
if (!settings.Telegram.HasKnownZone)
{
    Console.Error.WriteLine("Telegram:TimeZone is not a zone this machine "
        + "knows.");
    return 1;
}
if (send && !settings.Telegram.CanPost)
{
    Console.Error.WriteLine("--send needs Telegram:BotToken, FreeChannel "
        + "and PremiumChannel.");
    return 1;
}

using var runLock = RunLock.TryTake(settings.Folder);
if (runLock is null)
{
    Console.WriteLine("Another run is still busy. This one stops.");
    return 0;
}

using var telegramHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var api = new SchedulerApi(settings.ApiBaseUrl, settings.ApiKey);
var runner = new ScheduleRunner(
    settings,
    api,
    new TelegramGateway(() => telegramHttp, settings.Telegram),
    new ScheduleFile(settings.Folder),
    TimeProvider.System,
    Console.Out,
    pause: TimeSpan.FromMilliseconds(1100));

try
{
    if (plan)
    {
        // The plan posts nothing, so the kill switch does not stop it:
        // posting that is switched on again at noon finds today's plan.
        await runner.PlanAsync();
    }
    else if (await runner.PostingIsOnAsync())
    {
        await runner.TickAsync(send, test);
    }
    else
    {
        Console.WriteLine("Posting is switched off on the admin page.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"The run stopped: {ex.Message}");
    return 1;
}
