namespace EV_Charging.Models;

public class EvConfiguration
{
 public int WattsPerAmp { get; set; }=380;
 public int MinAmps { get; set; }=6;
 public int MaxAmps { get; set; }=20;
 public int BufferWatts { get; set; }=100;
 public int BatteryChargeTargetWatts { get; set; }=500;
 public int MaxBatteryDrainWatts { get; set; }=100;
 public int RequiredBatterySoc { get; set; }=20;
 public int DefaultTrackingSoc { get; set; }=90;
 public int StopThreshold { get; set; }=5;
 public int StopDelaySeconds { get; set; }=120;
}