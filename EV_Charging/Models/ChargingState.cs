namespace EV_Charging.Models;

/// <summary>
/// Mutable state tracking for charging algorithm
/// </summary>
public class ChargingState
{
    public int? LastSetAmps { get; set; }
    public string? LastCommand { get; set; }
    public int? LastAmps { get; set; }
    
    public DateTime? StopCountdownStart { get; set; }
    public int StopCountdownRemaining { get; set; }
    public bool StopRequired { get; set; }
    
    public DateTime? ExportBoostStart { get; set; }
    public bool ExportBoostActive { get; set; }
    
    public DateTime? BatteryPriorityStart { get; set; }
    public bool BatteryPriorityStopped { get; set; }
    
    public int IdealMaxAmps { get; set; }
    public int TargetAmps { get; set; }
    public int AvailableWatts { get; set; }
    public bool SolarGrabActive { get; set; }
    
    public string StatusText { get; set; } = string.Empty;
}