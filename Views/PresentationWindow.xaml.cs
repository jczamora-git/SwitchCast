using System.ComponentModel;
using System.Diagnostics;
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
    private readonly SubclassProc _subclassProc;
    private AppWindow? _appWindow;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private int _isCleanedUp;

    public PresentationWindow()
    {
        InitializeComponent();
        _subclassProc = OnSubclassProc;

        ViewModel = App.Current.Services.GetRequiredService<PresentationViewModel>();
        ViewModel.SetDispatcherQueue(DispatcherQueue);
        _settingsService = App.Current.Services.GetRequiredService<IApplicationSettingsService>();

        _settingsService.ThemeChanged += OnThemeChanged;
        Closed += OnWindowClosed;

        if (ViewModel.MediaPlayer is not null)
        {
            PresentationVideoPlayer.SetMediaPlayer(ViewModel.MediaPlayer);
        }

        _viewModelPropertyChangedHandler = (s, e) =>
        {
            if (e.PropertyName == nameof(PresentationViewModel.MediaPlayer))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (Volatile.Read(ref _isCleanedUp) == 0 && ViewModel.MediaPlayer is not null)
                    {
                        PresentationVideoPlayer.SetMediaPlayer(ViewModel.MediaPlayer);
                    }
                });
            }
        };
        ViewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        InitializeAppWindow();
        ApplyTheme(_settingsService.CurrentSettings.Theme);
    }

    public PresentationDisplayMode DisplayMode { get; private set; } = PresentationDisplayMode.Windowed;

    public event EventHandler<PresentationDisplayMode>? DisplayModeChanged;

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

            SetWindowSubclass(WindowHandle, _subclassProc, new UIntPtr(1003), IntPtr.Zero);
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

    /// <summary>
    /// Switches the Presentation Output window between Windowed and Fullscreen display mode.
    /// Preserves the same HWND, session state, and underlying capture/media stream.
    /// </summary>
    public void SetDisplayMode(PresentationDisplayMode mode)
    {
        if (DisplayMode == mode || _appWindow is null)
        {
            return;
        }

        DisplayMode = mode;

        if (mode == PresentationDisplayMode.Fullscreen)
        {
            // 1. Hide the custom XAML title bar completely
            AppTitleBar.Visibility = Visibility.Collapsed;
            TitleBarRow.Height = new GridLength(0);

            // 2. Clear title bar interop and switch to FullScreen presenter
            SetTitleBar(null);
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            // 1. Restore Default Overlapped presenter
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);

            // 2. Restore custom integrated title bar
            TitleBarRow.Height = new GridLength(38);
            AppTitleBar.Visibility = Visibility.Visible;
            SetTitleBar(AppTitleBar);
            UpdateTitleBarColors(_settingsService.CurrentSettings.Theme);
        }

        DisplayModeChanged?.Invoke(this, mode);
    }

    /// <summary>
    /// Toggles the current display mode of the presentation output window.
    /// </summary>
    public void ToggleDisplayMode()
    {
        SetDisplayMode(DisplayMode == PresentationDisplayMode.Fullscreen
            ? PresentationDisplayMode.Windowed
            : PresentationDisplayMode.Fullscreen);
    }

    /// <summary>
    /// Relocates the Presentation Output Window to the specified monitor, and optionally applies fullscreen mode.
    /// Preserves existing HWND, content layers, and capture/media states.
    /// </summary>
    public void MoveToMonitor(MonitorSource monitor, bool andFullscreen = false)
    {
        if (_appWindow is null)
        {
            return;
        }

        if (andFullscreen)
        {
            // Position on target monitor bounds first
            _appWindow.MoveAndResize(new RectInt32(monitor.Left, monitor.Top, monitor.Width, monitor.Height));
            SetDisplayMode(PresentationDisplayMode.Fullscreen);
        }
        else
        {
            SetDisplayMode(PresentationDisplayMode.Windowed);
            var (x, y, width, height) = WindowPositioningHelper.CalculateCenteredPosition(
                monitor.WorkAreaLeft,
                monitor.WorkAreaTop,
                monitor.WorkAreaWidth,
                monitor.WorkAreaHeight,
                Math.Min(1280, monitor.WorkAreaWidth),
                Math.Min(720, monitor.WorkAreaHeight));

            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
    }

    /// <summary>
    /// Restores the presentation output window to the primary monitor work area in windowed mode.
    /// </summary>
    public void RestoreToPrimaryMonitor(bool windowed = true)
    {
        if (_appWindow is null)
        {
            return;
        }

        if (windowed)
        {
            SetDisplayMode(PresentationDisplayMode.Windowed);
        }

        CenterPresentationWindow();
    }

    private void OnRootGridKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && DisplayMode == PresentationDisplayMode.Fullscreen)
        {
            SetDisplayMode(PresentationDisplayMode.Windowed);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Idempotently tears down XAML bindings, detaches native MediaPlayer and Image sources,
    /// and unhooks event subscriptions to prevent AccessViolationExceptions in Microsoft.WinUI.dll during window destruction.
    /// </summary>
    public void Cleanup()
    {
        if (Interlocked.Exchange(ref _isCleanedUp, 1) != 0)
        {
            return;
        }

        try
        {
            Bindings.StopTracking();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationWindow] Error stopping bindings: {ex.Message}");
        }

        if (_viewModelPropertyChangedHandler is not null)
        {
            ViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
            _viewModelPropertyChangedHandler = null;
        }

        try
        {
            PresentationVideoPlayer.SetMediaPlayer(null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationWindow] Error detaching MediaPlayer: {ex.Message}");
        }

        try
        {
            PresentationOutputImage.Source = null;
            PresentationDirectImage.Source = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationWindow] Error detaching image sources: {ex.Message}");
        }

        _settingsService.ThemeChanged -= OnThemeChanged;
        ViewModel.SetDispatcherQueue(null);
        ViewModel.Dispose();

        if (WindowHandle != IntPtr.Zero)
        {
            if (IsWindow(WindowHandle))
            {
                RemoveWindowSubclass(WindowHandle, _subclassProc, new UIntPtr(1003));
            }
            WindowHandle = IntPtr.Zero;
        }
        _appWindow = null;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Services.Diagnostics.SwitchCastDiagnostics.LogInfo(
            nameof(PresentationWindow),
            $"Closed | HWND=0x{WindowHandle:X} | PID={Environment.ProcessId}");

        Cleanup();
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

    private const uint WM_SETCURSOR = 0x0020;
    private const int HTCLIENT = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    private IntPtr OnSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_SETCURSOR)
        {
            short hitTest = (short)(lParam.ToInt64() & 0xFFFF);
            if (hitTest == HTCLIENT && !_settingsService.CurrentSettings.ShowPresentationCursor)
            {
                SetCursor(IntPtr.Zero);
                return (IntPtr)1;
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    #endregion
}
