using HADotNet.Core;
using EV_Charging.Models;
using System.Text.Json;

namespace EV_Charging.Services;

/// <summary>
/// Handles communication with Home Assistant via WebSocket and REST API
/// </summary>
public class HomeAssistantService
{
    private readonly ILogger<HomeAssistantService> _logger;
    private readonly HttpClient _httpClient;

    public HomeAssistantService(ILogger<HomeAssistantService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetch current energy readings from Home Assistant sensors
    /// </summary>
    public async Task<EnergyReadings?> FetchEnergyReadingsAsync()
    {
        try
        {
            var readings = new EnergyReadings();

            // Fetch each sensor individually
            readings.SolarPower = await FetchSensorStateAsync("sensor.la_electrical_solar_power");
            readings.BatteryPower = await FetchSensorStateAsync("sensor.la_electrical_battery_power");
            readings.BatterySoc = await FetchSensorStateAsync("sensor.la_electrical_battery_soc");
            readings.ChargingPower = await FetchSensorStateAsync("sensor.wattpilot_charging_power");
            readings.GridPower = await FetchSensorStateAsync("sensor.la_electrical_grid_power");
            readings.TrackingSoc = await FetchSensorStateAsync("input_number.ev_solar_grab_threshold");

            readings.Timestamp = DateTime.UtcNow;

            _logger.LogInformation(
                "Energy Readings: Solar={Solar}W, Battery={Battery}W, SoC={SoC}%, Charging={Charging}W, Grid={Grid}W",
                readings.SolarPower, readings.BatteryPower, readings.BatterySoc,
                readings.ChargingPower, readings.GridPower);

            return readings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch energy readings from Home Assistant");
            return null;
        }
    }

    /// <summary>
    /// Fetch a single sensor state from Home Assistant
    /// </summary>
    private async Task<double> FetchSensorStateAsync(string entityId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/states/{entityId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("state", out var stateElement) && 
                    double.TryParse(stateElement.GetString(), out var value))
                {
                    return value;
                }
            }
            return 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch state for {EntityId}", entityId);
            return 0.0;
        }
    }

    /// <summary>
    /// Check if car is at home
    /// </summary>
    public async Task<bool> IsCarAtHomeAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/states/device_tracker.ev3_location");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("state", out var stateElement))
                {
                    return stateElement.GetString()?.Equals("home", StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check car location");
            return false;
        }
    }

    /// <summary>
    /// Check if car is plugged in
    /// </summary>
    public async Task<bool> IsCarPluggedInAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/states/binary_sensor.garage_ev3_ev_battery_plug");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("state", out var stateElement))
                {
                    return stateElement.GetString()?.Equals("on", StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check car plug status");
            return false;
        }
    }

    /// <summary>
    /// Execute charging command (START/STOP)
    /// </summary>
    public async Task ExecuteChargingCommandAsync(ChargingCommand command)
    {
        try
        {
            if (command.Command == "STOP")
            {
                await _httpClient.PostAsJsonAsync("api/services/input_button/press",
                    new { entity_id = "input_button.ev_stop_charging" });
                _logger.LogInformation("Stop charging command sent: {Reason}", command.Reason);
            }
            else if (command.Command == "START")
            {
                await _httpClient.PostAsJsonAsync("api/services/input_button/press",
                    new { entity_id = "input_button.ev_start_charging" });
                _logger.LogInformation("Start charging command sent with {Amps}A: {Reason}",
                    command.TargetAmps, command.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute charging command: {Command}", command.Command);
        }
    }

    /// <summary>
    /// Set target charging current
    /// </summary>
    public async Task SetTargetCurrentAsync(int amps)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("api/services/input_number/set_value",
                new { entity_id = "input_number.wattpilot_target_current_ui", value = amps });
            _logger.LogInformation("Set target current to {Amps}A", amps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set target current");
        }
    }

    /// <summary>
    /// Update status text in Home Assistant
    /// </summary>
    public async Task UpdateStatusAsync(string status)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("api/services/input_text/set_value",
                new { entity_id = "input_text.ev_charging_status", value = status });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update status text");
        }
    }
}