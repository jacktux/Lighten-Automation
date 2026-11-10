using Microsoft.AspNetCore.Mvc;
using EV_Charging.Services;

namespace EV_Charging.Controllers;

[ApiController]
[Route("api/diagnostic")]
public class DiagnosticController : ControllerBase
{
    private readonly ILogger<DiagnosticController> _logger;
    private readonly HomeAssistantService _homeAssistantService;

    public DiagnosticController(ILogger<DiagnosticController> logger, HomeAssistantService homeAssistantService)
    {
        _logger = logger;
        _homeAssistantService = homeAssistantService;
    }

    /// <summary>
    /// Get environment and token configuration status
    /// </summary>
    [HttpGet("config-status")]
    public IActionResult GetConfigStatus()
    {
        try
        {
            var haUrl = Environment.GetEnvironmentVariable("SUPERVISOR_URL") ?? "NOT SET";
            var haToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") ?? "NOT SET";
            var hasToken = !string.IsNullOrEmpty(haToken);
            var tokenLength = haToken != "NOT SET" ? haToken.Length : 0;
            var tokenPreview = hasToken ? $"{haToken.Substring(0, Math.Min(10, haToken.Length))}...{haToken.Substring(Math.Max(0, haToken.Length - 10))}" : "NOT SET";

            _logger.LogInformation("Configuration Status - URL: {Url}, Token Present: {HasToken}", haUrl, hasToken);

            return Ok(new
            {
                supervisorUrl = haUrl,
                supervisorTokenPresent = hasToken,
                supervisorTokenLength = tokenLength,
                supervisorTokenPreview = tokenPreview,
                timestamp = DateTime.UtcNow,
                environment = "Production",
                dotnetVersion = Environment.Version.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting config status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test Home Assistant connectivity
    /// </summary>
    [HttpGet("test-connection")]
    public async Task<IActionResult> TestConnection()
    {
        var results = new Dictionary<string, object>();

        try
        {
            // Test 1: Check environment variables
            var haUrl = Environment.GetEnvironmentVariable("SUPERVISOR_URL");
            var haToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
            
            results["Environment Variables"] = new
            {
                supervisorUrlSet = !string.IsNullOrEmpty(haUrl),
                supervisorTokenSet = !string.IsNullOrEmpty(haToken),
                supervisorUrl = haUrl ?? "NOT SET"
            };

            if (string.IsNullOrEmpty(haUrl) || string.IsNullOrEmpty(haToken))
            {
                _logger.LogWarning("Missing environment variables");
                return Ok(results);
            }

            // Test 2: Try to fetch a simple entity to test authorization
            results["Authorization Test"] = await TestAuthorizationAsync();

            // Test 3: Fetch actual entity values
            results["Entity Values"] = await TestEntityFetchAsync();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection");
            results["Error"] = ex.Message;
            return StatusCode(500, results);
        }
    }

    /// <summary>
    /// Detailed token validation
    /// </summary>
    [HttpGet("token-validation")]
    public IActionResult TokenValidation()
    {
        try
        {
            var token = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") ?? string.Empty;
            var url = Environment.GetEnvironmentVariable("SUPERVISOR_URL") ?? string.Empty;

            var validation = new
            {
                tokenSet = !string.IsNullOrEmpty(token),
                tokenLength = token.Length,
                urlSet = !string.IsNullOrEmpty(url),
                urlValue = url,
                tokenFormat = AnalyzeTokenFormat(token),
                recommendations = GetRecommendations(token, url),
                timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Token validation: {Validation}", validation);
            return Ok(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Fetch all entity values with detailed error reporting
    /// </summary>
    [HttpGet("entity-values")]
    public async Task<IActionResult> GetEntityValues()
    {
        try
        {
            var entities = await _homeAssistantService.FetchAllEntityValuesAsync();
            
            var response = new
            {
                success = !entities.ContainsKey("Error"),
                totalEntities = entities.Count,
                timestamp = DateTime.UtcNow,
                entities = entities
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching entity values");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<object> TestAuthorizationAsync()
    {
        try
        {
            var entities = await _homeAssistantService.FetchAllEntityValuesAsync();
            
            if (entities.ContainsKey("Error"))
            {
                return new
                {
                    status = "FAILED",
                    error = entities["Error"],
                    message = "Authorization test failed - check SUPERVISOR_TOKEN"
                };
            }

            return new
            {
                status = "SUCCESS",
                message = "Successfully authenticated with Home Assistant",
                entitiesFetched = entities.Count
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "FAILED",
                error = ex.Message,
                message = "Could not reach Home Assistant API"
            };
        }
    }

    private async Task<object> TestEntityFetchAsync()
    {
        try
        {
            var entities = await _homeAssistantService.FetchAllEntityValuesAsync();
            
            var successCount = entities.Count(x => x.Key != "Error" && !x.Value.ToString()!.Contains("Error") && !x.Value.ToString()!.Contains("N/A"));
            var errorCount = entities.Count(x => x.Key == "Error" || x.Value.ToString()!.Contains("Error"));
            var naCount = entities.Count(x => x.Value.ToString()!.Contains("N/A"));

            return new
            {
                successCount = successCount,
                errorCount = errorCount,
                notAvailableCount = naCount,
                totalCount = entities.Count,
                sample = entities.Take(3).ToDictionary(x => x.Key, x => x.Value)
            };
        }
        catch (Exception ex)
        {
            return new
            {
                status = "FAILED",
                error = ex.Message
            };
        }
    }

    private string AnalyzeTokenFormat(string token)
    {
        if (string.IsNullOrEmpty(token))
            return "EMPTY";

        if (token.Length < 10)
            return "TOO_SHORT - Likely invalid";

        if (token.Contains(" "))
            return "CONTAINS_SPACES - Probably incorrect";

        if (token.All(char.IsLetterOrDigit))
            return "VALID_FORMAT - Alphanumeric only";

        return "VALID_FORMAT - Contains special characters";
    }

    private List<string> GetRecommendations(string token, string url)
    {
        var recommendations = new List<string>();

        if (string.IsNullOrEmpty(token))
            recommendations.Add("CRITICAL: SUPERVISOR_TOKEN is not set. Set it in your Home Assistant add-on configuration.");

        if (string.IsNullOrEmpty(url))
            recommendations.Add("CRITICAL: SUPERVISOR_URL is not set. Set it in your Home Assistant add-on configuration.");

        if (!string.IsNullOrEmpty(token) && token.Length < 10)
            recommendations.Add("WARNING: Token appears to be too short. Verify it's correct.");

        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
            recommendations.Add("WARNING: SUPERVISOR_URL should start with http:// or https://");

        if (!string.IsNullOrEmpty(url) && !url.EndsWith("/"))
            recommendations.Add("INFO: SUPERVISOR_URL should typically end with /");

        if (recommendations.Count == 0)
            recommendations.Add("Configuration appears to be valid. If you're still getting authorization errors, check the debug output.");

        return recommendations;
    }
}