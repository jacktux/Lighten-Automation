using Microsoft.AspNetCore.Mvc;
using EV_Charging.Models;
using EV_Charging.Services;

namespace EV_Charging.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly HomeAssistantService _homeAssistantService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(HomeAssistantService homeAssistantService, ILogger<ConfigController> logger)
    {
        _homeAssistantService = homeAssistantService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        try
        {
            var config = _homeAssistantService.GetConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult UpdateConfig([FromBody] EvChargingConfig newConfig)
    {
        try
        {
            _homeAssistantService.UpdateConfig(newConfig);
            return Ok(new { message = "Configuration updated successfully", config = newConfig });
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogWarning(ex, "Invalid configuration");
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Configuration validation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("reset")]
    public IActionResult ResetConfig()
    {
        try
        {
            _homeAssistantService.ResetConfig();
            var config = _homeAssistantService.GetConfig();
            return Ok(new { message = "Configuration reset to defaults", config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}