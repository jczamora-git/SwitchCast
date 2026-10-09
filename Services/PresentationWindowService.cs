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
            target.Closed -= OnWindowClosed;
            target.DisplayModeChanged -= OnWindowDisplayModeChanged;
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

    private void OnWindowDisplayModeChanged(object? sender, PresentationDisplayMode mode)
    {
        _displayMode = mode;
        DisplayModeChanged?.Invoke(this, mode);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        _displayMode = PresentationDisplayMode.Windowed;
        WindowClosed?.Invoke(this, EventArgs.Empty);
        DisplayModeChanged?.Invoke(this, PresentationDisplayMode.Windowed);
    }
}
