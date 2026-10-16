using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class PredictiveRecommendationEngine(OperatorActivityTracker activityTracker)
{
    public IReadOnlyList<PredictiveInsight> Generate(
        IEnumerable<TelemetryAlert> activeAlerts,
        IEnumerable<SensorProfile> sensors)
    {
        var sensorList = sensors.ToList();
        var insights = new List<PredictiveInsight>();
        var handledDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alert in activeAlerts.Where(alert => alert.Severity == AlertSeverity.Critical))
        {
            if (!handledDevices.Add(alert.DeviceId))
            {
                continue;
            }

            var suggestedControl = GetSuggestedControl(alert, sensorList);
            insights.Add(new PredictiveInsight
            {
                DeviceId = alert.DeviceId,
                Title = $"Immediate attention: {alert.MetricName}",
                Message = alert.Message,
                SuggestedAction = suggestedControl?.Description ?? alert.SuggestedAction,
                Confidence = "High",
                Evidence = "A current critical telemetry alert triggered this recommendation.",
                RecommendedDeviceId = suggestedControl?.DeviceId,
                RecommendedCommand = suggestedControl?.Command,
                RecommendedTargetState = suggestedControl?.TargetState
            });

            foreach (var relatedDeviceId in activityTracker.GetFrequentFollowUps(alert.DeviceId).Take(2))
            {
                var relatedDevice = sensorList.FirstOrDefault(sensor =>
                    sensor.DeviceId.Equals(relatedDeviceId, StringComparison.OrdinalIgnoreCase));

                if (relatedDevice is null)
                {
                    continue;
                }

                insights.Add(new PredictiveInsight
                {
                    DeviceId = relatedDevice.DeviceId,
                    Title = $"Likely follow-up device: {relatedDevice.DeviceId}",
                    Message = $"Operators have previously moved from {alert.DeviceId} to this device during an investigation.",
                    SuggestedAction = $"Review {relatedDevice.DeviceId} at {relatedDevice.DeploymentLocation} next.",
                    Confidence = "Pattern-based",
                    Evidence = "Based on recorded search and manual-command sequences."
                });
            }
        }

        return insights;
    }

    private static SuggestedControl? GetSuggestedControl(TelemetryAlert alert, List<SensorProfile> sensors)
    {
        if (alert.MetricName == "Soil Moisture")
        {
            var actuator = sensors.FirstOrDefault(sensor => sensor.DeviceId == "FARM-VALVE-01" &&
                sensor.Category == SensorCategory.Actuator);

            return actuator is null
                ? null
                : new SuggestedControl(
                    actuator.DeviceId,
                    "Open valve",
                    "Open",
                    $"Open {actuator.DeviceId} to restore irrigation, if it is safe to do so.");
        }

        if (alert.MetricName == "Power Usage Watts")
        {
            var actuator = sensors.FirstOrDefault(sensor => sensor.DeviceId == "GRID-RELAY-01" &&
                sensor.Category == SensorCategory.Actuator);

            return actuator is null
                ? null
                : new SuggestedControl(
                    actuator.DeviceId,
                    "Toggle smart relay",
                    "Off",
                    $"Turn off {actuator.DeviceId} to reduce the power load, if it is safe to do so.");
        }

        return null;
    }

    private sealed record SuggestedControl(
        string DeviceId,
        string Command,
        string TargetState,
        string Description);
}
