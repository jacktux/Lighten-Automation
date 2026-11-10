namespace EV_Charging.Models;

/// <summary>
/// Configuration for EV charging algorithm matching Node-RED flow
/// </summary>
public class EvChargingConfig
{
    public int WattsPerAmp { get; set; } = 380;
    
    public int MinAmps { get; set; } = 6;
    public int MaxAmps { get; set; } = 20;
    
    public int BufferWatts { get; set; } = 100;
    public int BatteryChargeTargetWatts { get; set; } = 500;
    public int MaxBatteryDrainWatts { get; set; } = 100;
    
    public int RequiredBatterySoc { get; set; } = 20;
    public int DefaultTrackingSoc { get; set; } = 90;
    
    public int StopThreshold { get; set; } = 5;
    public int StopDelaySeconds { get; set; } = 120;
    
    public int RampUpStepSmall { get; set; } = 1;
    public int RampUpStepLarge { get; set; } = 1;
    public int RampUpDiffThreshold { get; set; } = 999;
    
    public int AmpDeadband { get; set; } = 0;
    
    public bool ForceMaxIfExporting { get; set; } = true;
    public int ExportBoostWatts { get; set; } = 150;
    public bool RequireBatteryChargingForBoost { get; set; } = true;
    public int ExportBoostDelaySeconds { get; set; } = 60;
    
    public int EvChargingThresholdWatts { get; set; } = 200;
}