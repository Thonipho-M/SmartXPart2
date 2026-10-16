using SmartX.Shared.Models;

namespace SmartX.Api.Data;

public static class MockTelemetrySeeder
{
    public static void Seed(
        List<SensorProfile> sensors,
        List<TelemetryPacket<float>> floatTelemetry,
        List<TelemetryPacket<int>> integerTelemetry,
        List<TelemetryPacket<bool>> booleanTelemetry)
    {
        sensors.AddRange(
        [
            new SensorProfile
            {
                DeviceId = "FARM-MOIST-01",
                MacAddress = "AA:BB:CC:00:00:01",
                DeploymentLocation = "Hydroponic Farm A - Zone 1",
                Category = SensorCategory.Environmental
            },
            new SensorProfile
            {
                DeviceId = "GRID-METER-01",
                MacAddress = "AA:BB:CC:00:00:02",
                DeploymentLocation = "Smart Grid - Distribution Node 2",
                Category = SensorCategory.PowerConsumption
            },
            new SensorProfile
            {
                DeviceId = "FARM-VALVE-01",
                MacAddress = "AA:BB:CC:00:00:03",
                DeploymentLocation = "Hydroponic Farm A - Irrigation Line 1",
                Category = SensorCategory.Actuator
            },
            new SensorProfile
            {
                DeviceId = "FARM-TEMP-02",
                MacAddress = "AA:BB:CC:00:00:04",
                DeploymentLocation = "Hydroponic Farm A - Zone 2",
                Category = SensorCategory.Environmental
            },
            new SensorProfile
            {
                DeviceId = "FARM-PUMP-01",
                MacAddress = "AA:BB:CC:00:00:05",
                DeploymentLocation = "Hydroponic Farm A - Pump Room",
                Category = SensorCategory.Actuator
            },
            new SensorProfile
            {
                DeviceId = "GRID-RELAY-01",
                MacAddress = "AA:BB:CC:00:00:06",
                DeploymentLocation = "Smart Grid - Distribution Node 2",
                Category = SensorCategory.Actuator
            },
            new SensorProfile
            {
                DeviceId = "FARM-PH-01",
                MacAddress = "AA:BB:CC:00:00:07",
                DeploymentLocation = "Hydroponic Farm A - Nutrient Tank",
                Category = SensorCategory.Environmental
            },
            new SensorProfile
            {
                DeviceId = "GREENHOUSE-HUMID-01",
                MacAddress = "AA:BB:CC:00:00:08",
                DeploymentLocation = "Greenhouse B - Climate Zone",
                Category = SensorCategory.Environmental
            }
        ]);

        // Jagged arrays represent sequential batches before they are moved into List<T> storage.
        var moistureBatches = new[]
        {
            new[] { 59.2f, 57.8f, 55.6f, 31.4f, 24.8f },
            new[] { 61.5f, 60.9f, 60.1f, 59.8f, 59.2f }
        };
        AddPackets(floatTelemetry, "FARM-MOIST-01", "Soil Moisture", moistureBatches[0]);
        AddPackets(floatTelemetry, "FARM-MOIST-01", "Nutrient Temperature", moistureBatches[1]);
        AddPackets(
            floatTelemetry,
            "FARM-TEMP-02",
            "Ambient Temperature",
            new[] { 23.4f, 23.1f, 22.9f });
        AddPackets(floatTelemetry, "FARM-PH-01", "Nutrient pH", new[] { 6.2f, 6.3f, 6.1f, 6.2f });
        AddPackets(floatTelemetry, "GREENHOUSE-HUMID-01", "Humidity", new[] { 61.4f, 60.8f, 62.1f, 61.7f });

        var powerBatches = new[]
        {
            new[] { 610, 625, 640, 1180, 1250 },
            new[] { 92, 95, 97, 98, 94 }
        };
        AddPackets(integerTelemetry, "GRID-METER-01", "Power Usage Watts", powerBatches[0]);
        AddPackets(integerTelemetry, "GRID-METER-01", "Voltage", powerBatches[1]);

        var valveBatches = new[]
        {
            new[] { false, false, true, true, false },
            new[] { true, true, true, false, false }
        };
        AddPackets(booleanTelemetry, "FARM-VALVE-01", "Valve Open", valveBatches[0]);
        AddPackets(booleanTelemetry, "FARM-VALVE-01", "Manual Override", valveBatches[1]);
        AddPackets(booleanTelemetry, "FARM-PUMP-01", "Pump Running", new[] { false, true, true, true, true });
        AddPackets(booleanTelemetry, "GRID-RELAY-01", "Relay Connected", new[] { true, true, true, true, true });
    }

    private static void AddPackets<T>(
        List<TelemetryPacket<T>> destination,
        string deviceId,
        string metricName,
        T[] values,
        DateTime? firstReadingTime = null)
    {
        var packetStartTime = firstReadingTime ?? DateTime.UtcNow.AddMinutes(-5 * values.Length);

        for (var index = 0; index < values.Length; index++)
        {
            destination.Add(new TelemetryPacket<T>
            {
                DeviceId = deviceId,
                MetricName = metricName,
                Value = values[index],
                RecordedAtUtc = packetStartTime.AddMinutes(index * 5)
            });
        }
    }
}
