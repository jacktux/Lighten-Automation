using Microsoft.Extensions.Options;
using EV_Charging.Models;

namespace EV_Charging.Services;
public class ChargingService
{
 private readonly EvConfiguration _config;
 public ChargingService(IOptions<EvConfiguration> cfg){_config=cfg.Value;}
 public int CalculateAvailableWatts(int solarWatts)=>Math.Max(0, solarWatts-_config.BatteryChargeTargetWatts-_config.BufferWatts);
 public int CalculateIdealAmps(int availableWatts)=>Math.Clamp(availableWatts/_config.WattsPerAmp,_config.MinAmps,_config.MaxAmps);
}
