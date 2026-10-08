namespace SwitchCast.Models;

/// <summary>
/// Domain model representing a capturable desktop application window.
/// </summary>
public record WindowSource : CaptureSource
{
    public WindowSource()
    {
        Type = SourceType.Window;
    }

    /// <summary>
    /// Native Win32 window handle (HWND).
    /// </summary>
    public nint WindowHandle { get; init; }

    /// <summary>
    /// Process ID owning this window.
    /// </summary>
    public uint ProcessId { get; init; }

    /// <summary>
    /// Executable name of the owning process.
    /// </summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>
    /// Optional file path to process executable.
    /// </summary>
    public string? ProcessPath { get; init; }
}
