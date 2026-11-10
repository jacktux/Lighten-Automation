namespace EV_Charging.Models;

/// <summary>
/// Represents a command to execute on the EV charger
/// </summary>
public class ChargingCommand
{
    /// <summary>
    /// The charging command: "START" or "STOP"
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The target current in amps when starting a charge
    /// </summary>
    public int? TargetAmps { get; set; }

    /// <summary>
    /// Reason for the command (for logging purposes)
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}