using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service responsible for managing Windows application window focus and activation.
/// </summary>
public interface IWindowActivationService
{
    /// <summary>
    /// Attempts to bring the specified window to the foreground.
    /// </summary>
    /// <param name="hWnd">Native window handle.</param>
    /// <returns>True if activation succeeded or window is now in the foreground; otherwise, false.</returns>
    bool ActivateWindow(IntPtr hWnd);

    /// <summary>
    /// Attempts to bring the application window corresponding to the specified capture source to the foreground.
    /// Returns false without error for non-window sources (e.g. monitors).
    /// </summary>
    /// <param name="source">Capture source to activate.</param>
    /// <returns>True if the window was activated; otherwise, false.</returns>
    bool ActivateSource(CaptureSource? source);

    /// <summary>
    /// Registers the main application window handle for centralized foreground activation.
    /// </summary>
    /// <param name="hWnd">Native window handle of MainWindow.</param>
    void RegisterMainWindowHandle(IntPtr hWnd);

    /// <summary>
    /// Attempts to bring the main application window to the foreground, restoring if minimized.
    /// </summary>
    /// <returns>True if MainWindow was activated; otherwise, false.</returns>
    bool ActivateMainWindow();
}
