using System.Diagnostics;

namespace ClickDesk.Core;

public sealed record PlaybackOptions(int RepeatCount, double Speed, double LoopGapMs)
{
    public void Validate()
    {
        if (RepeatCount < 0 || RepeatCount > 999_999 || !double.IsFinite(Speed) || Speed < 0.1 ||
            Speed > 10 || !double.IsFinite(LoopGapMs) || LoopGapMs < 0 || LoopGapMs > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(PlaybackOptions), "回放设置超出允许范围。");
    }
}

public interface IPlaybackClock
{
    double ElapsedMs { get; }
    Task WaitUntilAsync(double targetMs, CancellationToken cancellationToken);
}

public sealed class PlaybackClock : IPlaybackClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    public double ElapsedMs => _watch.Elapsed.TotalMilliseconds;

    public async Task WaitUntilAsync(double targetMs, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = targetMs - ElapsedMs;
            if (remaining <= 0) return;
            // Keep the final wait short; schedule against absolute time to avoid per-click drift.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(remaining > 5 ? remaining - 2 : 1, 1000))),
                cancellationToken).ConfigureAwait(false);
        }
    }
}

public interface IMouseOutput
{
    void Send(PointerEvent item);
    void ReleaseAll();
}

public sealed record PlaybackProgress(long Round, int EventIndex, int EventCount);

public static class Playback
{
    public static async Task RunAsync(Recording recording, PlaybackOptions options, IMouseOutput output,
        IPlaybackClock clock, Action<PlaybackProgress>? progress, CancellationToken cancellationToken)
    {
        RecordingFile.Validate(recording);
        options.Validate();
        if (recording.Events.Count == 0) throw new InvalidOperationException("请先录制至少一次点击。");
        var roundStart = clock.ElapsedMs;
        try
        {
            for (long round = 1; options.RepeatCount == 0 || round <= options.RepeatCount; round++)
            {
                for (var index = 0; index < recording.Events.Count; index++)
                {
                    var item = recording.Events[index];
                    await clock.WaitUntilAsync(roundStart + item.OffsetMs / options.Speed, cancellationToken)
                        .ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    output.Send(item);
                    progress?.Invoke(new PlaybackProgress(round, index + 1, recording.Events.Count));
                }
                // Preserve the recorded quiet period after the final click in every round.
                await clock.WaitUntilAsync(roundStart + recording.DurationMs / options.Speed, cancellationToken)
                    .ConfigureAwait(false);
                if (options.RepeatCount != 0 && round == options.RepeatCount) break;
                await clock.WaitUntilAsync(Math.Max(clock.ElapsedMs, roundStart + recording.DurationMs / options.Speed)
                    + options.LoopGapMs, cancellationToken).ConfigureAwait(false);
                // Do not burst clicks to catch up if Windows temporarily paused this process.
                roundStart = clock.ElapsedMs;
                if (recording.DurationMs == 0 && options.LoopGapMs == 0)
                    await clock.WaitUntilAsync(roundStart + 1, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            output.ReleaseAll();
        }
    }
}
