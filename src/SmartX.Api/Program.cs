using SmartX.Api.Data;
using SmartX.Api.Services;
using SmartX.Shared.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins("http://localhost:5025", "https://localhost:7169")
            .AllowAnyHeader()
            .AllowAnyMethod()));
builder.Services.AddSingleton<RealTimeCommandStreamStore>();
builder.Services.AddSingleton<LiveDeviceRegistry>();
builder.Services.AddSingleton<SortedTelemetryHistoryStore>();
builder.Services.AddSingleton<ManualOverrideCommandStore>();
builder.Services.AddSingleton<OperatorActivityTracker>();
builder.Services.AddSingleton<PredictiveRecommendationEngine>();
builder.Services.AddSingleton<TelemetrySimulationState>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Keep local testing simple; deployed versions should always redirect to HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("BlazorClient");

var commandStream = app.Services.GetRequiredService<RealTimeCommandStreamStore>();
var liveDeviceRegistry = app.Services.GetRequiredService<LiveDeviceRegistry>();
var sortedTelemetryHistory = app.Services.GetRequiredService<SortedTelemetryHistoryStore>();
var manualOverrides = app.Services.GetRequiredService<ManualOverrideCommandStore>();
var activityTracker = app.Services.GetRequiredService<OperatorActivityTracker>();
var recommendationEngine = app.Services.GetRequiredService<PredictiveRecommendationEngine>();
var telemetrySimulation = app.Services.GetRequiredService<TelemetrySimulationState>();

// This list is temporary storage for Part 1. It is cleared when the API stops.
var registeredSensors = new List<SensorProfile>();

var sensors = app.MapGroup("/api/sensors")
    .WithTags("Sensors");

// GET /api/sensors returns every sensor registered during the current run.
sensors.MapGet("/", () => Results.Ok(registeredSensors))
    .WithName("GetSensors");

// POST /api/sensors adds one sensor registration record.
sensors.MapPost("/", (SensorProfile profile) =>
{
    if (string.IsNullOrWhiteSpace(profile.DeviceId) ||
        string.IsNullOrWhiteSpace(profile.MacAddress) ||
        string.IsNullOrWhiteSpace(profile.DeploymentLocation))
    {
        return Results.BadRequest(new
        {
            message = "Device ID, MAC address and deployment location are required."
        });
    }

    var macAddressAlreadyExists = registeredSensors.Any(sensor =>
        sensor.MacAddress.Equals(profile.MacAddress, StringComparison.OrdinalIgnoreCase));

    if (macAddressAlreadyExists || !liveDeviceRegistry.TryRegister(profile))
    {
        return Results.Conflict(new
        {
            message = "A sensor with this device ID or MAC address is already registered."
        });
    }

    registeredSensors.Add(profile);

    return Results.Created($"/api/sensors/{profile.DeviceId}", profile);
})
.WithName("RegisterSensor");

// Uses the live device dictionary for immediate lookup by unique node identifier.
sensors.MapGet("/lookup/{deviceId}", (string deviceId) =>
{
    if (!liveDeviceRegistry.TryGetDevice(deviceId, out var sensor))
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    activityTracker.RecordSearch(deviceId);
    return Results.Ok(sensor);
})
.WithName("GetLiveDeviceById");

// POST /api/sensors/{deviceId}/attachments stores a multipart attachment for one sensor.
sensors.MapPost("/{deviceId}/attachments", async (string deviceId, HttpRequest request) =>
{
    var sensorExists = liveDeviceRegistry.ContainsDevice(deviceId);

    if (!sensorExists)
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Upload the attachment as multipart form data." });
    }

    var form = await request.ReadFormAsync();
    var uploadedFile = form.Files.GetFile("file");

    if (uploadedFile is null || uploadedFile.Length == 0)
    {
        return Results.BadRequest(new { message = "Choose a file to attach." });
    }

    const long maximumFileSize = 10 * 1024 * 1024;
    if (uploadedFile.Length > maximumFileSize)
    {
        return Results.BadRequest(new { message = "Attachments must be 10 MB or smaller." });
    }

    var safeDeviceFolder = CreateSafeFolderName(deviceId);
    var uploadDirectory = Path.Combine(app.Environment.ContentRootPath, "Uploads", safeDeviceFolder);
    Directory.CreateDirectory(uploadDirectory);

    var safeFileName = Path.GetFileName(uploadedFile.FileName);
    var storedFileName = $"{Guid.NewGuid():N}_{safeFileName}";
    var storedFilePath = Path.Combine(uploadDirectory, storedFileName);

    await using (var destination = File.Create(storedFilePath))
    {
        await uploadedFile.CopyToAsync(destination);
    }

    var attachment = new SensorAttachment
    {
        FileName = safeFileName,
        ContentType = uploadedFile.ContentType,
        SizeBytes = uploadedFile.Length,
        UploadedAtUtc = DateTime.UtcNow
    };

    return Results.Created($"/api/sensors/{Uri.EscapeDataString(deviceId)}/attachments", attachment);
})
.WithName("AttachSensorFile");

// GET /api/sensors/{deviceId}/attachments lists files currently linked to a sensor.
sensors.MapGet("/{deviceId}/attachments", (string deviceId) =>
{
    var sensorExists = liveDeviceRegistry.ContainsDevice(deviceId);

    if (!sensorExists)
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    var uploadDirectory = Path.Combine(app.Environment.ContentRootPath, "Uploads", CreateSafeFolderName(deviceId));
    if (!Directory.Exists(uploadDirectory))
    {
        return Results.Ok(Array.Empty<SensorAttachment>());
    }

    var attachments = new DirectoryInfo(uploadDirectory).GetFiles()
        .Select(file => new SensorAttachment
        {
            FileName = file.Name[(file.Name.IndexOf('_') + 1)..],
            SizeBytes = file.Length,
            UploadedAtUtc = file.LastWriteTimeUtc
        })
        .OrderByDescending(file => file.UploadedAtUtc);

    return Results.Ok(attachments);
})
.WithName("GetSensorAttachments");

// Each list preserves its packet's original telemetry value type.
var floatTelemetry = new List<TelemetryPacket<float>>();
var integerTelemetry = new List<TelemetryPacket<int>>();
var booleanTelemetry = new List<TelemetryPacket<bool>>();

MockTelemetrySeeder.Seed(
    registeredSensors,
    floatTelemetry,
    integerTelemetry,
    booleanTelemetry);

liveDeviceRegistry.LoadExistingDevices(registeredSensors);

foreach (var packet in floatTelemetry)
{
    sortedTelemetryHistory.Add(packet);
    commandStream.EnqueueTelemetry(packet);
}

foreach (var packet in integerTelemetry)
{
    sortedTelemetryHistory.Add(packet);
    commandStream.EnqueueTelemetry(packet);
}

foreach (var packet in booleanTelemetry)
{
    sortedTelemetryHistory.Add(packet);
    commandStream.EnqueueTelemetry(packet);
}

commandStream.UpdateCriticalAlerts(TelemetryAlertEvaluator.Evaluate(
    registeredSensors,
    floatTelemetry,
    integerTelemetry,
    booleanTelemetry,
    DateTime.UtcNow));

var telemetry = app.MapGroup("/api/telemetry")
    .WithTags("Telemetry");

app.MapPost("/api/simulation/tick", () =>
{
    RunSimulationTick();
    return Results.Ok(new { message = "A new simulated telemetry batch was added to the command stream." });
})
.WithTags("Simulation")
.WithName("RunTelemetrySimulationTick");

// Validates a hierarchy such as Site > Greenhouse > Growing Bay using recursion.
app.MapPost("/api/deployments/validate", (DeploymentConfiguration configuration) =>
{
    var validationResult = DeploymentConfigurationValidator.Validate(configuration, registeredSensors);
    return Results.Ok(validationResult);
})
.WithTags("Deployments")
.WithName("ValidateDeploymentConfiguration");

app.MapGet("/api/alerts", () => Results.Ok(TelemetryAlertEvaluator.Evaluate(
    registeredSensors,
    floatTelemetry,
    integerTelemetry,
    booleanTelemetry,
    DateTime.UtcNow)))
    .WithTags("Alerts")
    .WithName("GetTelemetryAlerts");

var commandStreamEndpoints = app.MapGroup("/api/command-stream")
    .WithTags("Real-Time Command Stream");

var recommendationEndpoints = app.MapGroup("/api/recommendations")
    .WithTags("Predictive Recommendations");

recommendationEndpoints.MapPost("/searches/{deviceId}", (string deviceId) =>
{
    if (!liveDeviceRegistry.ContainsDevice(deviceId))
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    activityTracker.RecordSearch(deviceId);
    return Results.NoContent();
})
.WithName("RecordDeviceSearch");

recommendationEndpoints.MapGet("/activity", () => Results.Ok(activityTracker.GetRecentActivities()))
    .WithName("GetRecentOperatorActivity");

recommendationEndpoints.MapGet("/insights", () =>
{
    var activeAlerts = TelemetryAlertEvaluator.Evaluate(
        registeredSensors,
        floatTelemetry,
        integerTelemetry,
        booleanTelemetry,
        DateTime.UtcNow);

    return Results.Ok(recommendationEngine.Generate(activeAlerts, registeredSensors));
})
.WithName("GetPredictiveInsights");

commandStreamEndpoints.MapGet("/telemetry", () => Results.Ok(commandStream.GetLiveTelemetry()))
    .WithName("GetLiveCommandStreamTelemetry");

commandStreamEndpoints.MapGet("/critical-alerts", () => Results.Ok(commandStream.GetCriticalAlerts()))
    .WithName("GetPriorityCriticalAlerts");

commandStreamEndpoints.MapGet("/history/{deviceId}", (string deviceId) =>
{
    if (!liveDeviceRegistry.ContainsDevice(deviceId))
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    return Results.Ok(sortedTelemetryHistory.GetHistoryForDevice(deviceId));
})
.WithName("GetSortedCommandStreamHistory");

commandStreamEndpoints.MapGet("/commands", () => Results.Ok(manualOverrides.GetUndoHistory()))
    .WithName("GetManualOverrideHistory");

commandStreamEndpoints.MapPost("/commands", (ManualOverrideRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.DeviceId) ||
        string.IsNullOrWhiteSpace(request.Command) ||
        string.IsNullOrWhiteSpace(request.TargetState))
    {
        return Results.BadRequest(new { message = "Device ID, command and target state are required." });
    }

    if (!liveDeviceRegistry.TryGetDevice(request.DeviceId, out var sensor))
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    if (sensor!.Category != SensorCategory.Actuator)
    {
        return Results.BadRequest(new { message = "Manual overrides are only available for actuator devices." });
    }

    var issuedCommand = manualOverrides.Issue(request);
    activityTracker.RecordManualOverride(issuedCommand);
    telemetrySimulation.ApplyManualOverride(issuedCommand);
    RunSimulationTick();
    return Results.Created($"/api/command-stream/commands/{issuedCommand.CommandId}", issuedCommand);
})
.WithName("IssueManualOverride");

commandStreamEndpoints.MapPost("/commands/undo", () =>
{
    return manualOverrides.TryUndoLatest(out var undoResult)
        ? Results.Ok(undoResult)
        : Results.NotFound(new { message = "There is no manual override command to undo." });
})
.WithName("UndoLatestManualOverride");

// The three POST routes deliberately accept different generic packet types.
telemetry.MapPost("/float", (TelemetryPacket<float> packet) =>
    StoreTelemetryPacket(packet, floatTelemetry))
    .WithName("AddFloatTelemetry");

telemetry.MapPost("/integer", (TelemetryPacket<int> packet) =>
    StoreTelemetryPacket(packet, integerTelemetry))
    .WithName("AddIntegerTelemetry");

telemetry.MapPost("/boolean", (TelemetryPacket<bool> packet) =>
    StoreTelemetryPacket(packet, booleanTelemetry))
    .WithName("AddBooleanTelemetry");

// Uses overloaded operators to compare the two latest smart-meter readings.
telemetry.MapGet("/power-summary/{deviceId}", (string deviceId) =>
{
    var recentPackets = integerTelemetry
        .Where(packet => packet.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase) &&
                         packet.MetricName == "Power Usage Watts")
        .OrderByDescending(packet => packet.RecordedAtUtc)
        .Take(2)
        .ToList();

    if (recentPackets.Count < 2)
    {
        return Results.NotFound(new { message = "At least two power readings are required for a calculation." });
    }

    var latestReading = new PowerReading(recentPackets[0].Value);
    var previousReading = new PowerReading(recentPackets[1].Value);
    var combinedLoad = latestReading + previousReading;
    var powerDelta = latestReading - previousReading;

    return Results.Ok(new
    {
        deviceId,
        latestWatts = latestReading.Watts,
        previousWatts = previousReading.Watts,
        combinedWatts = combinedLoad.Watts,
        deltaWatts = powerDelta.Watts
    });
})
.WithName("GetPowerSummary");

// GET /api/telemetry returns all packets, grouped by their value type.
telemetry.MapGet("/", () => Results.Ok(new TelemetryHistory
{
    FloatPackets = floatTelemetry,
    IntegerPackets = integerTelemetry,
    BooleanPackets = booleanTelemetry
}))
.WithName("GetAllTelemetry");

// GET /api/telemetry/{deviceId} returns the history for one sensor.
telemetry.MapGet("/{deviceId}", (string deviceId) =>
{
    var sensorExists = liveDeviceRegistry.ContainsDevice(deviceId);

    if (!sensorExists)
    {
        return Results.NotFound(new { message = "The requested sensor is not registered." });
    }

    return Results.Ok(new TelemetryHistory
    {
        FloatPackets = floatTelemetry.Where(packet =>
            packet.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)).ToList(),
        IntegerPackets = integerTelemetry.Where(packet =>
            packet.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)).ToList(),
        BooleanPackets = booleanTelemetry.Where(packet =>
            packet.DeviceId.Equals(deviceId, StringComparison.OrdinalIgnoreCase)).ToList()
    });
})
.WithName("GetSensorTelemetry");

IResult StoreTelemetryPacket<T>(TelemetryPacket<T> packet, List<TelemetryPacket<T>> packetList)
{
    if (string.IsNullOrWhiteSpace(packet.DeviceId) || string.IsNullOrWhiteSpace(packet.MetricName))
    {
        return Results.BadRequest(new { message = "Device ID and metric name are required." });
    }

    var sensorExists = liveDeviceRegistry.ContainsDevice(packet.DeviceId);

    if (!sensorExists)
    {
        return Results.NotFound(new { message = "Register the sensor before sending telemetry." });
    }

    commandStream.EnqueueTelemetry(packet);
    sortedTelemetryHistory.Add(packet);
    packetList.Add(packet);

    commandStream.UpdateCriticalAlerts(TelemetryAlertEvaluator.Evaluate(
        registeredSensors,
        floatTelemetry,
        integerTelemetry,
        booleanTelemetry,
        DateTime.UtcNow));

    return Results.Created($"/api/telemetry/{packet.DeviceId}", packet);
}

void RunSimulationTick()
{
    var simulationTick = telemetrySimulation.NextTick();
    var timestamp = DateTime.UtcNow;

    StoreTelemetryPacket(new TelemetryPacket<float>
    {
        DeviceId = "FARM-MOIST-01",
        MetricName = "Soil Moisture",
        Value = simulationTick.SoilMoisture,
        RecordedAtUtc = timestamp
    }, floatTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<int>
    {
        DeviceId = "GRID-METER-01",
        MetricName = "Power Usage Watts",
        Value = simulationTick.PowerWatts,
        RecordedAtUtc = timestamp
    }, integerTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<float>
    {
        DeviceId = "FARM-PH-01",
        MetricName = "Nutrient pH",
        Value = simulationTick.NutrientPh,
        RecordedAtUtc = timestamp
    }, floatTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<float>
    {
        DeviceId = "GREENHOUSE-HUMID-01",
        MetricName = "Humidity",
        Value = simulationTick.Humidity,
        RecordedAtUtc = timestamp
    }, floatTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<bool>
    {
        DeviceId = "FARM-VALVE-01",
        MetricName = "Valve Open",
        Value = simulationTick.ValveOpen,
        RecordedAtUtc = timestamp
    }, booleanTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<bool>
    {
        DeviceId = "FARM-PUMP-01",
        MetricName = "Pump Running",
        Value = simulationTick.PumpRunning,
        RecordedAtUtc = timestamp
    }, booleanTelemetry);
    StoreTelemetryPacket(new TelemetryPacket<bool>
    {
        DeviceId = "GRID-RELAY-01",
        MetricName = "Relay Connected",
        Value = simulationTick.RelayConnected,
        RecordedAtUtc = timestamp
    }, booleanTelemetry);
}

string CreateSafeFolderName(string deviceId)
{
    var invalidCharacters = Path.GetInvalidFileNameChars();
    return string.Concat(deviceId.Select(character =>
        invalidCharacters.Contains(character) ? '_' : character));
}

app.Run();
