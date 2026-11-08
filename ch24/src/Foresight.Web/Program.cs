using Foresight.Shared;
using Foresight.Web.Components;
using Foresight.Web.Services;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

var blazor = builder.Services.AddRazorComponents();
blazor.AddInteractiveServerComponents(options =>
    {
        // A circuit whose connection dropped waits this long for the
        // browser to come back. The token waits with it; after that the
        // visitor signs in again.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
        // At most this many dropped circuits wait in memory at once.
        options.DisconnectedCircuitMaxRetained = 100;
    })
    .AddHubOptions(options =>
    {
        // The server pings a quiet connection every 15 seconds, and closes
        // one whose browser has sent nothing for 60.
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    });

// The visitor's address goes from the prerender into the circuit.
blazor.RegisterPersistentService<VisitorAddress>(RenderMode.InteractiveServer);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VisitorAddress>();

// The visitor's address as Cloudflare names it, believed only on a
// connection from Cloudflare's ranges: the API's rule (chapter 23).
builder.Services.AddEdge(config);

// Every page talks to the API through this one client. No message handler
// adds the token: the factory builds handlers in a DI scope of their own,
// never the circuit's (chapter 5). ForesightApi attaches it instead.
// The key marks this server as our own website, so the API believes the
// visitor it names (chapter 22).
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress =
        new Uri(config["ApiBaseUrl"] ?? "https://localhost:7030/");
    if (config["WebsiteKey"] is { Length: > 0 } key)
        client.DefaultRequestHeaders.Add(ApiHeaders.WebsiteKey, key);
});
// The visitor's login lives in their circuit. Both services are scoped:
// one of each per circuit, so no visitor ever sees another one's token.
builder.Services.AddScoped<CircuitTokenStore>();
builder.Services.AddScoped<ForesightApi>();
// The visitor's country, read from the first request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VisitorRegion>();

var app = builder.Build();

// First of all: whose request this is.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS for 30 days by default; see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute(
    "/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
