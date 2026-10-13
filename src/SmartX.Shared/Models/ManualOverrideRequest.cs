namespace SmartX.Shared.Models;

// Sent by an operator when manually changing an actuator's state.
public class ManualOverrideRequest
{
    public string DeviceId { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string TargetState { get; set; } = string.Empty;
}
