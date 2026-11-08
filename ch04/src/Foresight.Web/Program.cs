using Foresight.Web.Components;
using Foresight.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Every page talks to the API through this one client.
builder.Services.AddHttpClient("api", client =>
    client.BaseAddress =
        new Uri(config["ApiBaseUrl"] ?? "https://localhost:7030/"));
// The visitor's login lives in their circuit. Both services are scoped:
// one of each per circuit, so no visitor ever sees another one's token.
builder.Services.AddScoped<CircuitTokenStore>();
builder.Services.AddScoped<ForesightApi>();
// The visitor's country, read from the first request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VisitorRegion>();

var app = builder.Build();

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
