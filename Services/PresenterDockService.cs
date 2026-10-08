using Microsoft.UI.Xaml;
using SwitchCast.Views;

namespace SwitchCast.Services;

/// <summary>
/// Service managing the single instance, lifecycle, and visibility of the Floating Presenter Dock Window.
/// </summary>
public sealed class PresenterDockService : IPresenterDockService
{
    private PresenterDockWindow? _window;

    public bool IsDockOpen => _window is not null;

    public IntPtr DockWindowHandle => _window?.WindowHandle ?? IntPtr.Zero;

    public event EventHandler? DockOpened;
    public event EventHandler? DockClosed;
    public event EventHandler? RequestShowDashboard;

    public void ShowDashboard()
    {
        RequestShowDashboard?.Invoke(this, EventArgs.Empty);
    }

    public void ShowDock()
    {
        if (_window is null)
        {
            _window = new PresenterDockWindow();
            _window.Closed += OnWindowClosed;
            _window.Activate();
            DockOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _window.Activate();
            DockOpened?.Invoke(this, EventArgs.Empty);
        }
    }

    public void CloseDock()
    {
        if (_window is not null)
        {
            var target = _window;
            _window = null;
            target.Closed -= OnWindowClosed;
            target.Close();
            DockClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleDock()
    {
        if (IsDockOpen)
        {
            CloseDock();
        }
        else
        {
            ShowDock();
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _window = null;
        DockClosed?.Invoke(this, EventArgs.Empty);
    }
}
