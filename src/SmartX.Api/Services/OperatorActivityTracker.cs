using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class OperatorActivityTracker
{
    private const int MaximumActivityEvents = 50;
    private readonly object syncRoot = new();
    private readonly Queue<OperatorActivity> recentActivities = new();
    private readonly Dictionary<string, Dictionary<string, int>> followUpCounts =
        new(StringComparer.OrdinalIgnoreCase);
    private string? previousDeviceId;

    public void RecordSearch(string deviceId)
    {
        Record(deviceId, OperatorActivityKind.Search, "Viewed device history");
    }

    public void RecordManualOverride(ManualOverrideCommand command)
    {
        Record(command.DeviceId, OperatorActivityKind.ManualOverride, command.Command);
    }

    public IReadOnlyList<OperatorActivity> GetRecentActivities()
    {
        lock (syncRoot)
        {
            return recentActivities.Reverse().ToList();
        }
    }

    public IReadOnlyList<string> GetFrequentFollowUps(string deviceId)
    {
        lock (syncRoot)
        {
            if (!followUpCounts.TryGetValue(deviceId, out var relatedDevices))
            {
                return [];
            }

            return relatedDevices
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Select(entry => entry.Key)
                .ToList();
        }
    }

    // A nested dictionary counts sequences such as "Moisture Sensor B" followed by "Pump A".
    private void Record(string deviceId, OperatorActivityKind kind, string description)
    {
        lock (syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(previousDeviceId) &&
                !previousDeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase))
            {
                if (!followUpCounts.TryGetValue(previousDeviceId, out var relatedDevices))
                {
                    relatedDevices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    followUpCounts[previousDeviceId] = relatedDevices;
                }

                relatedDevices[deviceId] = relatedDevices.GetValueOrDefault(deviceId) + 1;
            }

            recentActivities.Enqueue(new OperatorActivity
            {
                DeviceId = deviceId,
                Kind = kind,
                Description = description
            });

            if (recentActivities.Count > MaximumActivityEvents)
            {
                recentActivities.Dequeue();
            }

            previousDeviceId = deviceId;
        }
    }
}
