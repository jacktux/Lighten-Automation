using EV_Charging.Models;

namespace EV_Charging.Services;

/// <summary>
/// Orchestrates the full charging cycle by coordinating algorithm, HA communication, and timers
/// </summary>
public class ChargingCoordinator
{
    private readonly ChargingAlgorithm _algorithm;
    private readonly HomeAssistantService _haService;
    private readonly ILogger<ChargingCoordinator> _logger;
    private readonly EvChargingConfig _config;
    
    private DateTime _lastExecutionTime = DateTime.MinValue;
    private const int ExecutionIntervalMs = 5000; // Run every 5 seconds

    public ChargingCoordinator(
        EvChargingConfig config,
        ChargingAlgorithm algorithm,
        HomeAssistantService haService,
        ILogger<ChargingCoordinator> logger)
    {
        _config = config;
        _algorithm = algorithm;
        _haService = haService;
        _logger = logger;
    }

    /// <summary>
    /// Main execution loop - called periodically to process one charging cycle
    /// </summary>
    public async Task ExecuteChargingCycleAsync()
    {
        // Rate limiting
        var elapsed = DateTime.UtcNow - _lastExecutionTime;
        if (elapsed.TotalMilliseconds < ExecutionIntervalMs)
        {
            return;
        }
        _lastExecutionTime = DateTime.UtcNow;

        try
        {
            // Step 1: Check preconditions
            var carAtHome = await _haService.IsCarAtHomeAsync();
            var carPluggedIn = await _haService.IsCarPluggedInAsync();

            if (!carAtHome || !carPluggedIn)
            {
                _logger.LogDebug("Charging disabled: Car not at home or not plugged in");
                return;
            }

            // Step 2: Fetch energy readings
            var readings = await _haService.FetchEnergyReadingsAsync();
            if (readings == null)
            {
                _logger.LogWarning("Failed to retrieve energy readings, skipping cycle");
                return;
            }

            // Step 3: Battery Protection
            if (!_algorithm.CheckBatteryProtection(readings.BatterySoc))
            {
                _logger.LogWarning("Battery protection triggered: SoC {SoC}% below {Min}%",
                    readings.BatterySoc, _config.RequiredBatterySoc);
                await _haService.ExecuteChargingCommandAsync(new ChargingCommand
                {
                    Command = "STOP",
                    Reason = "Battery floor protection"
                });
                return;
            }

            // Step 4: Solar Grab Check
            _algorithm.CheckSolarGrab(readings.BatterySoc, readings.TrackingSoc);
            if (!_algorithm.GetState().SolarGrabActive)
            {
                _logger.LogDebug("Solar grab not active, waiting for sufficient charge");
                return;
            }

            // Step 5: Calculate Available Watts
            _algorithm.CalculateAvailableWatts(
                readings.SolarPower,
                readings.BatteryPower,
                readings.ChargingPower,
                readings.GridPower
            );

            // Step 6: Calculate Ideal Amps
            _algorithm.CalculateIdealAmps(readings.GridPower, readings.BatteryPower);

            // Step 7: Update Low Surplus Timer
            _algorithm.UpdateLowSurplusTimer();

            // Step 8: Calculate Target Amps
            _algorithm.CalculateTargetAmps();

            // Step 9: Generate Command
            var command = _algorithm.GenerateCommand(readings.ChargingPower);
            
            if (command != null)
            {
                await _haService.ExecuteChargingCommandAsync(command);
                
                if (command.Command == "START" && command.TargetAmps.HasValue)
                {
                    await _haService.SetTargetCurrentAsync(command.TargetAmps.Value);
                }
            }

            // Step 10: Update Status
            var statusText = _algorithm.GenerateStatusText(readings.BatteryPower, readings.GridPower);
            await _haService.UpdateStatusAsync(statusText);

            _logger.LogInformation("Charging cycle complete: {Status}", statusText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing charging cycle");
        }
    }

    /// <summary>
    /// Get the current charging state for debugging
    /// </summary>
    public ChargingState GetState()
    {
        return _algorithm.GetState();
    }
}