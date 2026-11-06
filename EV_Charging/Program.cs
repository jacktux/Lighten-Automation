using HADotNet.Core;
using HADotNet.Core.Clients;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Read Home Assistant internal Supervisor variables
var haUrl = Environment.GetEnvironmentVariable("SUPERVISOR_URL") ?? "http://supervisor";
var haToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") ?? "";

// FORCE ASP.NET to listen on all internal container networks
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(9801); // Binds to 0.0.0.0:9801 inside the container
});

// Initialize the Home Assistant client factory at startup
if (!string.IsNullOrEmpty(haToken))
{
    var uri = new Uri(haUrl.EndsWith("/") ? haUrl : haUrl + "/");
    ClientFactory.Initialize(uri, haToken);
}

var app = builder.Build();

app.MapGet("/", () => "Lighten Automation - EV Charging - Home Page");

// Sample endpoint: Triggers a device turn-on command via your C# API
app.MapPost("/api/turn-on", async ([FromQuery] string entityId) =>
{
    try
    {
        var serviceClient = ClientFactory.GetClient<ServiceClient>();
        var parts = entityId.Split('.');
        var domain = parts[0];

        await serviceClient.CallService(domain, "turn_on", new { entity_id = entityId });
        return Results.Ok(new { message = $"Successfully triggered {entityId}" });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.Run();