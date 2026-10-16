using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public static class TelemetryAlertEvaluator
{
    public static List<TelemetryAlert> Evaluate(
        IEnumerable<SensorProfile> sensors,
        IEnumerable<TelemetryPacket<float>> floatPackets,
        IEnumerable<TelemetryPacket<int>> integerPackets,
        IEnumerable<TelemetryPacket<bool>> booleanPackets,
        DateTime nowUtc)
    {
        var alerts = new List<TelemetryAlert>();

        // Only the newest value for each device and metric represents its current alert state.
        foreach (var packet in floatPackets
                     .Where(packet => packet.MetricName == "Soil Moisture")
                     .GroupBy(packet => new { packet.DeviceId, packet.MetricName })
                     .Select(group => group.MaxBy(packet => packet.RecordedAtUtc)!)
                     .Where(packet => packet.Value < 35))
        {
            alerts.Add(new TelemetryAlert
            {
                DeviceId = packet.DeviceId,
                MetricName = packet.MetricName,
                Severity = AlertSeverity.Critical,
                Message = $"Low soil moisture detected: {packet.Value}%.",
                SuggestedAction = "Inspect the irrigation line and confirm the sensor is calibrated.",
                DetectedAtUtc = packet.RecordedAtUtc
            });
        }

        var powerSpikeThreshold = new PowerReading(1000);
        foreach (var packet in integerPackets
                     .Where(packet => packet.MetricName == "Power Usage Watts")
                     .GroupBy(packet => new { packet.DeviceId, packet.MetricName })
                     .Select(group => group.MaxBy(packet => packet.RecordedAtUtc)!)
                     .Where(packet => new PowerReading(packet.Value) > powerSpikeThreshold))
        {
            alerts.Add(new TelemetryAlert
            {
                DeviceId = packet.DeviceId,
                MetricName = packet.MetricName,
                Severity = AlertSeverity.Critical,
                Message = $"Power spike detected: {packet.Value} watts.",
                SuggestedAction = "Check connected equipment and compare this reading with recent power usage.",
                DetectedAtUtc = packet.RecordedAtUtc
            });
        }

        foreach (var sensor in sensors)
        {
            var lastReceivedUtc = floatPackets.Where(packet => packet.DeviceId == sensor.DeviceId)
                .Select(packet => packet.RecordedAtUtc)
                .Concat(integerPackets.Where(packet => packet.DeviceId == sensor.DeviceId).Select(packet => packet.RecordedAtUtc))
                .Concat(booleanPackets.Where(packet => packet.DeviceId == sensor.DeviceId).Select(packet => packet.RecordedAtUtc))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            if (lastReceivedUtc != DateTime.MinValue && nowUtc - lastReceivedUtc > TimeSpan.FromMinutes(10))
            {
                alerts.Add(new TelemetryAlert
                {
                    DeviceId = sensor.DeviceId,
                    MetricName = "Connection status",
                    Severity = AlertSeverity.Warning,
                    Message = $"Sensor has not reported telemetry for {(int)(nowUtc - lastReceivedUtc).TotalMinutes} minutes.",
                    SuggestedAction = "Check device power, network connection and the last known sensor reading.",
                    DetectedAtUtc = nowUtc
                });
            }
        }

        return alerts.OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.DetectedAtUtc)
            .ToList();
    }
}
