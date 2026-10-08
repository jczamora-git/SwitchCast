namespace SwitchCast.Services;

/// <summary>
/// Service managing the lifecycle, activation, and state of the dedicated Presentation Output Window.
/// </summary>
public interface IPresentationWindowService
{
    /// <summary>
    /// Gets whether the presentation output window is currently open.
    /// </summary>
    bool IsWindowOpen { get; }

    /// <summary>
    /// Gets the native Win32 window handle (HWND) of the presentation window, or IntPtr.Zero if closed.
    /// </summary>
    IntPtr WindowHandle { get; }

    /// <summary>
    /// Event raised when the presentation output window is opened or activated.
    /// </summary>
    event EventHandler? WindowOpened;

    /// <summary>
    /// Event raised when the presentation output window is closed.
    /// </summary>
    event EventHandler? WindowClosed;

    /// <summary>
    /// Shows or activates the single presentation output window.
    /// </summary>
    void ShowPresentationWindow();

    /// <summary>
    /// Closes the presentation output window if open.
    /// </summary>
    void ClosePresentationWindow();
}
