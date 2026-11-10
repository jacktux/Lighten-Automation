namespace EV_Charging.Models;
public class EnergyState
{
 public double BatterySoc { get; set; }
 public int SolarWatts { get; set; }
 public int HouseLoadWatts { get; set; }
 public int BatteryPowerWatts { get; set; }
 public int GridExportWatts { get; set; }
 public bool CarConnected { get; set; }
 public bool Charging { get; set; }
 public int CurrentAmps { get; set; }
}