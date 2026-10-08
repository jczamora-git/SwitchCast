using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Dedicated, shareable Presentation Output Window isolated from the Control Dashboard with a custom integrated title bar.
/// </summary>
public sealed partial class PresentationWindow : Window
{
    private readonly IApplicationSettingsService _settingsService;
    private AppWindow? _appWindow;

    public PresentationWindow()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetRequiredService<PresentationViewModel>();
        _settingsService = App.Current.Services.GetRequiredService<IApplicationSettingsService>();

        _settingsService.ThemeChanged += OnThemeChanged;
        Closed += OnWindowClosed;

        InitializeAppWindow();
        ApplyTheme(_settingsService.CurrentSettings.Theme);
    }

    public PresentationViewModel ViewModel { get; }

    public IntPtr WindowHandle { get; private set; }

    private void InitializeAppWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "SwitchCast Presentation Output";

            uint dpi = GetDpiForWindow(WindowHandle);
            if (dpi == 0)
            {
                dpi = 96;
            }
            double scale = dpi / 96.0;

            _appWindow.Resize(new SizeInt32((int)Math.Round(1280 * scale), (int)Math.Round(720 * scale)));

            UpdateTitleBarColors(_settingsService.CurrentSettings.Theme);
        }
    }

    private void OnThemeChanged(object? sender, ApplicationThemeOption newTheme)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTheme(newTheme));
    }

    private void ApplyTheme(ApplicationThemeOption theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            ApplicationThemeOption.Light => ElementTheme.Light,
            ApplicationThemeOption.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        UpdateTitleBarColors(theme);
    }

    private void UpdateTitleBarColors(ApplicationThemeOption theme)
    {
        if (_appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        var isDark = theme == ApplicationThemeOption.Dark ||
                     (theme == ApplicationThemeOption.System && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 240);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(35, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(55, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
        }
        else
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 30);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(45, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 160, 160, 160);
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _settingsService.ThemeChanged -= OnThemeChanged;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);
}
