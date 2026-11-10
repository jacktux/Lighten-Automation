namespace EV_Charging.Models;

/// <summary>
/// Current energy readings from Home Assistant sensors
/// </summary>
public class EnergyReadings
{
    public double SolarPower { get; set; }
    public double BatteryPower { get; set; }
    public double BatterySoc { get; set; }
    public double ChargingPower { get; set; }
    public double GridPower { get; set; }
    public double TrackingSoc { get; set; } = 90;
    
    /// <summary>Timestamp when readings were gathered</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}