using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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

    private const int DragThresholdSquared = 25; // 5 physical pixels squared threshold
    private bool _isPointerDown;
    private POINT _dragStartPoint;

    private AppWindow? _appWindow;
    private PresenterDockMenuWindow? _activeMenuWindow;
    private long _lastMenuClosedTicks;

    public PresenterDockWindow()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<PresenterDockViewModel>();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;

        InitializeAppWindow();
        SetupPointerHandlers();
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

    private void SetupPointerHandlers()
    {
        DockCardBorder.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnDockPointerPressed), handledEventsToo: true);
        DockCardBorder.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnDockPointerMoved), handledEventsToo: true);
        DockCardBorder.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnDockPointerReleased), handledEventsToo: true);
        DockCardBorder.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnDockPointerCanceled), handledEventsToo: true);
        DockCardBorder.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnDockPointerCaptureLost), handledEventsToo: true);
    }

    private void ApplyWindowSizingAndPosition(bool isCompact, bool initialCenter = false)
    {
        CloseActiveMenu();

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

    private void OnDockPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var ptr = e.GetCurrentPoint(null);
        if (!ptr.Properties.IsLeftButtonPressed)
        {
            _isPointerDown = false;
            return;
        }

        if (!GetCursorPos(out _dragStartPoint))
        {
            return;
        }

        if (!IsInteractiveControl(e.OriginalSource as DependencyObject))
        {
            // Direct press on non-interactive toolbar surfaces (background, padding, status badge, dividers, text labels)
            _isPointerDown = false;
            CloseActiveMenu();
            ReleaseCapture();
            SendMessage(WindowHandle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            e.Handled = true;
            return;
        }

        // Press on interactive control (buttons, dropdowns): track movement threshold to distinguish click vs drag
        _isPointerDown = true;
    }

    private void OnDockPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown)
        {
            return;
        }

        var ptr = e.GetCurrentPoint(null);
        if (!ptr.Properties.IsLeftButtonPressed)
        {
            _isPointerDown = false;
            return;
        }

        if (GetCursorPos(out POINT currentPoint))
        {
            int dx = currentPoint.X - _dragStartPoint.X;
            int dy = currentPoint.Y - _dragStartPoint.Y;
            if ((dx * dx + dy * dy) >= DragThresholdSquared)
            {
                _isPointerDown = false;
                CloseActiveMenu();
                ReleaseCapture();
                SendMessage(WindowHandle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                e.Handled = true;
            }
        }
    }

    private void OnDockPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
    }

    private void OnDockPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
    }

    private void OnDockPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
    }

    private static bool IsInteractiveControl(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is ButtonBase ||
                element is ComboBox ||
                element is TextBox ||
                element is RichEditBox ||
                element is PasswordBox ||
                element is Slider ||
                element is ToggleSwitch ||
                element is ListViewItem ||
                element is GridViewItem ||
                element is MenuFlyoutItem ||
                element is MenuFlyoutSubItem ||
                element is FlyoutPresenter ||
                element is MenuFlyoutPresenter ||
                element is ScrollBar ||
                element is Thumb)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void OnSourceSelectorClicked(object sender, RoutedEventArgs e)
    {
        ToggleMenu(PresenterDockMenuType.QueuedSources, sender as FrameworkElement);
    }

    private void OnModeSelectorClicked(object sender, RoutedEventArgs e)
    {
        ToggleMenu(PresenterDockMenuType.SwitchMode, sender as FrameworkElement);
    }

    private void OnMoreOptionsClicked(object sender, RoutedEventArgs e)
    {
        ToggleMenu(PresenterDockMenuType.MoreOptions, sender as FrameworkElement);
    }

    private void ToggleMenu(PresenterDockMenuType type, FrameworkElement? anchor)
    {
        if (anchor is null)
        {
            return;
        }

        long now = DateTime.UtcNow.Ticks;
        if (now - _lastMenuClosedTicks < TimeSpan.FromMilliseconds(200).Ticks &&
            _activeMenuWindow?.CurrentMenuType == type)
        {
            CloseActiveMenu();
            return;
        }

        if (_activeMenuWindow is not null)
        {
            bool isSameType = _activeMenuWindow.CurrentMenuType == type;
            CloseActiveMenu();
            if (isSameType)
            {
                return;
            }
        }

        var menu = new PresenterDockMenuWindow(ViewModel);
        _activeMenuWindow = menu;
        menu.Closed += (s, _) =>
        {
            _lastMenuClosedTicks = DateTime.UtcNow.Ticks;
            if (ReferenceEquals(_activeMenuWindow, s))
            {
                _activeMenuWindow = null;
            }
        };

        menu.ShowMenu(type, anchor, WindowHandle);
    }

    public void CloseActiveMenu()
    {
        if (_activeMenuWindow is not null)
        {
            var menu = _activeMenuWindow;
            _activeMenuWindow = null;
            _lastMenuClosedTicks = DateTime.UtcNow.Ticks;
            menu.CloseMenu();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PresenterDockViewModel.IsCompactMode))
        {
            DispatcherQueue.TryEnqueue(() => ApplyWindowSizingAndPosition(ViewModel.IsCompactMode, initialCenter: false));
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        CloseActiveMenu();
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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
