# Smart-X Part 2

Smart-X is a simulated Internet of Things management system for distributed environments such as hydroponic farms, smart-grid installations, and automated utility trackers. Part 2 extends the Part 1 telemetry dashboard with a Real-Time Command Stream, reversible manual actuator commands, timestamp-sorted history, and explainable predictive recommendations.

## Project details

- **Developer:** Thonipho Mavhungu
- **Student number:** ST10121100
- **Module assessment:** PROG7312 Smart-X Part 2

## Implemented features

- Smart-X startup gateway with the three required architectural pillars.
- ASP.NET Core Minimal API for sensor registration and telemetry ingestion.
- Sensor profiles containing device ID, MAC address, deployment location, and category.
- Generic `TelemetryPacket<T>` model for `float`, `int`, and `bool` telemetry values.
- Typed telemetry endpoints that preserve the original value type.
- Mock sensor and telemetry seeding using jagged arrays before data is transferred to `List<T>` collections.
- Sensor registration form connected to the API.
- Timestamped telemetry history with metric filtering and reading drill-down.
- Proactive alerts for low soil moisture, power spikes, and sensor disconnects.
- Alert investigation view with an actionable recommendation and linked sensor history.
- In-app help guide explaining how to use the dashboard.
- Real-Time Command Stream displaying recent incoming telemetry and priority critical alerts.
- Device filtering by operational category and active alert state.
- Manual actuator overrides with a stack-based **Undo latest** feature.
- Timestamp-sorted historical telemetry for efficient timeline review.
- Predictive Automated Insights based on critical readings and recorded operator search/command patterns.

## Part 2 data structures and algorithms

The Part 2 implementation deliberately uses the following structures to manage volatile telemetry and operator activity:

- `Queue<T>` keeps the most recent telemetry messages in first-in, first-out order for the live stream.
- `PriorityQueue<TElement, TPriority>` prioritises critical telemetry alerts ahead of routine messages.
- `Stack<T>` stores manual override commands so the most recent command can be undone first.
- `Dictionary<TKey, TValue>` stores live devices by unique device ID for fast lookups when telemetry or commands arrive.
- `SortedDictionary<TKey, TValue>` organises historical readings by UTC timestamp.
- A nested dictionary counts related device follow-ups, enabling pattern-based predictive insights.

## Technology stack

- .NET 10
- ASP.NET Core Minimal API
- Blazor WebAssembly
- C#
- Bootstrap and custom CSS

## Project structure

```text
src/
  SmartX.Api/       ASP.NET Core API and mock telemetry seeding
  SmartX.Client/    Blazor WebAssembly dashboard
  SmartX.Shared/    Shared models used by the API and dashboard
```

## Setup and running the project

### Prerequisites

Install the **.NET 10 SDK** before running the project. Confirm it is available by opening PowerShell and running:

```powershell
dotnet --version
```

The command should display a version starting with `10`.

### 1. Open the project folder

In PowerShell, move into the folder that contains `SmartX.slnx`:

```powershell
cd "C:\path\to\SmartXPart2"
```

### 2. Restore dependencies

Restore the NuGet packages for the complete solution. This is needed after cloning the repository or when dependencies change:

```powershell
dotnet restore SmartX.slnx
```

### 3. Compile the solution

Build both the API and the Blazor WebAssembly client:

```powershell
dotnet build SmartX.slnx
```

The build is successful when PowerShell reports `Build succeeded` with zero errors.

### 4. Start the backend API

Keep the first PowerShell window open and run:

```powershell
dotnet run --project src\SmartX.Api --launch-profile http
```

The API starts at `http://localhost:5145`. Leave this window running while using the dashboard.

### 5. Start the client dashboard

Open a **second** PowerShell window, move to the same project folder, and run:

```powershell
dotnet run --project src\SmartX.Client --launch-profile http
```

The browser should open automatically. If it does not, open [http://localhost:5025/](http://localhost:5025/) manually.

### 6. Stop the application

In each PowerShell window, press `Ctrl` + `C` to stop the API or client. The API uses temporary in-memory data, so newly registered sensors, uploaded attachments, command history, and learned activity patterns are cleared when the API stops.

## How to use the dashboard

### Telemetry workspace

1. Open **Telemetry** from the Smart-X gateway.
2. Review proactive alerts and select **Investigate** to open the affected sensor's history.
3. Register a sensor using its device ID, MAC address, deployment location, and category.
4. Choose a sensor, apply a metric filter, then select a reading to view its timestamp and source device.

### Real-Time Command Stream

1. Open **Command stream** from the gateway or sidebar.
2. Use the category drop-down or **Show devices with active alerts only** option to narrow the device list.
3. Review the incoming telemetry table. It refreshes automatically every five seconds; **Refresh stream** performs an immediate update.
4. Review **Critical alerts** first, then read **Automated Insights** to see the suggested action and the evidence supporting it.
5. Select **Review device** on an insight to inspect its timestamp-sorted history. This interaction helps the system learn future related-device patterns.
6. Under **Manual override**, choose an actuator device, select a command and target state, then select **Issue command**.
7. Select **Undo latest** to reverse the most recently issued manual override. Commands are undone in reverse order.

## AI assistance acknowledgement

This project was developed and reviewed by **Thonipho Mavhungu**. ChatGPT was used as an assistive tool during development, particularly for UI-layout guidance, code explanation, debugging support, and refining the project documentation. The final implementation was reviewed, tested, and organised by the student.
