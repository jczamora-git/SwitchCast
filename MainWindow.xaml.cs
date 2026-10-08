using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using SwitchCast.Views;
using Windows.Graphics;
using WinRT.Interop;

namespace SwitchCast;

/// <summary>
/// Primary desktop application shell hosting navigation and view transitions.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly INavigationService _navigationService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly IPresentationCoordinator _presentationCoordinator;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Current.Services.GetRequiredService<MainViewModel>();
        _navigationService = App.Current.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Current.Services.GetRequiredService<IApplicationSettingsService>();
        _lifecycleService = App.Current.Services.GetRequiredService<IApplicationLifecycleService>();
        _presentationCoordinator = App.Current.Services.GetRequiredService<IPresentationCoordinator>();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _navigationService.Navigated += OnNavigationServiceNavigated;

        InitializeAppWindow();
        ApplyTheme(_viewModel.CurrentTheme);
    }

    public MainViewModel ViewModel => _viewModel;

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
            _appWindow.Title = _viewModel.WindowTitle;

            ApplyAppIcon();
            ApplyStartupWindowPlacement();
            _appWindow.Closing += OnAppWindowClosing;

            UpdateTitleBarColors(_viewModel.CurrentTheme);
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
            Debug.WriteLine($"[MainWindow] ApplyAppIcon failed: {ex.Message}");
        }
    }

    private void ApplyStartupWindowPlacement()
    {
        if (_appWindow is null)
        {
            return;
        }

        var settings = _settingsService.CurrentSettings;
        uint dpi = GetDpiForWindow(WindowHandle);
        if (dpi == 0)
        {
            dpi = 96;
        }
        double scale = dpi / 96.0;

        // Establish DPI-scaled window dimensions (minimum 800x600 DIPs)
        double widthDip = Math.Max(800.0, settings.WindowWidth);
        double heightDip = Math.Max(600.0, settings.WindowHeight);

        int pixelWidth = (int)Math.Round(widthDip * scale);
        int pixelHeight = (int)Math.Round(heightDip * scale);

        bool placementApplied = false;

        // Check if user has enabled remembering position and valid saved coordinates exist
        if (settings.RememberWindowDimensions && settings.RememberWindowPosition &&
            settings.WindowPositionX.HasValue && settings.WindowPositionY.HasValue)
        {
            int savedX = settings.WindowPositionX.Value;
            int savedY = settings.WindowPositionY.Value;

            // Verify if saved coordinates intersect with an active monitor's work area
            var pt = new POINT { X = savedX, Y = savedY };
            var hSavedMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONULL);

            if (hSavedMonitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hSavedMonitor, ref info))
                {
                    // Clamp coordinates so the window is fully visible in the monitor work area
                    int maxX = Math.Max(info.rcWork.Left, info.rcWork.Right - pixelWidth);
                    int maxY = Math.Max(info.rcWork.Top, info.rcWork.Bottom - pixelHeight);
                    int clampedX = Math.Clamp(savedX, info.rcWork.Left, maxX);
                    int clampedY = Math.Clamp(savedY, info.rcWork.Top, maxY);

                    _appWindow.MoveAndResize(new RectInt32(clampedX, clampedY, pixelWidth, pixelHeight));
                    placementApplied = true;
                }
            }
        }

        if (!placementApplied)
        {
            // First launch, remember position disabled, or saved monitor disconnected: Center within active monitor work area
            var hMonitor = MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTOPRIMARY);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

            if (GetMonitorInfo(hMonitor, ref info))
            {
                int workAreaWidth = info.rcWork.Right - info.rcWork.Left;
                int workAreaHeight = info.rcWork.Bottom - info.rcWork.Top;

                var (centerX, centerY, width, height) = WindowPositioningHelper.CalculateCenteredPosition(
                    info.rcWork.Left,
                    info.rcWork.Top,
                    workAreaWidth,
                    workAreaHeight,
                    pixelWidth,
                    pixelHeight);

                _appWindow.MoveAndResize(new RectInt32(centerX, centerY, width, height));
            }
            else
            {
                _appWindow.Resize(new Windows.Graphics.SizeInt32(pixelWidth, pixelHeight));
            }
        }
    }

    private void OnNavViewLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Initialize(ContentFrame);
        _navigationService.NavigateTo<DashboardPage>();
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void OnNavViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _navigationService.NavigateTo<SettingsPage>();
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    _navigationService.NavigateTo<DashboardPage>();
                    break;
                case "Sources":
                    _navigationService.NavigateTo<SourcesPage>();
                    break;
            }
        }
    }

    private void OnNavigationServiceNavigated(object? sender, Type pageType)
    {
        if (pageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
        }
        else if (pageType == typeof(DashboardPage))
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
        else if (pageType == typeof(SourcesPage))
        {
            NavView.SelectedItem = NavView.MenuItems[1];
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentTheme))
        {
            ApplyTheme(_viewModel.CurrentTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.WindowTitle) && _appWindow is not null)
        {
            _appWindow.Title = _viewModel.WindowTitle;
        }
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

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_lifecycleService.IsShutdownApproved)
        {
            // Shutdown has already been confirmed and authorized; allow closure to complete.
            return;
        }

        // Synchronously intercept and cancel window destruction to prompt the user.
        args.Cancel = true;

        if (_lifecycleService.IsExitConfirmationOpen)
        {
            // Prevent duplicate dialog prompts if user rapidly clicks X or presses Alt+F4.
            return;
        }

        _lifecycleService.IsExitConfirmationOpen = true;

        try
        {
            if (Content?.XamlRoot is null)
            {
                // Fallback if visual root is not accessible
                _lifecycleService.ApproveShutdown();
                await _lifecycleService.ExecuteShutdownAsync();
                Close();
                return;
            }

            bool isPresenting = _presentationCoordinator.IsLive ||
                                _presentationCoordinator.IsPaused ||
                                _presentationCoordinator.IsBlackout;

            var dialog = new ContentDialog
            {
                Title = "Exit SwitchCast?",
                Content = isPresenting
                    ? "Your live presentation will stop, and all SwitchCast windows will close. Are you sure you want to exit?"
                    : "Are you sure you want to exit SwitchCast?",
                PrimaryButtonText = "Exit SwitchCast",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                _lifecycleService.ApproveShutdown();

                if (_appWindow is not null)
                {
                    uint dpi = GetDpiForWindow(WindowHandle);
                    if (dpi == 0)
                    {
                        dpi = 96;
                    }
                    double scale = dpi / 96.0;

                    var size = _appWindow.Size;
                    var pos = _appWindow.Position;

                    double widthDip = size.Width / scale;
                    double heightDip = size.Height / scale;

                    await _settingsService.SetWindowPlacementAsync(
                        widthDip,
                        heightDip,
                        pos.X,
                        pos.Y);
                }

                await _lifecycleService.ExecuteShutdownAsync();

                // Close MainWindow through the authorized exit path
                Close();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] Exit confirmation dialog error: {ex.Message}");
        }
        finally
        {
            _lifecycleService.IsExitConfirmationOpen = false;
        }
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

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

    private const uint MONITOR_DEFAULTTONULL = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}
