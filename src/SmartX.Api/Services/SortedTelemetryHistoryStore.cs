using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class SortedTelemetryHistoryStore
{
    private readonly object syncRoot = new();

    // SortedDictionary automatically keeps telemetry buckets in ascending timestamp order.
    private readonly SortedDictionary<DateTime, List<CommandStreamTelemetry>> historyByTimestamp = new();

    public void Add<T>(TelemetryPacket<T> packet)
    {
        var timestamp = packet.RecordedAtUtc.ToUniversalTime();
        var historyItem = new CommandStreamTelemetry
        {
            DeviceId = packet.DeviceId,
            MetricName = packet.MetricName,
            Value = packet.Value?.ToString() ?? string.Empty,
            ValueType = typeof(T).Name,
            RecordedAtUtc = timestamp
        };

        lock (syncRoot)
        {
            if (!historyByTimestamp.TryGetValue(timestamp, out var timestampEntries))
            {
                timestampEntries = [];
                historyByTimestamp.Add(timestamp, timestampEntries);
            }

            timestampEntries.Add(historyItem);
        }
    }

    public IReadOnlyList<CommandStreamTelemetry> GetHistoryForDevice(string deviceId)
    {
        lock (syncRoot)
        {
            return historyByTimestamp
                .SelectMany(timestampBucket => timestampBucket.Value)
                .Where(packet => packet.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
