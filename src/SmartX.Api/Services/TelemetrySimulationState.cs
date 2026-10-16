using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class TelemetrySimulationState
{
    private readonly object syncRoot = new();
    private int tickNumber;
    private bool moistureIssueActive = true;
    private bool powerIssueActive = true;

    public SimulatedTelemetryTick NextTick()
    {
        lock (syncRoot)
        {
            tickNumber++;
            var variation = tickNumber % 3;

            return new SimulatedTelemetryTick(
                SoilMoisture: moistureIssueActive ? 24.5f + variation : 55.5f + variation,
                PowerWatts: powerIssueActive ? 1240 + variation * 15 : 650 + variation * 10,
                NutrientPh: 6.1f + variation / 10f,
                Humidity: 61f + variation,
                ValveOpen: !moistureIssueActive,
                PumpRunning: !moistureIssueActive,
                RelayConnected: !powerIssueActive);
        }
    }

    // In the simulation, a safe actuator response produces the next healthy telemetry reading.
    public void ApplyManualOverride(ManualOverrideCommand command)
    {
        lock (syncRoot)
        {
            if ((command.DeviceId == "FARM-VALVE-01" || command.DeviceId == "FARM-PUMP-01") &&
                (command.TargetState.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
                 command.TargetState.Equals("On", StringComparison.OrdinalIgnoreCase)))
            {
                moistureIssueActive = false;
            }

            if (command.DeviceId == "GRID-RELAY-01" &&
                (command.TargetState.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                 command.TargetState.Equals("Closed", StringComparison.OrdinalIgnoreCase)))
            {
                powerIssueActive = false;
            }
        }
    }
}

public sealed record SimulatedTelemetryTick(
    float SoilMoisture,
    int PowerWatts,
    float NutrientPh,
    float Humidity,
    bool ValveOpen,
    bool PumpRunning,
    bool RelayConnected);
