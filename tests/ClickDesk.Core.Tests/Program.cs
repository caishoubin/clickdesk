using ClickDesk.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("recording preserves sequence, double-click timing and hold duration", () =>
    {
        var buffer = new RecordBuffer(new DesktopBounds(-1920, 0, 3840, 1080));
        Require(buffer.Append(100, -100, 50, ClickButton.Left, ClickAction.Down));
        Require(buffer.Append(135, -100, 50, ClickButton.Left, ClickAction.Up));
        Require(buffer.Append(210, -100, 50, ClickButton.Left, ClickAction.Down));
        Require(buffer.Append(245, -100, 50, ClickButton.Left, ClickAction.Up));
        var result = buffer.Finish(400);
        RecordingFile.Validate(result);
        Require(result.ClickCount == 2 && result.DurationMs == 400);
        Require(result.Events.Select(e => e.OffsetMs).SequenceEqual(new double[] { 100, 135, 210, 245 }));
        return Task.CompletedTask;
    }),
    ("recording ignores orphan releases and duplicate downs; stop balances held buttons", () =>
    {
        var buffer = new RecordBuffer(new DesktopBounds(0, 0, 800, 600));
        Require(!buffer.Append(1, 20, 30, ClickButton.Right, ClickAction.Up));
        Require(buffer.Append(10, 20, 30, ClickButton.Right, ClickAction.Down));
        Require(!buffer.Append(11, 20, 30, ClickButton.Right, ClickAction.Down));
        Require(buffer.Append(12, 50, 60, ClickButton.Middle, ClickAction.Down));
        var result = buffer.Finish(100);
        RecordingFile.Validate(result);
        Require(result.Events.Count == 4 && result.Events.Skip(2).All(e => e.Action == ClickAction.Up && e.OffsetMs == 100));
        return Task.CompletedTask;
    }),
    ("malformed recordings are rejected before any playback", () =>
    {
        var valid = Sample();
        Reject(() => RecordingFile.Validate(new Recording { DurationMs = double.NaN }));
        Reject(() => RecordingFile.Validate(new Recording { DurationMs = -1 }));
        Reject(() => RecordingFile.Validate(new Recording { FormatVersion = 20 }));
        Reject(() => RecordingFile.Validate(new Recording { Events = null! }));
        Reject(() => RecordingFile.Validate(new Recording { Screen = null! }));
        Reject(() => RecordingFile.Validate(new Recording { Events = [null!] }));
        Reject(() => RecordingFile.Validate(new Recording { DurationMs = 300, Events = [valid.Events[0]] }));
        Reject(() => RecordingFile.Validate(new Recording { DurationMs = 300, Events = [valid.Events[1], valid.Events[0]] }));
        Reject(() => RecordingFile.Validate(new Recording { DurationMs = 300,
            Events = [valid.Events[0] with { X = -1 }, valid.Events[1]] }));
        Reject(() => new PlaybackOptions(1, 0, 0).Validate());
        Reject(() => new PlaybackOptions(-1, 1, 0).Validate());
        Reject(() => new PlaybackOptions(1, 1, double.PositiveInfinity).Validate());
        return Task.CompletedTask;
    }),
    ("save/load round trip preserves precision; malformed files do not load", () =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"clickdesk-test-{Guid.NewGuid():N}.json");
        try
        {
            var sample = Sample();
            RecordingFile.Save(path, sample);
            RecordingFile.Save(path, sample); // Atomic overwrite is also supported.
            var loaded = RecordingFile.Load(path);
            Require(loaded.Screen == sample.Screen && loaded.Events.SequenceEqual(sample.Events));
            Require(loaded.DurationMs == sample.DurationMs);
            File.WriteAllText(path, "{\"events\":null}");
            Reject(() => RecordingFile.Load(path));
        }
        finally { File.Delete(path); }
        return Task.CompletedTask;
    }),
    ("absolute schedule scales time and preserves tail plus inter-round pause", async () =>
    {
        var clock = new FakeClock();
        var mouse = new FakeMouse(clock);
        await Playback.RunAsync(Sample(), new PlaybackOptions(2, 2, 50), mouse, clock, null, default);
        Require(mouse.Items.Select(x => x.Time).SequenceEqual(new double[] { 50, 60, 125, 130, 250, 260, 325, 330 }));
        Require(clock.ElapsedMs == 350 && mouse.ReleaseCalls == 1);
    }),
    ("a slow input call does not accumulate drift into subsequent scheduled events", async () =>
    {
        var clock = new FakeClock();
        var mouse = new FakeMouse(clock) { AfterSend = () => clock.Advance(5) };
        await Playback.RunAsync(Sample(), new PlaybackOptions(1, 1, 0), mouse, clock, null, default);
        Require(mouse.Items.Select(x => x.Time).SequenceEqual(new double[] { 100, 120, 250, 260 }));
        Require(clock.ElapsedMs == 300);
    }),
    ("cancel during long press releases held inputs and sends no later events", async () =>
    {
        using var source = new CancellationTokenSource();
        var clock = new FakeClock();
        var mouse = new FakeMouse(clock) { AfterSend = source.Cancel };
        try { await Playback.RunAsync(Sample(), new PlaybackOptions(0, 1, 0), mouse, clock, null, source.Token); }
        catch (OperationCanceledException) { }
        Require(mouse.Items.Count == 1 && mouse.ReleaseCalls == 1 && mouse.Held.Count == 0);
    }),
    ("cancellation also works in the trailing wait and between loops", async () =>
    {
        foreach (var threshold in new double[] { 300, 350 })
        {
            using var source = new CancellationTokenSource();
            var clock = new FakeClock { OnWait = target => { if (target >= threshold) source.Cancel(); } };
            var mouse = new FakeMouse(clock);
            try { await Playback.RunAsync(Sample(), new PlaybackOptions(0, 1, 50), mouse, clock, null, source.Token); }
            catch (OperationCanceledException) { }
            Require(mouse.Items.Count == 4 && mouse.ReleaseCalls == 1);
        }
    }),
    ("output failures still release held inputs", async () =>
    {
        var clock = new FakeClock();
        var mouse = new FakeMouse(clock) { AfterSend = () => throw new IOException("simulated failure") };
        try { await Playback.RunAsync(Sample(), new PlaybackOptions(1, 1, 0), mouse, clock, null, default); }
        catch (IOException) { }
        Require(mouse.Items.Count == 1 && mouse.ReleaseCalls == 1 && mouse.Held.Count == 0);
    }),
    ("infinite playback repeats until canceled", async () =>
    {
        using var source = new CancellationTokenSource();
        var clock = new FakeClock();
        var mouse = new FakeMouse(clock);
        await IgnoreCancellation(Playback.RunAsync(Sample(), new PlaybackOptions(0, 1, 0), mouse, clock,
            p => { if (p.Round == 3 && p.EventIndex == 1) source.Cancel(); }, source.Token));
        Require(mouse.Items.Count == 9 && mouse.ReleaseCalls == 1);
    }),
    ("coordinate normalization maps to exact physical pixels on negative and large desktops", () =>
    {
        foreach (var bounds in new[] { new DesktopBounds(-1920, -1080, 3840, 2160), new DesktopBounds(0, 0, 7680, 4320),
            new DesktopBounds(-50000, 0, 100000, 1000), new DesktopBounds(0, 0, 1, 1) })
        {
            for (var relative = 0; relative < bounds.Width; relative++)
            {
                var result = CoordinateMapper.ToAbsolute(bounds.Left + relative, bounds.Top, bounds);
                Require(result.X is >= 0 and <= 65535);
                // Windows absolute coordinates have a precision ceiling of 65,536 units.
                if (bounds.Width <= 65536) Require((long)result.X * bounds.Width / 65536 == relative);
            }
        }
        Reject(() => CoordinateMapper.ToAbsolute(-1, 0, new DesktopBounds(0, 0, 10, 10)));
        return Task.CompletedTask;
    }),
    ("recording capacity stops safely and balances all held buttons", () =>
    {
        var buffer = new RecordBuffer(new DesktopBounds(0, 0, 100, 100));
        for (var i = 0; !buffer.IsFull; i++)
            Require(buffer.Append(i, 10, 10, ClickButton.Left, i % 2 == 0 ? ClickAction.Down : ClickAction.Up));
        var result = buffer.Finish(RecordingFile.MaxEvents);
        RecordingFile.Validate(result);
        Require(result.Events.Count <= RecordingFile.MaxEvents);
        return Task.CompletedTask;
    })
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failures++; Console.WriteLine($"FAIL {name}: {error}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void Require(bool value) { if (!value) throw new Exception("Assertion failed."); }
static void Reject(Action action)
{
    try { action(); }
    catch (Exception error) when (error is InvalidDataException or ArgumentOutOfRangeException or System.Text.Json.JsonException) { return; }
    throw new Exception("Expected invalid input to be rejected.");
}
static async Task IgnoreCancellation(Task task) { try { await task; } catch (OperationCanceledException) { } }
static Recording Sample() => new()
{
    DurationMs = 300,
    Screen = new DesktopBounds(0, 0, 1920, 1080),
    Events = [new(100, 20, 30, ClickButton.Left, ClickAction.Down), new(120, 20, 30, ClickButton.Left, ClickAction.Up),
        new(250, 40, 50, ClickButton.Right, ClickAction.Down), new(260, 40, 50, ClickButton.Right, ClickAction.Up)]
};

sealed class FakeClock : IPlaybackClock
{
    public double ElapsedMs { get; private set; }
    public Action<double>? OnWait { get; init; }
    public void Advance(double delta) => ElapsedMs += delta;
    public Task WaitUntilAsync(double targetMs, CancellationToken token)
    {
        OnWait?.Invoke(targetMs);
        token.ThrowIfCancellationRequested();
        ElapsedMs = Math.Max(ElapsedMs, targetMs);
        return Task.CompletedTask;
    }
}

sealed class FakeMouse(FakeClock clock) : IMouseOutput
{
    public List<(double Time, PointerEvent Event)> Items { get; } = [];
    public HashSet<ClickButton> Held { get; } = [];
    public int ReleaseCalls { get; private set; }
    public Action? AfterSend { get; init; }
    public void Send(PointerEvent item)
    {
        Items.Add((clock.ElapsedMs, item));
        if (item.Action == ClickAction.Down) Held.Add(item.Button); else Held.Remove(item.Button);
        AfterSend?.Invoke();
    }
    public void ReleaseAll() { ReleaseCalls++; Held.Clear(); }
}
