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

            var suggestedAction = GetAlertAction(alert, sensorList);
            insights.Add(new PredictiveInsight
            {
                DeviceId = alert.DeviceId,
                Title = $"Immediate attention: {alert.MetricName}",
                Message = alert.Message,
                SuggestedAction = suggestedAction,
                Confidence = "High",
                Evidence = "A current critical telemetry alert triggered this recommendation."
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

    private static string GetAlertAction(TelemetryAlert alert, List<SensorProfile> sensors)
    {
        if (alert.MetricName == "Soil Moisture")
        {
            var actuator = sensors.FirstOrDefault(sensor => sensor.Category == SensorCategory.Actuator);
            return actuator is null
                ? alert.SuggestedAction
                : $"Review {actuator.DeviceId}; if it is safe to do so, issue an Open valve manual override.";
        }

        return alert.SuggestedAction;
    }
}
