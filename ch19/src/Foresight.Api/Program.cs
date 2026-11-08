using System.Text;
using Foresight.Api;
using Foresight.Api.Accounts;
using Foresight.Api.Channels;
using Foresight.Api.Data;
using Foresight.Api.Defenses;
using Foresight.Api.Feeds;
using Foresight.Api.Mail;
using Foresight.Api.Poll;
using Foresight.Api.Scheduling;
using Foresight.Api.TrackRecord;
using Foresight.Billing;
using Foresight.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

services.AddControllers();
services.AddSingleton(TimeProvider.System);
// One SQLite database in the data folder. The factory lets singleton
// stores open a short-lived context per call.
var dataFolder = config["DataFolder"] is { Length: > 0 } folder
    ? folder
    : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataFolder);
services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(
    $"Data Source={Path.Combine(dataFolder, "app.db")}"));

// The sample feed and its monthly limit.
services.Configure<FeedOptions>(config.GetSection(FeedOptions.SectionName));
services.AddSingleton<FeedMeter>();
services.AddSingleton<IEventSource, SampleEventSource>();

// Real mail over SMTP when the host names a server. Without one, each mail
// is a file in the data folder: on your machine and in every test. The
// code that sends mail only knows IMailSender.
services.Configure<MailOptions>(config.GetSection(MailOptions.SectionName));
services.PostConfigure<MailOptions>(o =>
{
    if (o.Folder.Length == 0) o.Folder = Path.Combine(dataFolder, "mail");
});
services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
if (config[$"{SmtpOptions.SectionName}:Host"] is { Length: > 0 })
    services.AddSingleton<IMailSender, SmtpMailSender>();
else
    services.AddSingleton<IMailSender, FolderMailSender>();

// One throttle for every confirmation mail, kept in the database.
services.Configure<ConfirmationMailOptions>(
    config.GetSection(ConfirmationMailOptions.SectionName));
services.AddSingleton<ConfirmationMailThrottle>();

// Accounts. Identity keeps them in the database and does the hashing, the
// lockout and the confirmation tokens; the API issues its own login token.
// One lookup form of an email for Identity and our stores alike.
services.AddSingleton<ILookupNormalizer, EmailLookupNormalizer>();
services.AddSingleton<UserStore>();
// Before Identity, so Identity keeps this hasher instead of its own: it
// also accepts the hashes of the first, hand-built login.
services.AddScoped<IPasswordHasher<AppUser>, LegacyPasswordHasher>();
var lockout = config.GetSection(LoginLockoutOptions.SectionName)
    .Get<LoginLockoutOptions>() ?? new LoginLockoutOptions();
services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = ""; // the email, checked
        options.Password.RequiredLength = PasswordRules.MinLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = lockout.MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromSeconds(lockout.LockSeconds);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
// OWASP's floor for PBKDF2 with SHA-512; Identity's default is lower.
services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
// The keys that sign the confirmation links, kept beside the database. In
// memory they would be lost at every restart, and with them every link
// still waiting in an inbox. On Windows they are encrypted for the machine.
var keys = services.AddDataProtection()
    .SetApplicationName("Foresight.Api")
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(dataFolder, "keys")));
if (OperatingSystem.IsWindows())
    keys.ProtectKeysWithDpapi(protectToLocalMachine: true);

// Login tokens. The key signs every login, so whoever holds it can sign one
// for any account, the admin's included. The committed settings leave it
// empty; it comes from user secrets on a developer machine and from the
// host in production. A missing or short key stops the API here. HMAC with
// SHA-256 needs at least 256 bits, and .NET would only refuse a shorter key
// at the first login, long after the deploy looked fine.
var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be a random value of at least 32 characters, "
        + "set in user secrets or on the host.");
services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
services.AddSingleton<TokenIssuer>();
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new()
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt.Key)),
    });

services.AddAuthorization(options =>
    options.AddPolicy(AdminEmail.PolicyName, policy =>
        policy.RequireAssertion(c => config.IsAdmin(c.User.GetEmail()))));

// A valid signature is not enough: the account must still exist. Asked
// for every request that carries a token (chapter 7).
services.AddScoped<AccountCheckEvents>();
services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
    options => options.EventsType = typeof(AccountCheckEvents));

// PayPal. The client secret and the webhook id come only from the host.
services.Configure<PayPalOptions>(
    config.GetSection(PayPalOptions.SectionName));
services.AddHttpClient(PayPalClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
services.AddSingleton<IPayPalClient, PayPalClient>();
services.AddSingleton<ISubscriptionStore, SubscriptionStore>();
services.AddSingleton<PremiumAccess>();
services.AddSingleton<ContractStore>();
services.AddSingleton<ISubscriptionListener, ContractConfirmation>();
services.AddSingleton(sp => new SubscriptionManager(
    sp.GetRequiredService<IPayPalClient>(),
    sp.GetRequiredService<ISubscriptionStore>(),
    sp.GetRequiredService<IOptions<PayPalOptions>>().Value.PremiumPlanIds,
    sp.GetRequiredService<ISubscriptionListener>(),
    sp.GetRequiredService<UserStore>()));

// The poll (chapter 18). Its candidates and rounds come from the settings.
services.Configure<PollOptions>(config.GetSection(PollOptions.SectionName));
services.AddSingleton<PollStore>();

// The track record (chapter 19): what was posted, and what happened.
services.Configure<TrackRecordOptions>(
    config.GetSection(TrackRecordOptions.SectionName));
services.PostConfigure<TrackRecordOptions>(o =>
{
    if (o.ScheduleFolder.Length == 0)
        o.ScheduleFolder = Path.Combine(dataFolder, "schedule");
});
services.AddSingleton<IPostedForecasts, ScheduleFiles>();
services.AddSingleton<IOutcomeSource, SampleOutcomeSource>();
services.AddSingleton<TrackRecordService>();

// One deletion order for the owner and the admin. Scoped, because
// Identity's UserManager is.
services.AddScoped<AccountDeletion>();

// The keys of our own callers: the scheduler's (chapter 14) and the
// website's (chapter 22).
services.AddTrustedCallers(config);

// Telegram and the two channels, and the kill switch (Part IV).
services.AddChannels(config);
services.AddSingleton<SwitchStore>();

var app = builder.Build();
// Bring the database to the newest schema: create it on the first start,
// and apply every migration a new release brings. EnsureCreated could not
// do the second; it does nothing once a database exists.
using (var db = app.Services
    .GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
    db.Database.Migrate();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

/// <summary>The entry point, visible to the tests.</summary>
public partial class Program;
