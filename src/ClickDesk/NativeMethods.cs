using System.ComponentModel;
using System.Runtime.InteropServices;
using ClickDesk.Core;

namespace ClickDesk;

internal static class NativeMethods
{
    internal const int WmHotkey = 0x0312;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint MouseMove = 0x0001, MouseAbsolute = 0x8000, MouseVirtualDesk = 0x4000;
    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseHookData
    {
        public Point Point;
        public uint MouseData, Flags, Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input { public uint Type; public InputUnion Data; }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr handle);
    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr handle, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int key);

    internal static DesktopBounds Desktop()
    {
        var rect = SystemInformation.VirtualScreen;
        return new DesktopBounds(rect.Left, rect.Top, rect.Width, rect.Height);
    }
}

internal sealed class MouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _proc;
    private IntPtr _handle;
    internal Exception? CallbackError { get; private set; }

    internal MouseHook(Action<int, int, ClickButton, ClickAction> onEvent)
    {
        _proc = (code, wParam, lParam) =>
        {
            if (code >= 0)
            {
                try
                {
                    var data = Marshal.PtrToStructure<NativeMethods.MouseHookData>(lParam);
                    // Ignore simulated events (including our own playback).
                    if ((data.Flags & 0x01) == 0)
                    {
                        var parsed = (int)wParam switch
                        {
                            0x0201 => (ClickButton.Left, ClickAction.Down),
                            0x0202 => (ClickButton.Left, ClickAction.Up),
                            0x0204 => (ClickButton.Right, ClickAction.Down),
                            0x0205 => (ClickButton.Right, ClickAction.Up),
                            0x0207 => (ClickButton.Middle, ClickAction.Down),
                            0x0208 => (ClickButton.Middle, ClickAction.Up),
                            _ => ((ClickButton Button, ClickAction Action)?)null
                        };
                        if (parsed is { } item)
                            onEvent(data.Point.X, data.Point.Y, item.Button, item.Action);
                    }
                }
                catch (Exception ex) { CallbackError = ex; }
            }
            return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
        };
        _handle = NativeMethods.SetWindowsHookEx(14, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法启动全局鼠标录制。");
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }
        GC.KeepAlive(_proc);
    }
}

internal sealed class WindowsMouseOutput(DesktopBounds desktop) : IMouseOutput
{
    private readonly HashSet<ClickButton> _held = [];

    public void Send(PointerEvent item)
    {
        var (x, y) = CoordinateMapper.ToAbsolute(item.X, item.Y, desktop);
        SendRaw(x, y, Flags(item.Button, item.Action) | NativeMethods.MouseMove |
            NativeMethods.MouseAbsolute | NativeMethods.MouseVirtualDesk);
        if (item.Action == ClickAction.Down) _held.Add(item.Button);
        else _held.Remove(item.Button);
    }

    public void ReleaseAll()
    {
        Exception? error = null;
        foreach (var button in _held.ToArray())
        {
            try { SendRaw(0, 0, Flags(button, ClickAction.Up)); _held.Remove(button); }
            catch (Exception ex) { error ??= ex; }
        }
        if (error is not null) throw error;
    }

    private static uint Flags(ClickButton button, ClickAction action) => (button, action) switch
    {
        (ClickButton.Left, ClickAction.Down) => 0x0002,
        (ClickButton.Left, ClickAction.Up) => 0x0004,
        (ClickButton.Right, ClickAction.Down) => 0x0008,
        (ClickButton.Right, ClickAction.Up) => 0x0010,
        (ClickButton.Middle, ClickAction.Down) => 0x0020,
        (ClickButton.Middle, ClickAction.Up) => 0x0040,
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    private static void SendRaw(int x, int y, uint flags)
    {
        var inputs = new[] { new NativeMethods.Input
        {
            Type = 0,
            Data = new NativeMethods.InputUnion { Mouse = new NativeMethods.MouseInput
            { Dx = x, Dy = y, Flags = flags, ExtraInfo = new UIntPtr(0x43444B31) } }
        } };
        if (NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Windows 拒绝了模拟点击。如果目标程序以管理员身份运行，请以管理员身份重新启动 ClickDesk。");
    }
}
