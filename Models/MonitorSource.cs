namespace SwitchCast.Models;

/// <summary>
/// Domain model representing a connected physical or virtual display monitor.
/// </summary>
public record MonitorSource : CaptureSource
{
    public MonitorSource()
    {
        Type = SourceType.Display;
    }

    /// <summary>
    /// Native Win32 monitor handle (HMONITOR).
    /// </summary>
    public nint MonitorHandle { get; init; }

    /// <summary>
    /// System device name (e.g., \\.\DISPLAY1).
    /// </summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Display resolution width in physical pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Display resolution height in physical pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Whether this monitor is the primary Windows desktop display.
    /// </summary>
    public bool IsPrimary { get; init; }
}
