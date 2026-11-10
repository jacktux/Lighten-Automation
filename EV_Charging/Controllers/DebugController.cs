using Microsoft.AspNetCore.Mvc;
using EV_Charging.Services;

namespace EV_Charging.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly HomeAssistantService _homeAssistantService;
    private readonly ChargingCoordinator _coordinator;
    private readonly ILogger<DebugController> _logger;

    public DebugController(HomeAssistantService homeAssistantService, ChargingCoordinator coordinator, ILogger<DebugController> logger)
    {
        _homeAssistantService = homeAssistantService;
        _coordinator = coordinator;
        _logger = logger;
    }

    [HttpGet("entities")]
    public async Task<IActionResult> GetEntityValues()
    {
        try
        {
            var entities = await _homeAssistantService.FetchAllEntityValuesAsync();
            return Ok(new { timestamp = DateTime.UtcNow, entities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching entity values");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("state")]
    public IActionResult GetAlgorithmState()
    {
        try
        {
            var state = _coordinator.GetState();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching algorithm state");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}