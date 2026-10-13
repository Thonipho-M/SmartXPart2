using SmartX.Shared.Models;

namespace SmartX.Api.Services;

public sealed class ManualOverrideCommandStore
{
    private readonly object syncRoot = new();
    private readonly Stack<ManualOverrideCommand> undoHistory = new();
    private readonly Dictionary<string, string> currentDeviceStates =
        new(StringComparer.OrdinalIgnoreCase);

    // Stack<T> keeps the newest command on top, ready for an immediate undo.
    public ManualOverrideCommand Issue(ManualOverrideRequest request)
    {
        lock (syncRoot)
        {
            var previousState = currentDeviceStates.GetValueOrDefault(request.DeviceId, "Unknown");
            var command = new ManualOverrideCommand
            {
                DeviceId = request.DeviceId,
                Command = request.Command,
                PreviousState = previousState,
                TargetState = request.TargetState
            };

            currentDeviceStates[request.DeviceId] = request.TargetState;
            undoHistory.Push(command);
            return command;
        }
    }

    public IReadOnlyList<ManualOverrideCommand> GetUndoHistory()
    {
        lock (syncRoot)
        {
            return undoHistory.ToList();
        }
    }

    public bool TryUndoLatest(out ManualOverrideUndoResult? result)
    {
        lock (syncRoot)
        {
            if (!undoHistory.TryPop(out var command))
            {
                result = null;
                return false;
            }

            currentDeviceStates[command.DeviceId] = command.PreviousState;
            result = new ManualOverrideUndoResult
            {
                RevertedCommand = command,
                RestoredState = command.PreviousState
            };
            return true;
        }
    }
}
