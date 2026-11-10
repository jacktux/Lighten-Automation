using Microsoft.AspNetCore.Mvc;
using EV_Charging.Services;

namespace EV_Charging.Controllers;

[ApiController]
[Route("api/charging")]
public class ChargingController : ControllerBase
{
    private readonly ChargingCoordinator _coordinator;
    private readonly ChargingAlgorithm _charging;
    private readonly ILogger<ChargingController> _logger;

    public ChargingController(ChargingCoordinator coordinator, ChargingAlgorithm charging, ILogger<ChargingController> logger)
    {
        _coordinator = coordinator;
        _charging = charging;
        _logger = logger;
    }

    [HttpPost("cycle")]
    public async Task<IActionResult> ExecuteCycle()
    {
        try
        {
            await _coordinator.ExecuteChargingCycleAsync();
            return Ok(new { message = "Charging cycle executed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in charging cycle endpoint");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("idealamps")]
    public IActionResult IdealAmps([FromQuery] double gridPower, [FromQuery] double batteryPower)
    {
        try
        {
            _charging.CalculateIdealAmps(gridPower, batteryPower);
            var state = _charging.GetState();
            return Ok(new { idealMaxAmps = state.IdealMaxAmps, availableWatts = state.AvailableWatts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating ideal amps");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
