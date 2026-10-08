using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.ViewModels;

namespace SwitchCast;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    public MainWindow? MainWindow => _mainWindow as MainWindow;

    /// <summary>
    /// Activates and brings the primary application window to the foreground, restoring if minimized.
    /// </summary>
    public void ActivateMainWindow()
    {
        if (_mainWindow is MainWindow mw && mw.WindowHandle != IntPtr.Zero)
        {
            var activationService = Services.GetService<IWindowActivationService>();
            if (activationService is not null)
            {
                activationService.ActivateWindow(mw.WindowHandle);
                return;
            }
        }

        _mainWindow?.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core & Application Services
        services.AddSingleton<IPresentationStateService, PresentationStateService>();
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IWindowDiscoveryService, Win32WindowDiscoveryService>();
        services.AddSingleton<IMonitorDiscoveryService, Win32MonitorDiscoveryService>();
        services.AddSingleton<IWindowIconService, Win32WindowIconService>();

        // Native Capture Pipeline Services
        services.AddSingleton<IWin32DiagnosticCaptureService, Win32DiagnosticCaptureService>();
        services.AddSingleton<IDirect3D11DeviceProvider, Direct3D11DeviceProvider>();
        services.AddSingleton<IGraphicsCaptureItemFactory, GraphicsCaptureItemFactory>();
        services.AddSingleton<ICaptureSessionManager, CaptureSessionManager>();
        services.AddSingleton<ICapturePreviewRenderer, Direct3D11PreviewRenderer>();
        services.AddSingleton<ICaptureCoordinator, CaptureCoordinator>();

        // Presentation Output & Orchestration Services
        services.AddSingleton<IWindowActivationService, Win32WindowActivationService>();
        services.AddSingleton<IPresentationWindowService, PresentationWindowService>();
        services.AddSingleton<IPresentationOutputRenderer, Direct3D11PresentationRenderer>();
        services.AddSingleton<IPresentationCoordinator, PresentationCoordinator>();
        services.AddSingleton<IPresenterDockService, PresenterDockService>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IApplicationLifecycleService, ApplicationLifecycleService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PresentationViewModel>();
        services.AddTransient<PresenterDockViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // Preload persistent user settings before initializing UI
        var settingsService = Services.GetRequiredService<IApplicationSettingsService>();
        await settingsService.LoadSettingsAsync();

        var presentationStateService = Services.GetRequiredService<IPresentationStateService>();
        presentationStateService.SetSwitchMode(settingsService.CurrentSettings.SwitchMode);

        // Initialize and wire up system-wide global hotkeys
        var hotkeyService = Services.GetRequiredService<IHotkeyService>();
        var presentationCoordinator = Services.GetRequiredService<IPresentationCoordinator>();
        var presenterDockService = Services.GetRequiredService<IPresenterDockService>();

        hotkeyService.HotkeyTriggered += (s, e) =>
        {
            switch (e.Action)
            {
                case Models.HotkeyAction.NextSource:
                    _ = presentationCoordinator.SwitchToNextSourceAsync();
                    break;
                case Models.HotkeyAction.PreviousSource:
                    _ = presentationCoordinator.SwitchToPreviousSourceAsync();
                    break;
                case Models.HotkeyAction.TogglePause:
                    if (presentationCoordinator.IsPaused)
                    {
                        _ = presentationCoordinator.ResumePresentationAsync();
                    }
                    else
                    {
                        _ = presentationCoordinator.PausePresentationAsync();
                    }
                    break;
                case Models.HotkeyAction.ToggleBlackout:
                    _ = presentationCoordinator.ToggleBlackoutAsync();
                    break;
                case Models.HotkeyAction.StopPresentation:
                    _ = presentationCoordinator.StopPresentationAsync();
                    break;
                case Models.HotkeyAction.TogglePresenterDock:
                    presenterDockService.ToggleDock();
                    break;
                case Models.HotkeyAction.ShowDashboard:
                    ActivateMainWindow();
                    break;
                case Models.HotkeyAction.SelectSource1:
                    _ = presentationCoordinator.SwitchToSourceIndexAsync(0);
                    break;
                case Models.HotkeyAction.SelectSource2:
                    _ = presentationCoordinator.SwitchToSourceIndexAsync(1);
                    break;
                case Models.HotkeyAction.SelectSource3:
                    _ = presentationCoordinator.SwitchToSourceIndexAsync(2);
                    break;
                case Models.HotkeyAction.SelectSource4:
                    _ = presentationCoordinator.SwitchToSourceIndexAsync(3);
                    break;
                case Models.HotkeyAction.SelectSource5:
                    _ = presentationCoordinator.SwitchToSourceIndexAsync(4);
                    break;
            }
        };

        presenterDockService.RequestShowDashboard += (s, e) => ActivateMainWindow();

        hotkeyService.Initialize();

        _mainWindow = new MainWindow();

        var windowIconService = Services.GetService<IWindowIconService>() as Win32WindowIconService;
        if (windowIconService is not null && _mainWindow.DispatcherQueue is not null)
        {
            windowIconService.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        _mainWindow.Activate();
    }
}
