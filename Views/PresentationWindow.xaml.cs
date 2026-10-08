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

            ApplyAppIcon();
            CenterPresentationWindow();

            UpdateTitleBarColors(_settingsService.CurrentSettings.Theme);
        }
    }

    private void ApplyAppIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            string primaryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "SwitchCast.ico");
            if (System.IO.File.Exists(primaryPath))
            {
                _appWindow.SetIcon(primaryPath);
                return;
            }

            string fallbackPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Assets", "SwitchCast.ico");
            if (System.IO.File.Exists(fallbackPath))
            {
                _appWindow.SetIcon(fallbackPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PresentationWindow] ApplyAppIcon failed: {ex.Message}");
        }
    }

    private void CenterPresentationWindow()
    {
        if (_appWindow is null)
        {
            return;
        }

        uint dpi = GetDpiForWindow(WindowHandle);
        if (dpi == 0)
        {
            dpi = 96;
        }
        double scale = dpi / 96.0;

        // Determine target monitor: prefer monitor where MainWindow is currently located
        IntPtr targetHwnd = WindowHandle;
        if (App.Current.MainWindow is MainWindow mw && mw.WindowHandle != IntPtr.Zero)
        {
            targetHwnd = mw.WindowHandle;
        }

        var hMonitor = MonitorFromWindow(targetHwnd, MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (GetMonitorInfo(hMonitor, ref info))
        {
            int workAreaWidth = info.rcWork.Right - info.rcWork.Left;
            int workAreaHeight = info.rcWork.Bottom - info.rcWork.Top;

            var (x, y, width, height) = WindowPositioningHelper.CalculateDpiScaledCenteredPosition(
                info.rcWork.Left,
                info.rcWork.Top,
                workAreaWidth,
                workAreaHeight,
                1280.0,
                720.0,
                scale);

            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        else
        {
            int pixelWidth = (int)Math.Round(1280.0 * scale);
            int pixelHeight = (int)Math.Round(720.0 * scale);
            _appWindow.Resize(new SizeInt32(pixelWidth, pixelHeight));
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

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}
