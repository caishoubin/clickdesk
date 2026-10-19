using System.Diagnostics;
using ClickDesk.Core;

namespace ClickDesk;

internal sealed class MainForm : Form
{
    private enum Mode { Idle, PreparingRecord, Recording, PreparingPlayback, Playing, Stopping }
    private sealed record ClickRow(ClickButton Button, int X, int Y, double Offset, double Gap, double? Hold);

    private static readonly Color Ink = Color.FromArgb(25, 42, 62);
    private static readonly Color Muted = Color.FromArgb(102, 117, 136);
    private static readonly Color Teal = Color.FromArgb(0, 119, 108);
    private static readonly Color Red = Color.FromArgb(193, 53, 67);
    private static readonly Color Surface = Color.FromArgb(244, 247, 250);

    private readonly Button _record = ActionButton("●  开始录制   F6", Red, true);
    private readonly Button _play = ActionButton("▶  开始回放   F7", Teal, true);
    private readonly Button _stop = ActionButton("■  停止   F8", Ink, false);
    private readonly Button _open = ActionButton("打开录制", Ink, false);
    private readonly Button _save = ActionButton("保存录制", Ink, false);
    private readonly Button _clear = ActionButton("清空", Ink, false);
    private readonly Label _countValue = ValueLabel("0");
    private readonly Label _durationValue = ValueLabel("00:00.0");
    private readonly Label _stateValue = ValueLabel("准备就绪");
    private readonly Label _details = TextLabel("录下点击，让重复的工作自动完成。", Muted);
    private readonly Label _footer = TextLabel("F6 开始 / 结束录制    ·    F7 开始 / 停止回放    ·    F8 随时停止", Muted);
    private readonly Label _hotkeyInfo = TextLabel("全局快捷键正在初始化…", Muted);
    private readonly Label _empty = TextLabel("还没有点击记录\n\n点击“开始录制”或按 F6，切换到目标窗口进行操作。", Muted);
    private readonly NumericUpDown _repeat = Number(1, 999_999, 1, 0);
    private readonly NumericUpDown _speed = Number(0.1m, 10, 1, 2);
    private readonly NumericUpDown _gap = Number(0, 3600, 1, 1);
    private readonly NumericUpDown _countdown = Number(1, 10, 3, 0);
    private readonly CheckBox _forever = new() { Text = "持续循环，直到手动停止", AutoSize = true };
    private readonly CheckBox _minimize = new() { Text = "开始时自动最小化", Checked = true, AutoSize = true };
    private readonly DataGridView _grid = new();
    private readonly NotifyIcon _tray = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private readonly List<ClickRow> _rows = [];
    private readonly Dictionary<ClickButton, int> _pendingRows = [];
    private readonly HashSet<ClickButton> _ignoredButtons = [];
    private readonly Stopwatch _recordWatch = new();
    private readonly List<int> _registeredHotkeys = [];

    private Mode _mode;
    private Recording _recording = new() { Screen = NativeMethods.Desktop() };
    private RecordBuffer? _buffer;
    private MouseHook? _hook;
    private CancellationTokenSource? _operation;
    private PlaybackProgress? _progress;
    private int _processedEvents;
    private double _lastDown;
    private bool _dirty, _stopHotkeyReady, _restoreAfterRun, _closeWhenIdle;
    private bool _promptOpen;
    private string? _path;
    private ClickTestForm? _testForm;
    private readonly string? _initialPath;

    internal MainForm(string? initialPath)
    {
        _initialPath = initialPath;
        Text = "ClickDesk · 鼠标录制器";
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Surface;
        ForeColor = Ink;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1120, 780);
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        BuildInterface();
        _record.Click += async (_, _) => await StartRecordingAsync();
        _play.Click += async (_, _) => await StartPlaybackAsync();
        _stop.Click += (_, _) => StopCurrent();
        _open.Click += (_, _) => OpenRecording();
        _save.Click += (_, _) => SaveRecording();
        _clear.Click += (_, _) =>
        {
            if (!MaybeSave()) return;
            _recording = new Recording { Screen = NativeMethods.Desktop() };
            _path = null;
            _dirty = false;
            ResetRows();
            RefreshInterface("记录已清空，可以开始新的录制。");
        };
        _forever.CheckedChanged += (_, _) => _repeat.Enabled = _mode == Mode.Idle && !_forever.Checked;
        _timer.Tick += (_, _) => OnTimer();
        ConfigureTray();
        RefreshInterface();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(28, 22, 28, 12), Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        var title = new Label { Text = "ClickDesk", Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            ForeColor = Ink, AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty };
        heading.Controls.Add(title, 0, 0);
        heading.Controls.Add(new Label { Text = "鼠标录制器  /  WINDOWS", ForeColor = Teal,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight }, 1, 0);
        root.Controls.Add(heading, 0, 0);

        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 0, 0, 12) };
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        stats.Controls.Add(StatCard("点击次数", _countValue), 0, 0);
        stats.Controls.Add(StatCard("录制时长", _durationValue), 1, 0);
        stats.Controls.Add(StatCard("当前状态", _stateValue), 2, 0);
        root.Controls.Add(stats, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        foreach (var button in new[] { _record, _play, _stop, _open, _save, _clear }) actions.Controls.Add(button);
        root.Controls.Add(actions, 0, 2);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
        var timeline = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            BackColor = Color.White, Padding = new Padding(16), Margin = new Padding(0, 0, 14, 0) };
        timeline.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        timeline.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        timeline.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        timeline.Controls.Add(new Label { Text = "点击时间线", AutoSize = true,
            Font = new Font(Font, FontStyle.Bold), ForeColor = Ink }, 0, 0);
        _details.Dock = DockStyle.Fill;
        timeline.Controls.Add(_details, 0, 1);
        ConfigureGrid();
        var gridHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        gridHost.Controls.Add(_grid);
        _empty.Dock = DockStyle.Fill;
        _empty.TextAlign = ContentAlignment.MiddleCenter;
        gridHost.Controls.Add(_empty);
        timeline.Controls.Add(gridHost, 0, 2);
        content.Controls.Add(timeline, 0, 0);
        content.Controls.Add(SettingsPanel(), 1, 0);
        root.Controls.Add(content, 0, 3);
        _footer.Dock = DockStyle.Fill;
        root.Controls.Add(_footer, 0, 4);
        var note = TextLabel("按屏幕绝对坐标回放。请保持目标窗口位置、屏幕布局和缩放设置一致。", Muted);
        note.Dock = DockStyle.Fill;
        root.Controls.Add(note, 0, 5);
    }

    private Control SettingsPanel()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true, Margin = Padding.Empty };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true, Padding = new Padding(18), Width = 260 };
        flow.Controls.Add(new Label { Text = "回放设置", Font = new Font(Font, FontStyle.Bold), AutoSize = true,
            Margin = new Padding(0, 0, 0, 16) });
        AddSetting(flow, "重复次数", _repeat);
        _forever.Margin = new Padding(0, 2, 0, 12);
        flow.Controls.Add(_forever);
        AddSetting(flow, "回放速度（倍，1 = 原速）", _speed);
        AddSetting(flow, "每轮额外等待（秒）", _gap);
        AddSetting(flow, "开始前倒计时（秒）", _countdown);
        _minimize.Margin = new Padding(0, 12, 0, 10);
        flow.Controls.Add(_minimize);
        _hotkeyInfo.MaximumSize = new Size(220, 0);
        _hotkeyInfo.AutoSize = true;
        _hotkeyInfo.Margin = new Padding(0, 6, 0, 10);
        flow.Controls.Add(_hotkeyInfo);
        var help = TextLabel("快速开始\n\n1. 按 F6，倒计时后进行点击\n2. 再按 F6 结束录制\n3. 按 F7，开始重复操作\n\n按 F8 可停止录制、倒计时或回放。\n\n记录左 / 右 / 中键及双击节奏。\n不记录键盘、滚轮和连续移动轨迹。", Muted);
        help.AutoSize = true;
        help.MaximumSize = new Size(224, 0);
        help.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(help);
        var test = ActionButton("打开点击测试板", Teal, false);
        test.Margin = new Padding(0, 16, 0, 0);
        test.Click += (_, _) =>
        {
            if (_testForm is null || _testForm.IsDisposed) _testForm = new ClickTestForm();
            _testForm.Show();
            _testForm.Activate();
        };
        flow.Controls.Add(test);
        scroll.Controls.Add(flow);
        return scroll;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeight = 38;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface,
            ForeColor = Muted, Font = new Font(Font, FontStyle.Bold), SelectionBackColor = Surface };
        _grid.DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Ink, BackColor = Color.White,
            SelectionBackColor = Color.FromArgb(222, 242, 239), SelectionForeColor = Ink, Padding = new Padding(4) };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 253);
        _grid.GridColor = Surface;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.RowTemplate.Height = 34;
        _grid.VirtualMode = true;
        foreach (var (name, title, weight) in new[] { ("index", "序号", 45), ("button", "按键", 55),
            ("point", "坐标 (X, Y)", 115), ("gap", "间隔 / 秒", 85), ("hold", "按住 / 秒", 85), ("time", "时间 / 秒", 85) })
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title,
                FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.CellValueNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            var row = _rows[e.RowIndex];
            e.Value = e.ColumnIndex switch
            {
                0 => (e.RowIndex + 1).ToString(),
                1 => row.Button switch { ClickButton.Left => "左键", ClickButton.Right => "右键", _ => "中键" },
                2 => $"{row.X}, {row.Y}",
                3 => (row.Gap / 1000).ToString("0.000"),
                4 => row.Hold is { } hold ? (hold / 1000).ToString("0.000") : "按住中…",
                5 => (row.Offset / 1000).ToString("0.000"),
                _ => ""
            };
        };
    }

    private static Label TextLabel(string text, Color color) => new()
    { Text = text, ForeColor = color, AutoSize = true, Margin = Padding.Empty };

    private static Label ValueLabel(string text) => new()
    { Text = text, AutoSize = true, Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
        ForeColor = Ink, Margin = Padding.Empty, Dock = DockStyle.Fill };

    private static Button ActionButton(string text, Color color, bool primary) => new()
    {
        Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Height = 40, MinimumSize = new Size(0, 40), Padding = new Padding(12, 6, 12, 6),
        FlatStyle = FlatStyle.Flat, BackColor = primary ? color : Color.White,
        ForeColor = primary ? Color.White : color, Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 10, 0), FlatAppearance = { BorderSize = 0 }
    };

    private static NumericUpDown Number(decimal min, decimal max, decimal value, int decimals) => new()
    { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimals,
        Increment = decimals == 0 ? 1 : 0.1m, Width = 218, Height = 32, ThousandsSeparator = true,
        Margin = new Padding(0, 0, 0, 8) };

    private static void AddSetting(FlowLayoutPanel flow, string text, Control control)
    {
        var label = TextLabel(text, Muted);
        label.Margin = new Padding(0, 0, 0, 7);
        flow.Controls.Add(label);
        flow.Controls.Add(control);
    }

    private static Control StatCard(string title, Label value)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White,
            Padding = new Padding(16, 8, 16, 4), Margin = new Padding(0, 0, 12, 0), RowCount = 2, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(TextLabel(title, Muted), 0, 0);
        panel.Controls.Add(value, 0, 1);
        return panel;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // A laptop at 150% scaling can have less than 720 logical pixels of usable height.
        var area = Screen.FromControl(this).WorkingArea;
        var available = new Size(Math.Max(1, area.Width - 32), Math.Max(1, area.Height - 32));
        MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
        Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var missing = new List<string>();
        foreach (var (id, key) in new[] { (1, Keys.F6), (2, Keys.F7), (3, Keys.F8) })
        {
            if (NativeMethods.RegisterHotKey(Handle, id, NativeMethods.ModNoRepeat, (uint)key))
            { _registeredHotkeys.Add(id); if (id == 3) _stopHotkeyReady = true; }
            else missing.Add(key.ToString());
        }
        _hotkeyInfo.Text = missing.Count == 0 ? "●  全局快捷键已就绪" : $"快捷键被占用：{string.Join("、", missing)}。\n请关闭占用程序后重新启动。";
        _hotkeyInfo.ForeColor = missing.Count == 0 ? Teal : Red;
        _tray.Visible = true;
        _timer.Start();
        if (!string.IsNullOrWhiteSpace(_initialPath)) LoadRecording(_initialPath);
        RefreshInterface();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey && !_promptOpen)
        {
            var id = (int)m.WParam;
            if (id == 1) _ = StartRecordingAsync();
            else if (id == 2) _ = StartPlaybackAsync();
            else if (id == 3) StopCurrent();
            return;
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _mode != Mode.Idle) { StopCurrent(); return true; }
        if (_mode == Mode.Idle && !_promptOpen)
        {
            if (keyData == (Keys.Control | Keys.S)) { SaveRecording(); return true; }
            if (keyData == (Keys.Control | Keys.O)) { OpenRecording(); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ConfigureTray()
    {
        _tray.Icon = Icon;
        _tray.Text = "ClickDesk · F8 随时停止";
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => RestoreWindow());
        menu.Items.Add("停止操作 (F8)", null, (_, _) => StopCurrent());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Close());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreWindow();
    }

    private async Task StartRecordingAsync()
    {
        if (_promptOpen) return;
        if (_mode is Mode.Recording or Mode.PreparingRecord) { StopCurrent(); return; }
        if (_mode != Mode.Idle || !CheckStopHotkey() || !MaybeSave()) return;
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        _mode = Mode.PreparingRecord;
        PrepareWindow();
        RefreshInterface();
        try
        {
            await CountdownAsync("录制", token);
            token.ThrowIfCancellationRequested();
            // Ignore buttons already held at recording start until they are released.
            _ignoredButtons.Clear();
            foreach (var (button, key) in new[] { (ClickButton.Left, 1), (ClickButton.Right, 2), (ClickButton.Middle, 4) })
                if ((NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0) _ignoredButtons.Add(button);
            var nextBuffer = new RecordBuffer(NativeMethods.Desktop());
            _hook = new MouseHook(OnMouseEvent);
            _buffer = nextBuffer;
            _recording = new Recording { Screen = NativeMethods.Desktop() };
            _path = null;
            _dirty = false;
            ResetRows();
            _recordWatch.Restart();
            _mode = Mode.Recording;
            RefreshInterface("正在录制。按 F6 或 F8 结束；对本工具窗口的点击会自动忽略。");
        }
        catch (OperationCanceledException) { FinishOperation("已取消录制倒计时。"); }
        catch (Exception ex) { FinishOperation("无法开始录制。"); ShowError(ex); }
    }

    private void OnMouseEvent(int x, int y, ClickButton button, ClickAction action)
    {
        if (_mode != Mode.Recording || _buffer is null) return;
        if (action == ClickAction.Down)
        {
            var hit = NativeMethods.WindowFromPoint(new NativeMethods.Point { X = x, Y = y });
            if (NativeMethods.GetAncestor(hit, 2) == Handle)
            { _ignoredButtons.Add(button); return; }
        }
        else if (_ignoredButtons.Remove(button)) return;
        _buffer.Append(_recordWatch.Elapsed.TotalMilliseconds, x, y, button, action);
    }

    private async Task StartPlaybackAsync()
    {
        if (_promptOpen) return;
        if (_mode is Mode.Playing or Mode.PreparingPlayback) { StopCurrent(); return; }
        if (_mode != Mode.Idle || _recording.Events.Count == 0 || !CheckStopHotkey()) return;
        var desktop = NativeMethods.Desktop();
        if (_recording.Events.Any(item => !desktop.Contains(item.X, item.Y)))
        { ShowError(new InvalidOperationException("部分录制坐标超出了当前屏幕，请恢复原来的显示器布局后再回放。")); return; }
        if (desktop != _recording.Screen && Ask("当前屏幕布局与录制时不同，点击位置可能发生变化。\n\n是否仍按原坐标回放？",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        var options = new PlaybackOptions(_forever.Checked ? 0 : (int)_repeat.Value, (double)_speed.Value, (double)_gap.Value * 1000);
        _operation = new CancellationTokenSource();
        var token = _operation.Token;
        var recording = _recording;
        _mode = Mode.PreparingPlayback;
        _progress = null;
        PrepareWindow();
        RefreshInterface();
        try
        {
            await CountdownAsync("回放", token);
            token.ThrowIfCancellationRequested();
            _mode = Mode.Playing;
            RefreshInterface("正在按录制节奏回放。按 F7 或 F8 停止。");
            await Task.Run(() => Playback.RunAsync(recording, options, new WindowsMouseOutput(desktop), new PlaybackClock(),
                p => Volatile.Write(ref _progress, p), token), token);
            FinishOperation("回放已完成。");
        }
        catch (OperationCanceledException) { FinishOperation("已停止，模拟按住的鼠标按键已松开。"); }
        catch (Exception ex) { FinishOperation("回放已停止。"); ShowError(ex); }
    }

    private async Task CountdownAsync(string action, CancellationToken token)
    {
        for (var seconds = (int)_countdown.Value; seconds > 0; seconds--)
        {
            token.ThrowIfCancellationRequested();
            _stateValue.Text = $"{seconds} 秒后{action}";
            _details.Text = $"请切换到目标窗口，按 F8 可取消。";
            _tray.Text = $"ClickDesk · {seconds} 秒后{action} · F8 取消";
            await Task.Delay(1000, token);
        }
    }

    private bool CheckStopHotkey()
    {
        if (_stopHotkeyReady) return true;
        ShowError(new InvalidOperationException("F8 紧急停止快捷键不可用。请关闭占用 F8 的程序，然后重新启动 ClickDesk。"));
        return false;
    }

    private void PrepareWindow()
    {
        _restoreAfterRun = _minimize.Checked || WindowState == FormWindowState.Minimized;
        if (_minimize.Checked) WindowState = FormWindowState.Minimized;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void StopCurrent()
    {
        if (_mode is Mode.Idle or Mode.Stopping) return;
        if (_mode == Mode.Recording)
        {
            _hook?.Dispose();
            _hook = null;
            _recordWatch.Stop();
            _recording = _buffer!.Finish(_recordWatch.Elapsed.TotalMilliseconds);
            _buffer = null;
            _dirty = _recording.Events.Count > 0;
            ResetRows();
            AppendRows(_recording.Events);
            FinishOperation($"录制完成：{_recording.ClickCount} 次点击。可以保存，或按 F7 回放。");
            return;
        }
        _mode = Mode.Stopping;
        _operation?.Cancel();
        RefreshInterface("正在停止…");
    }

    private void FinishOperation(string message)
    {
        _hook?.Dispose();
        _hook = null;
        _operation?.Dispose();
        _operation = null;
        _mode = Mode.Idle;
        _tray.Text = "ClickDesk · F8 随时停止";
        RefreshInterface(message);
        if (_restoreAfterRun) RestoreWindow();
        _restoreAfterRun = false;
        if (_closeWhenIdle)
        {
            _closeWhenIdle = false;
            BeginInvoke(Close);
        }
    }

    private void OnTimer()
    {
        if (_mode == Mode.Recording && _buffer is { } buffer)
        {
            AppendRows(buffer.Events);
            _countValue.Text = buffer.ClickCount.ToString("N0");
            _durationValue.Text = Duration(_recordWatch.Elapsed.TotalMilliseconds);
            if (_hook?.CallbackError is { } error) { StopCurrent(); ShowError(error); }
            else if (buffer.IsFull || _recordWatch.Elapsed.TotalMilliseconds >= RecordingFile.MaxDurationMs)
            { StopCurrent(); _details.Text = "已达到单次录制上限，录制已自动停止并保留。"; }
        }
        if (_mode == Mode.Playing && Volatile.Read(ref _progress) is { } progress)
        {
            _stateValue.Text = $"回放 · 第 {progress.Round:N0} 轮";
            _details.Text = $"事件 {progress.EventIndex:N0} / {progress.EventCount:N0}  ·  F8 随时停止";
        }
    }

    private static string Duration(double milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}" :
            $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 100}";
    }

    private void ResetRows()
    {
        _rows.Clear(); _pendingRows.Clear(); _processedEvents = 0; _lastDown = 0;
        _grid.RowCount = 0;
        _empty.Visible = true;
    }

    private void AppendRows(IReadOnlyList<PointerEvent> events)
    {
        var oldCount = _rows.Count;
        for (; _processedEvents < events.Count; _processedEvents++)
        {
            var item = events[_processedEvents];
            if (item.Action == ClickAction.Down)
            {
                _pendingRows[item.Button] = _rows.Count;
                _rows.Add(new ClickRow(item.Button, item.X, item.Y, item.OffsetMs, item.OffsetMs - _lastDown, null));
                _lastDown = item.OffsetMs;
            }
            else if (_pendingRows.Remove(item.Button, out var index))
                _rows[index] = _rows[index] with { Hold = item.OffsetMs - _rows[index].Offset };
        }
        _empty.Visible = _rows.Count == 0;
        _grid.RowCount = _rows.Count;
        if (_rows.Count > oldCount && _mode == Mode.Recording) _grid.FirstDisplayedScrollingRowIndex = _rows.Count - 1;
        _grid.Invalidate();
    }

    private void RefreshInterface(string? message = null)
    {
        var idle = _mode == Mode.Idle;
        _record.Enabled = idle || _mode is Mode.PreparingRecord or Mode.Recording;
        _record.Text = _mode is Mode.PreparingRecord or Mode.Recording ? "■  结束录制   F6" : "●  开始录制   F6";
        _play.Enabled = (idle && _recording.Events.Count > 0) || _mode is Mode.PreparingPlayback or Mode.Playing;
        _play.Text = _mode is Mode.PreparingPlayback or Mode.Playing ? "■  停止回放   F7" : "▶  开始回放   F7";
        _stop.Enabled = !idle && _mode != Mode.Stopping;
        _open.Enabled = idle;
        _save.Enabled = idle && _recording.Events.Count > 0;
        _clear.Enabled = idle && _recording.Events.Count > 0;
        foreach (var control in new Control[] { _speed, _gap, _countdown, _forever, _minimize }) control.Enabled = idle;
        _repeat.Enabled = idle && !_forever.Checked;
        _stateValue.ForeColor = _mode == Mode.Recording ? Red : _mode == Mode.Playing ? Teal : Ink;
        _stateValue.Text = _mode switch { Mode.Recording => "正在录制", Mode.Playing => "正在回放",
            Mode.PreparingRecord or Mode.PreparingPlayback => "准备中…", Mode.Stopping => "正在停止…", _ => "准备就绪" };
        _tray.Text = $"ClickDesk · {_stateValue.Text} · F8 随时停止";
        if (_mode != Mode.Recording)
        {
            _countValue.Text = _recording.ClickCount.ToString("N0");
            _durationValue.Text = Duration(_recording.DurationMs);
        }
        if (message is not null) _details.Text = message;
        Text = $"{(_dirty ? "* " : "")}{(_path is null ? "ClickDesk · 鼠标录制器" : Path.GetFileName(_path) + " · ClickDesk")}";
    }

    private DialogResult Ask(string text, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        _promptOpen = true;
        try { return MessageBox.Show(this, text, "ClickDesk", buttons, icon); }
        finally { _promptOpen = false; }
    }

    private bool MaybeSave()
    {
        if (!_dirty) return true;
        RestoreWindow();
        var answer = Ask("当前录制尚未保存，是否先保存？", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer == DialogResult.No || (answer == DialogResult.Yes && SaveRecording());
    }

    private bool SaveRecording()
    {
        if (_mode != Mode.Idle || _recording.Events.Count == 0) return false;
        using var dialog = new SaveFileDialog { Filter = "ClickDesk 录制 (*.clickdesk.json)|*.clickdesk.json|JSON 文件 (*.json)|*.json",
            DefaultExt = "clickdesk.json", AddExtension = true, FileName = _path is null ?
                $"鼠标录制-{DateTime.Now:yyyyMMdd-HHmmss}.clickdesk.json" : Path.GetFileName(_path) };
        _promptOpen = true;
        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            RecordingFile.Save(dialog.FileName, _recording);
            _path = dialog.FileName;
            _dirty = false;
            RefreshInterface("录制已保存。下次打开文件即可再次回放。");
            return true;
        }
        catch (Exception ex) { ShowError(ex); return false; }
        finally { _promptOpen = false; }
    }

    private void OpenRecording()
    {
        if (_mode != Mode.Idle || !MaybeSave()) return;
        using var dialog = new OpenFileDialog { Filter = "ClickDesk 录制 (*.clickdesk.json;*.json)|*.clickdesk.json;*.json", CheckFileExists = true };
        _promptOpen = true;
        try { if (dialog.ShowDialog(this) == DialogResult.OK) LoadRecording(dialog.FileName); }
        finally { _promptOpen = false; }
    }

    private void LoadRecording(string path)
    {
        try
        {
            var loaded = RecordingFile.Load(path);
            _recording = loaded;
            _path = path;
            _dirty = false;
            ResetRows();
            AppendRows(loaded.Events);
            RefreshInterface($"已打开：{Path.GetFileName(path)}");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception error) => Ask(error.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_mode != Mode.Idle)
        {
            e.Cancel = true;
            _closeWhenIdle = true;
            StopCurrent();
        }
        else if (!MaybeSave()) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _operation?.Cancel();
            _hook?.Dispose();
            foreach (var id in _registeredHotkeys) NativeMethods.UnregisterHotKey(Handle, id);
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _testForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
