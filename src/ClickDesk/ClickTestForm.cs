using System.Diagnostics;

namespace ClickDesk;

/// <summary>A harmless local target for trying the recorder before using another application.</summary>
internal sealed class ClickTestForm : Form
{
    internal ClickTestForm()
    {
        Text = "ClickDesk · 点击测试板";
        Font = new Font("Microsoft YaHei UI", 10F);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 480);
        MinimumSize = new Size(500, 420);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(244, 247, 250);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "在这里试一试\n按 F6 录制 A → B → C，再按 F6 结束，按 F7 回放。\n保持本窗口的位置不变，观察顺序和间隔是否相同。",
            Dock = DockStyle.Fill, AutoSize = false }, 0, 0);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        for (var i = 0; i < 3; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
        var summary = new Label { Text = "等待点击…", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var log = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
        var watch = Stopwatch.StartNew();
        double? last = null;
        var counts = new int[3];
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var name = ((char)('A' + i)).ToString();
            var button = new Button { Text = name, Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 22F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 119, 108), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 10, 10), FlatAppearance = { BorderSize = 0 } };
            button.MouseDown += (_, e) =>
            {
                var now = watch.Elapsed.TotalMilliseconds;
                var gap = last is null ? "首次点击" : $"间隔 {(now - last.Value) / 1000:0.000} 秒";
                last = now;
                counts[index]++;
                summary.Text = $"A: {counts[0]}    B: {counts[1]}    C: {counts[2]}";
                log.Items.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}    {name} / {e.Button}    {gap}");
                if (log.Items.Count > 200) log.Items.RemoveAt(200);
            };
            buttons.Controls.Add(button, i, 0);
        }
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(summary, 0, 2);
        root.Controls.Add(log, 0, 3);
        Controls.Add(root);
    }
}
