namespace SmartX.Shared.Models;

// A display-friendly version of a typed telemetry packet for the live command stream.
public class CommandStreamTelemetry
{
    public string DeviceId { get; init; } = string.Empty;

    public string MetricName { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string ValueType { get; init; } = string.Empty;

    public DateTime RecordedAtUtc { get; init; }
}
