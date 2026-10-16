namespace SmartX.Shared.Models;

// A clear, explainable recommendation shown before an operator starts investigating.
public class PredictiveInsight
{
    public string DeviceId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string SuggestedAction { get; init; } = string.Empty;

    public string Confidence { get; init; } = string.Empty;

    public string Evidence { get; init; } = string.Empty;

    // When present, this maps a monitored alert to the actuator that can safely address it.
    public string? RecommendedDeviceId { get; init; }

    public string? RecommendedCommand { get; init; }

    public string? RecommendedTargetState { get; init; }
}
