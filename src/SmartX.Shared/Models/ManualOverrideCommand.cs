namespace SmartX.Shared.Models;

// Stored in the undo stack after a manual command is issued.
public class ManualOverrideCommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();

    public string DeviceId { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string PreviousState { get; init; } = string.Empty;

    public string TargetState { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
}
