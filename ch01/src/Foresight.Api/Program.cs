using Foresight.Api.Feeds;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddControllers();
services.AddSingleton(TimeProvider.System);

// The sample feed. Chapter 3 gives it its monthly limit.
services.AddSingleton<IEventSource, SampleEventSource>();

var app = builder.Build();

app.MapControllers();
app.Run();

/// <summary>The entry point, visible to the tests.</summary>
public partial class Program;
