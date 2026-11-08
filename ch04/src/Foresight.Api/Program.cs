using System.Text;
using Foresight.Api.Accounts;
using Foresight.Api.Data;
using Foresight.Api.Feeds;
using Foresight.Api.Mail;
using Foresight.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

// Each mail is a file in the data folder, on your machine and in every
// test. Chapter 6 adds real mail over SMTP; the code that sends mail only
// knows IMailSender.
services.Configure<MailOptions>(config.GetSection(MailOptions.SectionName));
services.PostConfigure<MailOptions>(o =>
{
    if (o.Folder.Length == 0) o.Folder = Path.Combine(dataFolder, "mail");
});
services.AddSingleton<IMailSender, FolderMailSender>();

// Accounts. Identity keeps them in the database and does the hashing, the
// lockout and the confirmation tokens; the API issues its own login token.
// One lookup form of an email for Identity and our stores alike.
services.AddSingleton<ILookupNormalizer, EmailLookupNormalizer>();
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
