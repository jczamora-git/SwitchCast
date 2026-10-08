using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SwitchCast.Models;
using SwitchCast.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Minimal single-row floating presenter companion dock window designed for instant source switching during live presentations.
/// </summary>
public sealed partial class PresenterDockWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private AppWindow? _appWindow;

    public PresenterDockWindow()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<PresenterDockViewModel>();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        InitializeAppWindow();
    }

    public PresenterDockViewModel ViewModel { get; }

    public IntPtr WindowHandle { get; private set; }

    /// <summary>
    /// Helper method for XAML compiled binding: maps boolean to Visibility.
    /// </summary>
    public static Visibility BoolToVis(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Helper method for XAML compiled binding: maps inverted boolean to Visibility.
    /// </summary>
    public static Visibility InvertBoolToVis(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void InitializeAppWindow()
    {
        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "SwitchCast Presenter Dock";

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            }

            ApplyWindowSizingAndPosition(ViewModel.IsCompactMode, initialCenter: true);

            SetWindowPos(WindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
    }

    private void ApplyWindowSizingAndPosition(bool isCompact, bool initialCenter = false)
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

        // Establish minimal single-row target DIP dimensions
        double widthDip = isCompact ? 460.0 : 660.0;
        double heightDip = isCompact ? 46.0 : 52.0;

        int pixelWidth = (int)Math.Round(widthDip * scale);
        int pixelHeight = (int)Math.Round(heightDip * scale);

        _appWindow.Resize(new SizeInt32(pixelWidth, pixelHeight));

        if (initialCenter)
        {
            CenterDockOnTopWorkArea(pixelWidth);
        }
    }

    private void CenterDockOnTopWorkArea(int pixelWidth)
    {
        if (_appWindow is null)
        {
            return;
        }

        var hMonitor = MonitorFromWindow(WindowHandle, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (GetMonitorInfo(hMonitor, ref info))
        {
            int workWidth = info.rcWork.Right - info.rcWork.Left;
            int targetX = info.rcWork.Left + (workWidth - pixelWidth) / 2;

            uint dpi = GetDpiForWindow(WindowHandle);
            if (dpi == 0)
            {
                dpi = 96;
            }
            double scale = dpi / 96.0;
            int targetY = info.rcWork.Top + (int)Math.Round(24.0 * scale);

            _appWindow.Move(new PointInt32(targetX, targetY));
        }
    }

    private void OnDragRegionPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Hand off dragging directly to the Windows window manager
        ReleaseCapture();
        SendMessage(WindowHandle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    private void OnSourceItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CaptureSource source)
        {
            _ = ViewModel.SwitchSourceCommand.ExecuteAsync(source);

            // Close the source flyout
            SourceDropDownButton?.Flyout?.Hide();
        }
    }

    private void OnModeActiveAndLiveClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.ActiveAndLive);
        ExpandedModeDropDownButton?.Flyout?.Hide();
        CompactModeDropDownButton?.Flyout?.Hide();
    }

    private void OnModeActiveOnlyClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.ActiveOnly);
        ExpandedModeDropDownButton?.Flyout?.Hide();
        CompactModeDropDownButton?.Flyout?.Hide();
    }

    private void OnModeLiveOnlyClicked(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.LiveOnly);
        ExpandedModeDropDownButton?.Flyout?.Hide();
        CompactModeDropDownButton?.Flyout?.Hide();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PresenterDockViewModel.IsCompactMode))
        {
            DispatcherQueue.TryEnqueue(() => ApplyWindowSizingAndPosition(ViewModel.IsCompactMode, initialCenter: false));
        }
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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
