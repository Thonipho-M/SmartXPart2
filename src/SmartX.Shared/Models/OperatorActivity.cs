namespace SmartX.Shared.Models;

// Records an operator interaction used by the predictive recommendation engine.
public class OperatorActivity
{
    public string DeviceId { get; init; } = string.Empty;

    public OperatorActivityKind Kind { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
