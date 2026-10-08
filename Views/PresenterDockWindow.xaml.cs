using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SwitchCast.ViewModels;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Compact floating presenter companion dock window designed for quick source switching during presentations.
/// </summary>
public sealed partial class PresenterDockWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private AppWindow? _appWindow;

    public PresenterDockWindow()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<PresenterDockViewModel>();

        InitializeAppWindow();
    }

    public PresenterDockViewModel ViewModel { get; }

    public IntPtr WindowHandle { get; private set; }

    private void InitializeAppWindow()
    {
        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "SwitchCast Presenter Dock";
            _appWindow.Resize(new Windows.Graphics.SizeInt32(660, 68));

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            SetWindowPos(WindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
