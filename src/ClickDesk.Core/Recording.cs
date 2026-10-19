using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickDesk.Core;

public enum ClickButton { Left, Right, Middle }
public enum ClickAction { Down, Up }

public sealed record PointerEvent(double OffsetMs, int X, int Y, ClickButton Button, ClickAction Action);

public sealed record DesktopBounds(int Left, int Top, int Width, int Height)
{
    public bool Contains(int x, int y) => x >= Left && y >= Top &&
        (long)x < (long)Left + Width && (long)y < (long)Top + Height;
}

public sealed class Recording
{
    public int FormatVersion { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DesktopBounds Screen { get; init; } = new(0, 0, 1920, 1080);
    public double DurationMs { get; init; }
    public List<PointerEvent> Events { get; init; } = [];

    [JsonIgnore]
    public int ClickCount => Events.Count(e => e.Action == ClickAction.Down);
}

public static class RecordingFile
{
    public const int MaxEvents = 100_000;
    public const double MaxDurationMs = 24 * 60 * 60 * 1000;
    public const int MaxFileBytes = 20 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Validate(Recording recording)
    {
        if (recording.FormatVersion != 1)
            throw new InvalidDataException("This recording format version is not supported.");
        if (!double.IsFinite(recording.DurationMs) || recording.DurationMs < 0 ||
            recording.DurationMs > MaxDurationMs)
            throw new InvalidDataException("Recording duration must be between 0 and 24 hours.");
        if (recording.Screen is null || recording.Screen.Width <= 0 || recording.Screen.Height <= 0 ||
            recording.Screen.Width > 100_000 || recording.Screen.Height > 100_000)
            throw new InvalidDataException("The recording contains invalid desktop information.");
        if (recording.Events is null || recording.Events.Count > MaxEvents)
            throw new InvalidDataException("Invalid event count. A recording supports up to 100,000 events.");

        var held = new HashSet<ClickButton>();
        double previous = 0;
        foreach (var item in recording.Events)
        {
            if (item is null || !double.IsFinite(item.OffsetMs) || item.OffsetMs < previous ||
                item.OffsetMs > recording.DurationMs || !Enum.IsDefined(item.Button) ||
                !Enum.IsDefined(item.Action) || !recording.Screen.Contains(item.X, item.Y))
                throw new InvalidDataException("A recorded event has an invalid time, position, or mouse button.");
            if (item.Action == ClickAction.Down ? !held.Add(item.Button) : !held.Remove(item.Button))
                throw new InvalidDataException("The recording contains unmatched mouse down / up events.");
            previous = item.OffsetMs;
        }
        if (held.Count != 0)
            throw new InvalidDataException("The recording contains a mouse button that was never released.");
    }

    public static Recording Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaxFileBytes)
            throw new InvalidDataException("The file is too large. Choose a ClickDesk recording smaller than 20 MB.");
        var recording = JsonSerializer.Deserialize<Recording>(stream, Options)
            ?? throw new InvalidDataException("The recording file is empty.");
        Validate(recording);
        return recording;
    }

    public static void Save(string path, Recording recording)
    {
        Validate(recording);
        var fullPath = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, $".clickdesk-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, recording, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
