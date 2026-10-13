using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class RealTimeCommandStreamStore
{
    private const int MaximumLivePackets = 100;
    private readonly object syncRoot = new();
    private readonly Queue<CommandStreamTelemetry> telemetryQueue = new();
    private readonly PriorityQueue<TelemetryAlert, int> criticalAlertQueue = new();

    // Queue<T> preserves first-in, first-out order for the live telemetry stream.
    public void EnqueueTelemetry<T>(TelemetryPacket<T> packet)
    {
        var streamPacket = new CommandStreamTelemetry
        {
            DeviceId = packet.DeviceId,
            MetricName = packet.MetricName,
            Value = packet.Value?.ToString() ?? string.Empty,
            ValueType = typeof(T).Name,
            RecordedAtUtc = packet.RecordedAtUtc
        };

        lock (syncRoot)
        {
            telemetryQueue.Enqueue(streamPacket);

            // Discard the oldest packet once the volatile stream reaches its capacity.
            if (telemetryQueue.Count > MaximumLivePackets)
            {
                telemetryQueue.Dequeue();
            }
        }
    }

    // Critical alerts bypass the standard telemetry queue and are ranked first.
    public void UpdateCriticalAlerts(IEnumerable<TelemetryAlert> alerts)
    {
        lock (syncRoot)
        {
            criticalAlertQueue.Clear();

            foreach (var alert in alerts.Where(alert => alert.Severity == AlertSeverity.Critical))
            {
                // PriorityQueue removes lower values first. Newer alerts receive higher priority.
                var timestampSeconds = (int)new DateTimeOffset(alert.DetectedAtUtc.ToUniversalTime())
                    .ToUnixTimeSeconds();
                var priority = int.MaxValue - timestampSeconds;
                criticalAlertQueue.Enqueue(alert, priority);
            }
        }
    }

    public IReadOnlyList<CommandStreamTelemetry> GetLiveTelemetry()
    {
        lock (syncRoot)
        {
            return telemetryQueue.ToList();
        }
    }

    public IReadOnlyList<TelemetryAlert> GetCriticalAlerts()
    {
        lock (syncRoot)
        {
            var copiedQueue = new PriorityQueue<TelemetryAlert, int>(criticalAlertQueue.UnorderedItems);
            var alerts = new List<TelemetryAlert>();

            while (copiedQueue.TryDequeue(out var alert, out _))
            {
                alerts.Add(alert);
            }

            return alerts;
        }
    }
}
