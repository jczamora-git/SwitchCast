using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Service providing standalone Win32 GDI window and monitor capture diagnostics.
/// </summary>
public interface IWin32DiagnosticCaptureService
{
    /// <summary>
    /// Captures a single diagnostic frame from a window handle (HWND) using GDI PrintWindow and BitBlt fallbacks.
    /// </summary>
    /// <param name="hWnd">Target window handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An independently owned SoftwareBitmap, or null if window is invalid or minimized.</returns>
    Task<SoftwareBitmap?> CaptureWindowDiagnosticAsync(nint hWnd, CancellationToken cancellationToken = default);
}
