using EV_Charging.Models;
using EV_Charging.Services;
using HADotNet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Read Home Assistant environment variables
var haUrl = Environment.GetEnvironmentVariable("SUPERVISOR_URL") ?? "http://supervisor/";
var haToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") ?? "";

// Log environment configuration on startup
var logger = LoggerFactory.Create(config => config.AddConsole())
    .CreateLogger("Startup");

logger.LogInformation("=== EV Charging Service Starting ===");
logger.LogInformation("SUPERVISOR_URL: {Url}", string.IsNullOrEmpty(haUrl) ? "NOT SET" : haUrl);
logger.LogInformation("SUPERVISOR_TOKEN: {TokenInfo}", 
    string.IsNullOrEmpty(haToken) ? "NOT SET" : $"SET ({haToken.Length} chars)");

// Configure Kestrel to listen on all interfaces
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(9801);
});

// Register configuration
builder.Services.Configure<EvChargingConfig>(
    builder.Configuration.GetSection("EvConfiguration"));

// Register EvChargingConfig as a scoped service for direct injection
builder.Services.AddScoped(sp => sp.GetRequiredService<IOptions<EvChargingConfig>>().Value);

// Register Home Assistant HTTP client
if (!string.IsNullOrEmpty(haToken))
{
    var uri = new Uri(haUrl.EndsWith("/") ? haUrl : haUrl + "/");
    ClientFactory.Initialize(uri, haToken);
    
    logger.LogInformation("Initializing Home Assistant client with URL: {Url}", uri);
    
    builder.Services.AddHttpClient<HomeAssistantService>()
        .ConfigureHttpClient(client =>
        {
            client.BaseAddress = uri + "core/";
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {haToken}");
        });
}
else
{
    logger.LogError("SUPERVISOR_TOKEN is not set. Home Assistant integration will not work.");
}

// Register services
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<EvChargingConfig>();
    return new ChargingAlgorithm(config);
});

builder.Services.AddScoped<ChargingCoordinator>();

builder.Services.AddControllers();
builder.Services.AddLogging();

var app = builder.Build();

// Enable static files
app.UseStaticFiles();

// Add middleware to handle X-Ingress-Path header for Home Assistant ingress proxy
app.Use(async (context, next) =>
{
    var ingressPath = context.Request.Headers["X-Ingress-Path"].FirstOrDefault() ?? "";
    if (!string.IsNullOrEmpty(ingressPath))
    {
        context.Items["IngressPath"] = ingressPath;
    }
    await next();
});

// Start background charging cycle
var chargingBackgroundService = app.Services.GetRequiredService<ChargingCoordinator>();

_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            //await chargingBackgroundService.ExecuteChargingCycleAsync();
            //await Task.Delay(5000); // Run every 5 seconds
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Background charging cycle error");
        }
    }
});

app.MapGet("/", () => Results.File("config.html", "text/html"));
app.MapGet("/config", () => Results.File("config.html", "text/html"));
app.MapGet("/debug", () => Results.File("debug.html", "text/html"));
app.MapGet("/diagnostic", () => Results.File("diagnostic.html", "text/html"));
app.MapControllers();
app.Run();