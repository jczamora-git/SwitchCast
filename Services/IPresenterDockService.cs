namespace SwitchCast.Services;

/// <summary>
/// Service managing the single instance, lifecycle, and visibility of the Floating Presenter Dock Window.
/// </summary>
public interface IPresenterDockService
{
    /// <summary>
    /// Gets whether the presenter dock window is currently open.
    /// </summary>
    bool IsDockOpen { get; }

    /// <summary>
    /// Gets the native Win32 window handle (HWND) of the dock window, or IntPtr.Zero if closed.
    /// </summary>
    IntPtr DockWindowHandle { get; }

    /// <summary>
    /// Event raised when the presenter dock window is opened.
    /// </summary>
    event EventHandler? DockOpened;

    /// <summary>
    /// Event raised when the presenter dock window is closed.
    /// </summary>
    event EventHandler? DockClosed;

    /// <summary>
    /// Opens or brings the presenter dock window to the foreground.
    /// </summary>
    void ShowDock();

    /// <summary>
    /// Closes the presenter dock window if open.
    /// </summary>
    void CloseDock();

    /// <summary>
    /// Toggles the visibility of the presenter dock window.
    /// </summary>
    void ToggleDock();
}
