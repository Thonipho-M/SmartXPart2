using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class LiveDeviceRegistry
{
    // A dictionary provides O(1) lookup by the Smart-X unique device identifier.
    private readonly Dictionary<string, SensorProfile> devicesById =
        new(StringComparer.OrdinalIgnoreCase);

    public void LoadExistingDevices(IEnumerable<SensorProfile> sensors)
    {
        foreach (var sensor in sensors)
        {
            devicesById.TryAdd(sensor.DeviceId, sensor);
        }
    }

    public bool TryRegister(SensorProfile sensor)
    {
        return devicesById.TryAdd(sensor.DeviceId, sensor);
    }

    public bool ContainsDevice(string deviceId)
    {
        return devicesById.ContainsKey(deviceId);
    }

    public bool TryGetDevice(string deviceId, out SensorProfile? sensor)
    {
        return devicesById.TryGetValue(deviceId, out sensor);
    }
}
