using EV_Charging.Models;

namespace EV_Charging.Services;

/// <summary>
/// Core charging algorithm logic converted from Node-RED flow
/// </summary>
public class ChargingAlgorithm
{
    private readonly EvChargingConfig _config;
    private ChargingState _state;

    public ChargingAlgorithm(EvChargingConfig config)
    {
        _config = config;
        _state = new ChargingState();
    }

    /// <summary>
    /// Battery Protection: Check if battery SOC is above minimum threshold
    /// </summary>
    public bool CheckBatteryProtection(double batterySoc)
    {
        if (batterySoc < _config.RequiredBatterySoc)
        {
            return false; // Battery below floor, stop charging
        }
        return true; // Battery OK, continue
    }

    /// <summary>
    /// Solar Grab Check: Determine if we should attempt solar charging
    /// </summary>
    public void CheckSolarGrab(double batterySoc, double trackingSoc)
    {
        var threshold = trackingSoc > 0 ? trackingSoc : _config.DefaultTrackingSoc;
        _state.SolarGrabActive = batterySoc >= threshold;
        _state.StatusText = _state.SolarGrabActive 
            ? $"Solar Grab Active: {batterySoc:F1}%/{threshold:F0}%" 
            : $"Waiting for threshold: {batterySoc:F1}%/{threshold:F0}%";
    }

    /// <summary>
    /// Calculate available watts for EV charging
    /// </summary>
    public int CalculateAvailableWatts(double solarWatts, double batteryPower, 
        double chargingPower, double gridPower)
    {
        var exportWatts = Math.Max(0, -gridPower);
        int availableWatts;

        // Battery charging: reserve power for battery, send rest to EV
        if (batteryPower < 0)
        {
            availableWatts = (int)(solarWatts 
                - _config.BufferWatts 
                - _config.BatteryChargeTargetWatts);
        }
        // Battery discharging: back off EV charging quickly
        else
        {
            availableWatts = (int)(chargingPower
                - Math.Max(0, batteryPower - _config.MaxBatteryDrainWatts)
                - _config.BufferWatts);
        }

        availableWatts += (int)exportWatts;
        _state.AvailableWatts = Math.Max(0, availableWatts);

        return _state.AvailableWatts;
    }

    /// <summary>
    /// Calculate ideal max amps based on available power
    /// </summary>
    public void CalculateIdealAmps(double gridPower, double batteryPower)
    {
        _state.IdealMaxAmps = Math.Max(0, Math.Min(
            _config.MaxAmps,
            (int)Math.Ceiling((double)_state.AvailableWatts / _config.WattsPerAmp)
        ));

        // Export Boost: Force max amps when exporting power
        if (_config.ForceMaxIfExporting)
        {
            var exporting = gridPower < -_config.ExportBoostWatts;
            var batteryCharging = batteryPower < 0;
            var batteryCondition = !_config.RequireBatteryChargingForBoost || batteryCharging;

            if (exporting && batteryCondition)
            {
                if (!_state.ExportBoostStart.HasValue)
                {
                    _state.ExportBoostStart = DateTime.UtcNow;
                }

                var elapsedSeconds = (DateTime.UtcNow - _state.ExportBoostStart.Value).TotalSeconds;
                
                if (elapsedSeconds >= _config.ExportBoostDelaySeconds 
                    && _state.IdealMaxAmps >= (_config.MaxAmps - 1))
                {
                    _state.IdealMaxAmps = _config.MaxAmps;
                }

                _state.ExportBoostActive = elapsedSeconds >= _config.ExportBoostDelaySeconds;
            }
            else
            {
                _state.ExportBoostStart = null;
                _state.ExportBoostActive = false;
            }
        }
    }

    /// <summary>
    /// Low Surplus Timer: Track when available amps drop below threshold
    /// </summary>
    public void UpdateLowSurplusTimer()
    {
        if (_state.IdealMaxAmps < _config.StopThreshold)
        {
            if (!_state.StopCountdownStart.HasValue)
            {
                _state.StopCountdownStart = DateTime.UtcNow;
            }

            var elapsedSeconds = (DateTime.UtcNow - _state.StopCountdownStart.Value).TotalSeconds;
            _state.StopCountdownRemaining = Math.Max(0,
                _config.StopDelaySeconds - (int)elapsedSeconds);
            _state.StopRequired = elapsedSeconds >= _config.StopDelaySeconds;
        }
        else
        {
            _state.StopCountdownStart = null;
            _state.StopCountdownRemaining = 0;
            _state.StopRequired = false;
        }
    }

    /// <summary>
    /// Ramp Controller: Smoothly adjust target amps based on ideal amps
    /// </summary>
    public void CalculateTargetAmps()
    {
        var current = _state.LastSetAmps ?? 0;

        if (_state.StopRequired)
        {
            _state.TargetAmps = 0;
            return;
        }

        int target = current;

        if (current == 0)
        {
            target = _config.MinAmps;
        }
        else if (_state.IdealMaxAmps > current)
        {
            var diff = _state.IdealMaxAmps - current;
            var step = diff > _config.RampUpDiffThreshold 
                ? _config.RampUpStepLarge 
                : _config.RampUpStepSmall;
            target = current + step;
        }
        else if (_state.IdealMaxAmps < current)
        {
            target = Math.Max(_state.IdealMaxAmps, _config.MinAmps);
        }

        _state.TargetAmps = Math.Max(
            _config.MinAmps,
            Math.Min(_config.MaxAmps, target)
        );
    }

    /// <summary>
    /// Generate charging command with duplicate suppression
    /// </summary>
    public ChargingCommand? GenerateCommand(double chargingPower)
    {
        // STOP
        if (_state.StopRequired)
        {
            if (_state.LastCommand == "STOP")
            {
                return null; // Suppress duplicate
            }

            _state.LastCommand = "STOP";
            _state.LastAmps = null;

            return new ChargingCommand
            {
                Command = "STOP",
                Reason = "Low surplus timer expired"
            };
        }

        // EV should be charging but isn't - restart
        if (_state.TargetAmps >= _config.MinAmps 
            && chargingPower < _config.EvChargingThresholdWatts)
        {
            _state.LastCommand = "START";
            _state.LastAmps = _state.TargetAmps;

            return new ChargingCommand
            {
                Command = "START",
                TargetAmps = _state.TargetAmps,
                Reason = $"Restart from {chargingPower:F0}W"
            };
        }

        // Duplicate suppression
        if (_state.LastCommand == "START" && _state.LastAmps == _state.TargetAmps)
        {
            return null; // No change needed
        }

        // Send update
        _state.LastCommand = "START";
        _state.LastAmps = _state.TargetAmps;

        return new ChargingCommand
        {
            Command = "START",
            TargetAmps = _state.TargetAmps,
            Reason = "Normal operation"
        };
    }

    /// <summary>
    /// Generate comprehensive status text
    /// </summary>
    public string GenerateStatusText(double batteryPower, double gridPower)
    {
        var batteryState = batteryPower < 0
            ? $"Charge {Math.Abs(batteryPower):F0}W"
            : $"Drain {batteryPower:F0}W";

        var status = $"{_state.TargetAmps}A " +
            $"Cap:{_state.IdealMaxAmps}A " +
            $"Avail:{_state.AvailableWatts}W " +
            $"{batteryState} " +
            $"Grid:{gridPower:F0}W";

        if (_state.StopCountdownRemaining > 0)
        {
            status += $" Stop:{_state.StopCountdownRemaining}s";
        }

        return status;
    }

    public ChargingState GetState() => _state;

    public void ResetState()
    {
        _state = new ChargingState();
    }
}