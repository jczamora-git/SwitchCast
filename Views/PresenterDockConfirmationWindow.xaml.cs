using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace SwitchCast.Views;

/// <summary>
/// Dedicated, centered modal confirmation window for Presenter Dock closure during active presentation sessions.
/// Completely decoupled from the compact Floating Dock surface to prevent input freezing, z-order, and clipping bugs.
/// </summary>
public sealed partial class PresenterDockConfirmationWindow : Window
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int GWL_EXSTYLE = -20;
    private const int GWLP_HWNDPARENT = -8;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;

    private static bool _isConfirmationOpen;

    private AppWindow? _appWindow;
    private TaskCompletionSource<bool>? _tcs;
    private IntPtr _ownerHwnd;

    public PresenterDockConfirmationWindow()
    {
        InitializeComponent();
        InitializeAppWindow();

        RootGrid.KeyDown += OnRootGridKeyDown;
        Activated += OnWindowActivated;
    }

    public IntPtr WindowHandle { get; private set; }

    private void InitializeAppWindow()
    {
        WindowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = "Stop presentation and close Dock?";

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            }

            // Apply ToolWindow style so it does not pollute Alt+Tab or taskbar
            var exStyle = GetWindowLongPtr(WindowHandle, GWL_EXSTYLE);
            SetWindowLongPtr(WindowHandle, GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | WS_EX_TOOLWINDOW));

            _appWindow.Closing += OnAppWindowClosing;
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            CancelButton.Focus(FocusState.Programmatic);
        }
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            Dismiss(confirmed: false);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Dismiss(confirmed: false);
    }

    private void OnStopCloseClick(object sender, RoutedEventArgs e)
    {
        Dismiss(confirmed: true);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Treat window closing via Alt+F4 as cancel
        _tcs?.TrySetResult(false);
    }

    private void Dismiss(bool confirmed)
    {
        _tcs?.TrySetResult(confirmed);
        try
        {
            Close();
        }
        catch
        {
            // Ignore close issues
        }
    }

    /// <summary>
    /// Displays a centered, topmost confirmation modal above the specified owner window.
    /// Returns true if the user confirmed Stop &amp; Close, or false if cancelled.
    /// </summary>
    public static async Task<bool> ShowConfirmationAsync(IntPtr ownerHwnd)
    {
        if (_isConfirmationOpen)
        {
            return false;
        }

        _isConfirmationOpen = true;
        PresenterDockConfirmationWindow? window = null;

        try
        {
            window = new PresenterDockConfirmationWindow();
            window._ownerHwnd = ownerHwnd;

            if (ownerHwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(window.WindowHandle, GWLP_HWNDPARENT, ownerHwnd);
            }

            window.PositionWindow(ownerHwnd);

            // Temporarily disable owner window to prevent input conflicts and duplicate clicks
            if (ownerHwnd != IntPtr.Zero)
            {
                EnableWindow(ownerHwnd, false);
            }

            var tcs = new TaskCompletionSource<bool>();
            window._tcs = tcs;

            window.Activate();

            bool result = await tcs.Task;
            return result;
        }
        catch (Exception ex)
        {
            Services.Diagnostics.SwitchCastDiagnostics.LogException(
                nameof(PresenterDockConfirmationWindow),
                nameof(ShowConfirmationAsync),
                ex);
            return false;
        }
        finally
        {
            if (ownerHwnd != IntPtr.Zero)
            {
                EnableWindow(ownerHwnd, true);
                SetForegroundWindow(ownerHwnd);
            }

            _isConfirmationOpen = false;
        }
    }

    private void PositionWindow(IntPtr ownerHwnd)
    {
        if (_appWindow is null)
        {
            return;
        }

        uint dpi = GetDpiForWindow(WindowHandle);
        if (dpi == 0) dpi = 96;
        double scale = dpi / 96.0;

        double widthDip = 480.0;
        double heightDip = 210.0;

        int pixelWidth = (int)Math.Round(widthDip * scale);
        int pixelHeight = (int)Math.Round(heightDip * scale);

        // Center on the monitor containing owner window (or primary monitor)
        IntPtr hTarget = ownerHwnd != IntPtr.Zero ? ownerHwnd : WindowHandle;
        IntPtr hMonitor = MonitorFromWindow(hTarget, MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfo(hMonitor, ref info))
        {
            int workAreaWidth = info.rcWork.Right - info.rcWork.Left;
            int workAreaHeight = info.rcWork.Bottom - info.rcWork.Top;

            int posX = info.rcWork.Left + Math.Max(0, (workAreaWidth - pixelWidth) / 2);
            int posY = info.rcWork.Top + Math.Max(0, (workAreaHeight - pixelHeight) / 2);

            _appWindow.MoveAndResize(new RectInt32(posX, posY, pixelWidth, pixelHeight));
        }
        else
        {
            _appWindow.Resize(new SizeInt32(pixelWidth, pixelHeight));
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    #endregion
}
