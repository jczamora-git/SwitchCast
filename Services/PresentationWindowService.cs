using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Views;

namespace SwitchCast.Services;

/// <summary>
/// Service managing the single instance and lifecycle of the Presentation Output Window.
/// </summary>
public sealed class PresentationWindowService : IPresentationWindowService
{
    private readonly IWindowActivationService? _windowActivationService;
    private PresentationWindow? _window;
    private PresentationDisplayMode _displayMode = PresentationDisplayMode.Windowed;

    public PresentationWindowService(IWindowActivationService? windowActivationService = null)
    {
        _windowActivationService = windowActivationService;
    }

    public bool IsWindowOpen => _window is not null;

    public IntPtr WindowHandle => _window?.WindowHandle ?? IntPtr.Zero;

    public PresentationDisplayMode DisplayMode => _window?.DisplayMode ?? _displayMode;

    public event EventHandler? WindowOpened;
    public event EventHandler? WindowClosed;
    public event EventHandler<PresentationDisplayMode>? DisplayModeChanged;

    public void ShowPresentationWindow()
    {
        if (_window is null)
        {
            _window = new PresentationWindow();
            _window.Closed += OnWindowClosed;
            _window.DisplayModeChanged += OnWindowDisplayModeChanged;
            _window.Activate();
            WindowOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _window.Activate();
            WindowOpened?.Invoke(this, EventArgs.Empty);
        }

        if (_window.WindowHandle != IntPtr.Zero && _windowActivationService is not null)
        {
            _windowActivationService.ActivateWindow(_window.WindowHandle);
        }
    }

    public void ClosePresentationWindow()
    {
        if (_window is not null)
        {
            var target = _window;
            _window = null;
            Diagnostics.SwitchCastDiagnostics.LogInfo(
                nameof(PresentationWindowService),
                $"Close requested | HWND=0x{target.WindowHandle:X} | PID={Environment.ProcessId}");

            target.Closed -= OnWindowClosed;
            target.DisplayModeChanged -= OnWindowDisplayModeChanged;
            target.Cleanup();
            target.Close();
            _displayMode = PresentationDisplayMode.Windowed;
            WindowClosed?.Invoke(this, EventArgs.Empty);
            DisplayModeChanged?.Invoke(this, PresentationDisplayMode.Windowed);
        }
    }

    public void SetDisplayMode(PresentationDisplayMode mode)
    {
        _displayMode = mode;
        if (_window is not null)
        {
            _window.SetDisplayMode(mode);
        }
        else
        {
            DisplayModeChanged?.Invoke(this, mode);
        }
    }

    public void ToggleDisplayMode()
    {
        if (_window is not null)
        {
            _window.ToggleDisplayMode();
        }
        else
        {
            SetDisplayMode(DisplayMode == PresentationDisplayMode.Fullscreen
                ? PresentationDisplayMode.Windowed
                : PresentationDisplayMode.Fullscreen);
        }
    }

    public void MoveToMonitor(MonitorSource monitor, bool andFullscreen = false)
    {
        if (_window is not null)
        {
            _window.MoveToMonitor(monitor, andFullscreen);
            _displayMode = _window.DisplayMode;
        }
        else if (andFullscreen)
        {
            _displayMode = PresentationDisplayMode.Fullscreen;
        }
    }

    public void RestoreToPrimaryMonitor(bool windowed = true)
    {
        if (_window is not null)
        {
            _window.RestoreToPrimaryMonitor(windowed);
            _displayMode = _window.DisplayMode;
        }
        else if (windowed)
        {
            _displayMode = PresentationDisplayMode.Windowed;
        }
    }

    private void OnWindowDisplayModeChanged(object? sender, PresentationDisplayMode mode)
    {
        _displayMode = mode;
        DisplayModeChanged?.Invoke(this, mode);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is PresentationWindow pw)
        {
            pw.Cleanup();
        }
        _window = null;
        _displayMode = PresentationDisplayMode.Windowed;
        WindowClosed?.Invoke(this, EventArgs.Empty);
        DisplayModeChanged?.Invoke(this, PresentationDisplayMode.Windowed);
    }
}
