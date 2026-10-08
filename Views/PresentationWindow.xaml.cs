using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SwitchCast.ViewModels;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Dedicated, shareable Presentation Output Window isolated from the Control Dashboard.
/// </summary>
public sealed partial class PresentationWindow : Window
{
    private AppWindow? _appWindow;

    public PresentationWindow()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<PresentationViewModel>();

        InitializeAppWindow();
    }

    public PresentationViewModel ViewModel { get; }

    public IntPtr WindowHandle { get; private set; }

    private void InitializeAppWindow()
    {
        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "SwitchCast Presentation Output";
            _appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 720));
        }
    }
}
