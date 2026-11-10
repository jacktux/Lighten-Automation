using EV_Charging.Models;
using HADotNet.Core;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EV_Charging.Services;

/// <summary>
/// Handles communication with Home Assistant via WebSocket and REST API
/// </summary>
public class HomeAssistantService
{
    private readonly ILogger<HomeAssistantService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IOptions<EvChargingConfig> _configOptions;

    public HomeAssistantService(ILogger<HomeAssistantService> logger, HttpClient httpClient, IOptions<EvChargingConfig> configOptions)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configOptions = configOptions;
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

    /// <summary>
    /// Get configuration
    /// </summary>
    public EvChargingConfig GetConfig()
    {
        return _configOptions.Value;
    }

    /// <summary>
    /// Update configuration
    /// </summary>
    public void UpdateConfig(EvChargingConfig newConfig)
    {
        if (newConfig == null)
        {
            throw new ArgumentNullException(nameof(newConfig), "Configuration cannot be null");
        }

        // Validate ranges
        if (newConfig.MinAmps < 1 || newConfig.MinAmps > newConfig.MaxAmps)
        {
            throw new ArgumentException("MinAmps must be between 1 and MaxAmps", nameof(newConfig.MinAmps));
        }

        if (newConfig.MaxAmps < newConfig.MinAmps || newConfig.MaxAmps > 100)
        {
            throw new ArgumentException("MaxAmps must be between MinAmps and 100", nameof(newConfig.MaxAmps));
        }

        if (newConfig.WattsPerAmp < 100 || newConfig.WattsPerAmp > 1000)
        {
            throw new ArgumentException("WattsPerAmp must be between 100 and 1000", nameof(newConfig.WattsPerAmp));
        }

        if (newConfig.RequiredBatterySoc < 0 || newConfig.RequiredBatterySoc > 100)
        {
            throw new ArgumentException("RequiredBatterySoc must be between 0 and 100", nameof(newConfig.RequiredBatterySoc));
        }

        if (newConfig.DefaultTrackingSoc < 0 || newConfig.DefaultTrackingSoc > 100)
        {
            throw new ArgumentException("DefaultTrackingSoc must be between 0 and 100", nameof(newConfig.DefaultTrackingSoc));
        }

        // Update the in-memory configuration
        var currentConfig = _configOptions.Value;
        currentConfig.WattsPerAmp = newConfig.WattsPerAmp;
        currentConfig.MinAmps = newConfig.MinAmps;
        currentConfig.MaxAmps = newConfig.MaxAmps;
        currentConfig.BufferWatts = newConfig.BufferWatts;
        currentConfig.BatteryChargeTargetWatts = newConfig.BatteryChargeTargetWatts;
        currentConfig.MaxBatteryDrainWatts = newConfig.MaxBatteryDrainWatts;
        currentConfig.RequiredBatterySoc = newConfig.RequiredBatterySoc;
        currentConfig.DefaultTrackingSoc = newConfig.DefaultTrackingSoc;
        currentConfig.StopThreshold = newConfig.StopThreshold;
        currentConfig.StopDelaySeconds = newConfig.StopDelaySeconds;
        currentConfig.RampUpStepSmall = newConfig.RampUpStepSmall;
        currentConfig.RampUpStepLarge = newConfig.RampUpStepLarge;
        currentConfig.RampUpDiffThreshold = newConfig.RampUpDiffThreshold;
        currentConfig.AmpDeadband = newConfig.AmpDeadband;
        currentConfig.ForceMaxIfExporting = newConfig.ForceMaxIfExporting;
        currentConfig.ExportBoostWatts = newConfig.ExportBoostWatts;
        currentConfig.RequireBatteryChargingForBoost = newConfig.RequireBatteryChargingForBoost;
        currentConfig.ExportBoostDelaySeconds = newConfig.ExportBoostDelaySeconds;
        currentConfig.EvChargingThresholdWatts = newConfig.EvChargingThresholdWatts;

        _logger.LogInformation("Configuration updated successfully");
    }

    /// <summary>
    /// Reset configuration to defaults
    /// </summary>
    public void ResetConfig()
    {
        var defaultConfig = new EvChargingConfig();

        var currentConfig = _configOptions.Value;
        currentConfig.WattsPerAmp = defaultConfig.WattsPerAmp;
        currentConfig.MinAmps = defaultConfig.MinAmps;
        currentConfig.MaxAmps = defaultConfig.MaxAmps;
        currentConfig.BufferWatts = defaultConfig.BufferWatts;
        currentConfig.BatteryChargeTargetWatts = defaultConfig.BatteryChargeTargetWatts;
        currentConfig.MaxBatteryDrainWatts = defaultConfig.MaxBatteryDrainWatts;
        currentConfig.RequiredBatterySoc = defaultConfig.RequiredBatterySoc;
        currentConfig.DefaultTrackingSoc = defaultConfig.DefaultTrackingSoc;
        currentConfig.StopThreshold = defaultConfig.StopThreshold;
        currentConfig.StopDelaySeconds = defaultConfig.StopDelaySeconds;
        currentConfig.RampUpStepSmall = defaultConfig.RampUpStepSmall;
        currentConfig.RampUpStepLarge = defaultConfig.RampUpStepLarge;
        currentConfig.RampUpDiffThreshold = defaultConfig.RampUpDiffThreshold;
        currentConfig.AmpDeadband = defaultConfig.AmpDeadband;
        currentConfig.ForceMaxIfExporting = defaultConfig.ForceMaxIfExporting;
        currentConfig.ExportBoostWatts = defaultConfig.ExportBoostWatts;
        currentConfig.RequireBatteryChargingForBoost = defaultConfig.RequireBatteryChargingForBoost;
        currentConfig.ExportBoostDelaySeconds = defaultConfig.ExportBoostDelaySeconds;
        currentConfig.EvChargingThresholdWatts = defaultConfig.EvChargingThresholdWatts;

        _logger.LogInformation("Configuration reset to defaults");
    }
}