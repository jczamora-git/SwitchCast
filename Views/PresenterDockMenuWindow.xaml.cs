using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Lightweight borderless popup window hosted externally to the Presenter Dock for unconstrained dropdown menus.
/// </summary>
public sealed partial class PresenterDockMenuWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int GWL_EXSTYLE = -20;
    private const int GWLP_HWNDPARENT = -8;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    private readonly PresenterDockViewModel _viewModel;
    private AppWindow? _appWindow;
    private bool _isClosing;

    public PresenterDockMenuWindow(PresenterDockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeAppWindow();
        Activated += OnWindowActivated;
    }

    public PresenterDockViewModel ViewModel => _viewModel;

    public IntPtr WindowHandle { get; private set; }

    public PresenterDockMenuType CurrentMenuType { get; private set; }

    private void InitializeAppWindow()
    {
        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "SwitchCast Presenter Dock Menu";

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            }

            // Apply ToolWindow style so it does not appear in Alt+Tab or taskbar
            var exStyle = GetWindowLongPtr(WindowHandle, GWL_EXSTYLE);
            SetWindowLongPtr(WindowHandle, GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | WS_EX_TOOLWINDOW));
        }
    }

    /// <summary>
    /// Displays and positions the specified dropdown menu anchored to a UI element on the dock.
    /// </summary>
    public void ShowMenu(PresenterDockMenuType menuType, FrameworkElement anchorElement, IntPtr ownerHwnd)
    {
        CurrentMenuType = menuType;

        // Set owner window
        if (ownerHwnd != IntPtr.Zero)
        {
            SetWindowLongPtr(WindowHandle, GWLP_HWNDPARENT, ownerHwnd);
        }

        // Hide all views first
        SourcesMenuPanel.Visibility = Visibility.Collapsed;
        ModeMenuPanel.Visibility = Visibility.Collapsed;
        MoreMenuPanel.Visibility = Visibility.Collapsed;

        double desiredWidthDip;
        double desiredHeightDip;

        switch (menuType)
        {
            case PresenterDockMenuType.QueuedSources:
                SourcesMenuPanel.Visibility = Visibility.Visible;
                var sources = _viewModel.SelectedSources;
                SourceCountText.Text = $"({sources.Count})";
                SourcesListView.ItemsSource = sources;

                if (sources.Count == 0)
                {
                    EmptySourcesPanel.Visibility = Visibility.Visible;
                    SourcesListView.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptySourcesPanel.Visibility = Visibility.Collapsed;
                    SourcesListView.Visibility = Visibility.Visible;
                }

                desiredWidthDip = 290.0;
                desiredHeightDip = sources.Count == 0
                    ? 110.0
                    : Math.Min(310.0, 48.0 + (sources.Count * 42.0));
                break;

            case PresenterDockMenuType.SwitchMode:
                ModeMenuPanel.Visibility = Visibility.Visible;
                CheckActiveAndLive.Visibility = _viewModel.IsModeActiveAndLive ? Visibility.Visible : Visibility.Collapsed;
                CheckActiveOnly.Visibility = _viewModel.IsModeActiveOnly ? Visibility.Visible : Visibility.Collapsed;
                CheckLiveOnly.Visibility = _viewModel.IsModeLiveOnly ? Visibility.Visible : Visibility.Collapsed;
                desiredWidthDip = 235.0;
                desiredHeightDip = 155.0;
                break;

            case PresenterDockMenuType.MoreOptions:
            default:
                MoreMenuPanel.Visibility = Visibility.Visible;
                StopPresentationButton.Visibility = _viewModel.IsPresenting ? Visibility.Visible : Visibility.Collapsed;
                StopDivider.Visibility = _viewModel.IsPresenting ? Visibility.Visible : Visibility.Collapsed;
                desiredWidthDip = 210.0;
                desiredHeightDip = _viewModel.IsPresenting ? 135.0 : 92.0;
                break;
        }

        PositionAndShow(anchorElement, ownerHwnd, desiredWidthDip, desiredHeightDip);
    }

    private void PositionAndShow(FrameworkElement anchorElement, IntPtr ownerHwnd, double desiredWidthDip, double desiredHeightDip)
    {
        if (_appWindow is null)
        {
            return;
        }

        uint dpi = GetDpiForWindow(ownerHwnd != IntPtr.Zero ? ownerHwnd : WindowHandle);
        if (dpi == 0)
        {
            dpi = 96;
        }
        double scale = dpi / 96.0;

        // Calculate screen coordinates of anchor element
        var transform = anchorElement.TransformToVisual(null);
        var originDips = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        var pt = new POINT
        {
            X = (int)Math.Round(originDips.X * scale),
            Y = (int)Math.Round(originDips.Y * scale)
        };

        if (ownerHwnd != IntPtr.Zero)
        {
            ClientToScreen(ownerHwnd, ref pt);
        }

        int anchorWidth = (int)Math.Round(anchorElement.ActualWidth * scale);
        int anchorHeight = (int)Math.Round(anchorElement.ActualHeight * scale);
        int menuWidthPx = (int)Math.Round(desiredWidthDip * scale);
        int menuHeightPx = (int)Math.Round(desiredHeightDip * scale);

        // Get monitor work area
        var hMonitor = MonitorFromWindow(ownerHwnd != IntPtr.Zero ? ownerHwnd : WindowHandle, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref info);

        int spacingPx = (int)Math.Round(4.0 * scale);
        int marginPx = (int)Math.Round(8.0 * scale);

        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            pt.X,
            pt.Y,
            anchorWidth,
            anchorHeight,
            menuWidthPx,
            menuHeightPx,
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right,
            info.rcWork.Bottom,
            spacingPx,
            marginPx);

        _appWindow.MoveAndResize(new RectInt32(targetX, targetY, menuWidthPx, menuHeightPx));
        _appWindow.Show(true);
        SetWindowPos(WindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    private void OnSourceListItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CaptureSource source)
        {
            _ = _viewModel.SwitchSourceCommand.ExecuteAsync(source);
            CloseMenu();
        }
    }

    private void OnModeActiveAndLiveClicked(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.ActiveAndLive);
        CloseMenu();
    }

    private void OnModeActiveOnlyClicked(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.ActiveOnly);
        CloseMenu();
    }

    private void OnModeLiveOnlyClicked(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.LiveOnly);
        CloseMenu();
    }

    private void OnStopPresentationClicked(object sender, RoutedEventArgs e)
    {
        _ = _viewModel.StopPresentationCommand.ExecuteAsync(null);
        CloseMenu();
    }

    private void OnShowDashboardClicked(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _viewModel.ShowDashboardCommand.Execute(null);
    }

    private void OnShowOutputWindowClicked(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _viewModel.ShowOutputWindowCommand.Execute(null);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            CloseMenu();
        }
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            CloseMenu();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Safely dismisses and closes the popup menu window.
    /// </summary>
    public void CloseMenu()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Activated -= OnWindowActivated;

        if (WindowHandle != IntPtr.Zero)
        {
            // Detach owner before closing so Windows does not automatically reactivate the owner dock window
            SetWindowLongPtr(WindowHandle, GWLP_HWNDPARENT, IntPtr.Zero);
        }

        try
        {
            Close();
        }
        catch
        {
            // Ignore close exceptions during teardown
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
