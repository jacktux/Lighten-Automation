using EV_Charging.Models;
using EV_Charging.Services;
using HADotNet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Read Home Assistant environment variables
var haUrl = Environment.GetEnvironmentVariable("SUPERVISOR_URL") ?? "http://supervisor";
var haToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") ?? "";

// Configure Kestrel to listen on all interfaces
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(9801);
});

// Register configuration
builder.Services.Configure<EvChargingConfig>(
    builder.Configuration.GetSection("EvConfiguration"));

// Register Home Assistant client
if (!string.IsNullOrEmpty(haToken))
{
    var uri = new Uri(haUrl.EndsWith("/") ? haUrl : haUrl + "/");
    ClientFactory.Initialize(uri, haToken);
}

// Register services
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IOptions<EvChargingConfig>>().Value;
    return new ChargingAlgorithm(config);
});

builder.Services.AddScoped<HomeAssistantService>();
builder.Services.AddScoped<ChargingCoordinator>();

builder.Services.AddControllers();
builder.Services.AddLogging();

var app = builder.Build();

// Start background charging cycle
var chargingBackgroundService = app.Services.GetRequiredService<ChargingCoordinator>();

_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            await chargingBackgroundService.ExecuteChargingCycleAsync();
            await Task.Delay(5000); // Run every 5 seconds
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Background charging cycle error");
        }
    }
});

app.MapGet("/", () => "Lighten Automation - EV Charging Service");
app.MapControllers();
app.Run();