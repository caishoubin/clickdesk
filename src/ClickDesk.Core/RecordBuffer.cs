namespace ClickDesk.Core;

/// <summary>All methods run on the hook's owning UI thread.</summary>
public sealed class RecordBuffer(DesktopBounds screen)
{
    private readonly List<PointerEvent> _events = [];
    private readonly Dictionary<ClickButton, PointerEvent> _held = [];
    public IReadOnlyList<PointerEvent> Events => _events;
    public int ClickCount { get; private set; }
    public bool IsFull => _events.Count >= RecordingFile.MaxEvents - 6;

    public bool Append(double offsetMs, int x, int y, ClickButton button, ClickAction action)
    {
        if (IsFull || !double.IsFinite(offsetMs) || offsetMs < 0 ||
            offsetMs > RecordingFile.MaxDurationMs || !screen.Contains(x, y) ||
            !Enum.IsDefined(button) || !Enum.IsDefined(action)) return false;
        if (_events.Count > 0 && offsetMs < _events[^1].OffsetMs) return false;
        if (action == ClickAction.Down ? _held.ContainsKey(button) : !_held.ContainsKey(button)) return false;
        var item = new PointerEvent(offsetMs, x, y, button, action);
        if (action == ClickAction.Down)
        {
            _held.Add(button, item);
            ClickCount++;
        }
        else _held.Remove(button);
        _events.Add(item);
        return true;
    }

    public Recording Finish(double durationMs)
    {
        durationMs = Math.Clamp(durationMs, 0, RecordingFile.MaxDurationMs);
        if (_events.Count > 0) durationMs = Math.Max(durationMs, _events[^1].OffsetMs);
        // Stopping during a long press must never create a replay that leaves a button held.
        foreach (var item in _held.Values)
            _events.Add(item with { OffsetMs = durationMs, Action = ClickAction.Up });
        _held.Clear();
        return new Recording { Screen = screen, DurationMs = durationMs, Events = [.. _events] };
    }
}
