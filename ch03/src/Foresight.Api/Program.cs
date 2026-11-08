using Foresight.Api.Data;
using Foresight.Api.Feeds;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();
// Bring the database to the newest schema: create it on the first start,
// and apply every migration a new release brings. EnsureCreated could not
// do the second; it does nothing once a database exists.
using (var db = app.Services
    .GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
    db.Database.Migrate();

app.MapControllers();
app.Run();

/// <summary>The entry point, visible to the tests.</summary>
public partial class Program;
