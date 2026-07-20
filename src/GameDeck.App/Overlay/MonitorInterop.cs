using System.Runtime.InteropServices;
using GameDeck.Core.Overlay;

namespace GameDeck.App.Overlay;

/// <summary>A display, with its work area pre-converted to WPF DIPs.</summary>
internal sealed record MonitorInfo(string DeviceName, RectD WorkAreaDip, bool IsPrimary);

/// <summary>Multi-monitor lookup; WPF only exposes the primary work area.</summary>
internal static class MonitorInterop
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr _, ref Rect _, IntPtr _) =>
            {
                var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    var scale = GetDpiForMonitor(hMonitor, 0, out var dpiX, out _) == 0 ? 96.0 / dpiX : 1.0;
                    var work = new RectD(
                        info.Work.Left * scale,
                        info.Work.Top * scale,
                        (info.Work.Right - info.Work.Left) * scale,
                        (info.Work.Bottom - info.Work.Top) * scale);
                    monitors.Add(new MonitorInfo(info.Device, work, (info.Flags & 1) != 0));
                }
                return true;
            }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>The stored monitor if it still exists, otherwise the primary.</summary>
    public static MonitorInfo ByNameOrPrimary(IReadOnlyList<MonitorInfo> monitors, string? deviceName) =>
        monitors.FirstOrDefault(m => m.DeviceName == deviceName)
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors[0];

    /// <summary>The monitor whose work area contains the point, otherwise the primary.</summary>
    public static MonitorInfo ByPointOrPrimary(IReadOnlyList<MonitorInfo> monitors, double x, double y) =>
        monitors.FirstOrDefault(m =>
            x >= m.WorkAreaDip.X && x < m.WorkAreaDip.Right &&
            y >= m.WorkAreaDip.Y && y < m.WorkAreaDip.Bottom)
            ?? ByNameOrPrimary(monitors, null);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);
}
