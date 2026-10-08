using System.Runtime.InteropServices;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Native Win32 monitor discovery service that enumerates physical and virtual display monitors.
/// </summary>
public class Win32MonitorDiscoveryService : IMonitorDiscoveryService
{
    private const int MONITORINFOF_PRIMARY = 0x00000001;

    public Task<IReadOnlyList<MonitorSource>> EnumerateMonitorsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<MonitorSource>>(() =>
        {
            var monitors = new List<MonitorSource>();
            var displayIndex = 1;

            EnumDisplayMonitors(nint.Zero, nint.Zero, (nint hMonitor, nint _, ref RECT _, nint _) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var info = new MONITORINFOEX();
                info.cbSize = Marshal.SizeOf(info);

                if (GetMonitorInfo(hMonitor, ref info))
                {
                    var width = Math.Abs(info.rcMonitor.Right - info.rcMonitor.Left);
                    var height = Math.Abs(info.rcMonitor.Bottom - info.rcMonitor.Top);
                    var isPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0;
                    var deviceName = info.szDevice.TrimEnd('\0');

                    var friendlyTitle = isPrimary
                        ? $"Display {displayIndex} — Primary ({width} × {height})"
                        : $"Display {displayIndex} ({width} × {height})";

                    var source = new MonitorSource
                    {
                        Id = $"mon_{hMonitor}_{deviceName.TrimStart('\\', '.')}",
                        Title = friendlyTitle,
                        MonitorHandle = hMonitor,
                        DeviceName = deviceName,
                        Width = width,
                        Height = height,
                        IsPrimary = isPrimary,
                        IsAvailable = true
                    };

                    monitors.Add(source);
                    displayIndex++;
                }

                return true;
            }, nint.Zero);

            // Sort primary display first, then by device name
            return monitors.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DeviceName).ToList();
        }, cancellationToken);
    }

    #region Win32 P/Invoke Declarations

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    #endregion
}
