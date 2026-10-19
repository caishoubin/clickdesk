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
            throw new InvalidDataException("不支持这个录制文件版本。");
        if (!double.IsFinite(recording.DurationMs) || recording.DurationMs < 0 ||
            recording.DurationMs > MaxDurationMs)
            throw new InvalidDataException("录制时长必须在 0 到 24 小时之间。");
        if (recording.Screen is null || recording.Screen.Width <= 0 || recording.Screen.Height <= 0 ||
            recording.Screen.Width > 100_000 || recording.Screen.Height > 100_000)
            throw new InvalidDataException("录制文件的屏幕信息无效。");
        if (recording.Events is null || recording.Events.Count > MaxEvents)
            throw new InvalidDataException("录制事件数量无效，最多支持 100,000 个事件。");

        var held = new HashSet<ClickButton>();
        double previous = 0;
        foreach (var item in recording.Events)
        {
            if (item is null || !double.IsFinite(item.OffsetMs) || item.OffsetMs < previous ||
                item.OffsetMs > recording.DurationMs || !Enum.IsDefined(item.Button) ||
                !Enum.IsDefined(item.Action) || !recording.Screen.Contains(item.X, item.Y))
                throw new InvalidDataException("录制事件的时间、坐标或鼠标按键无效。");
            if (item.Action == ClickAction.Down ? !held.Add(item.Button) : !held.Remove(item.Button))
                throw new InvalidDataException("录制文件包含未配对的鼠标按下 / 松开事件。");
            previous = item.OffsetMs;
        }
        if (held.Count != 0)
            throw new InvalidDataException("录制文件包含未松开的鼠标按键。");
    }

    public static Recording Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaxFileBytes)
            throw new InvalidDataException("文件过大，请选择小于 20 MB 的 ClickDesk 录制文件。");
        var recording = JsonSerializer.Deserialize<Recording>(stream, Options)
            ?? throw new InvalidDataException("录制文件内容为空。");
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
