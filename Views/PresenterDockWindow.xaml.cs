using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Input;
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
/// Minimal permanent floating presenter companion dock window designed for instant source switching and video playback control.
/// </summary>
public sealed partial class PresenterDockWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private AppWindow? _appWindow;
    private InputNonClientPointerSource? _nonClientPointerSource;
    private PresenterDockMenuWindow? _activeMenuWindow;
    private long _lastMenuClosedTicks;

    public PresenterDockWindow()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<PresenterDockViewModel>();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;

        InitializeAppWindow();

        DockCardBorder.Loaded += OnDockCardLoaded;
        DockCardBorder.SizeChanged += OnDockCardSizeChanged;
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

            ApplyWindowSizingAndPosition(ViewModel.IsActiveSourceVideo, initialCenter: true);

            SetWindowPos(WindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

            try
            {
                _nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(windowId);
            }
            catch
            {
                // NonClient pointer source fallback handled if not supported
            }
        }
    }

    private void OnDockCardLoaded(object sender, RoutedEventArgs e)
    {
        UpdateNonClientRegions();
    }

    private void OnDockCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateNonClientRegions();
    }

    /// <summary>
    /// Dynamically defines genuine native Windows caption drag regions for non-interactive areas
    /// and ensures interactive controls receive normal client mouse input.
    /// </summary>
    private void UpdateNonClientRegions()
    {
        if (_nonClientPointerSource is null || _appWindow is null)
        {
            return;
        }

        try
        {
            uint dpi = GetDpiForWindow(WindowHandle);
            if (dpi == 0) dpi = 96;
            double scale = dpi / 96.0;

            int windowWidth = _appWindow.Size.Width;
            int windowHeight = _appWindow.Size.Height;

            if (windowWidth <= 0 || windowHeight <= 0)
            {
                return;
            }

            // 1. Set the entire dock surface as native caption (draggable)
            _nonClientPointerSource.SetRegionRects(
                NonClientRegionKind.Caption,
                [new RectInt32(0, 0, windowWidth, windowHeight)]);

            // 2. Discover all interactive elements and set them as Passthrough (interactive client regions)
            var passthroughRects = new List<RectInt32>();
            CollectInteractiveRects(DockCardBorder, passthroughRects, scale);

            if (passthroughRects.Count > 0)
            {
                _nonClientPointerSource.SetRegionRects(
                    NonClientRegionKind.Passthrough,
                    passthroughRects.ToArray());
            }
        }
        catch
        {
            // Defensive handling for non-client pointer configuration
        }
    }

    private void CollectInteractiveRects(DependencyObject parent, List<RectInt32> rects, double scale)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Visibility == Visibility.Visible)
            {
                if (IsDirectInteractiveControl(fe))
                {
                    try
                    {
                        var transform = fe.TransformToVisual(DockCardBorder);
                        var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, fe.ActualWidth, fe.ActualHeight));

                        if (bounds.Width > 0 && bounds.Height > 0)
                        {
                            int x = (int)Math.Floor(bounds.X * scale);
                            int y = (int)Math.Floor(bounds.Y * scale);
                            int w = (int)Math.Ceiling(bounds.Width * scale);
                            int h = (int)Math.Ceiling(bounds.Height * scale);

                            rects.Add(new RectInt32(x, y, w, h));
                        }
                    }
                    catch
                    {
                        // Ignore visual transform issues on unattached nodes
                    }
                }
                else
                {
                    CollectInteractiveRects(child, rects, scale);
                }
            }
        }
    }

    private static bool IsDirectInteractiveControl(FrameworkElement element)
    {
        return element is ButtonBase ||
               element is ComboBox ||
               element is Slider ||
               element is TextBox ||
               element is ToggleSwitch ||
               element is ListViewItem;
    }

    private void ApplyWindowSizingAndPosition(bool isVideoActive, bool initialCenter = false)
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

        // Baseline width: 680 DIPs (comfortable single row).
        // Height: 52 DIPs for standard sources, 86 DIPs when adaptive video row is on air.
        double widthDip = 680.0;
        double heightDip = isVideoActive ? 86.0 : 52.0;

        int pixelWidth = (int)Math.Round(widthDip * scale);
        int pixelHeight = (int)Math.Round(heightDip * scale);

        _appWindow.Resize(new SizeInt32(pixelWidth, pixelHeight));

        if (initialCenter)
        {
            CenterDockOnTopWorkArea(pixelWidth);
        }

        DispatcherQueue.TryEnqueue(UpdateNonClientRegions);
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

    private void OnTimelineSliderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider)
        {
            ViewModel.CompleteScrubbing(slider.Value);
        }
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
        if (e.PropertyName == nameof(PresenterDockViewModel.IsActiveSourceVideo))
        {
            DispatcherQueue.TryEnqueue(() => ApplyWindowSizingAndPosition(ViewModel.IsActiveSourceVideo, initialCenter: false));
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
