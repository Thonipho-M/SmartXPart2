namespace SmartX.Shared.Models;

public class ManualOverrideUndoResult
{
    public ManualOverrideCommand RevertedCommand { get; init; } = new();

    public string RestoredState { get; init; } = string.Empty;

    public DateTime RevertedAtUtc { get; init; } = DateTime.UtcNow;
}
