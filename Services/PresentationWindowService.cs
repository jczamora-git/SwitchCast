using Microsoft.UI.Xaml;
using SwitchCast.Views;

namespace SwitchCast.Services;

/// <summary>
/// Service managing the single instance and lifecycle of the Presentation Output Window.
/// </summary>
public sealed class PresentationWindowService : IPresentationWindowService
{
    private PresentationWindow? _window;

    public bool IsWindowOpen => _window is not null;

    public IntPtr WindowHandle => _window?.WindowHandle ?? IntPtr.Zero;

    public event EventHandler? WindowOpened;
    public event EventHandler? WindowClosed;

    public void ShowPresentationWindow()
    {
        if (_window is null)
        {
            _window = new PresentationWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();
            WindowOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _window.Activate();
            WindowOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClosePresentationWindow()
    {
        if (_window is not null)
        {
            var target = _window;
            _window = null;
            target.Closed -= OnWindowClosed;
            target.Close();
            WindowClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }
}
