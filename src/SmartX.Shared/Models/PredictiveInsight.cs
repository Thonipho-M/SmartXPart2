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
}
