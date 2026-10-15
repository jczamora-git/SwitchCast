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
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Services = ConfigureServices();

        var reportingService = Services.GetRequiredService<SwitchCast.Services.Diagnostics.IErrorReportingService>();
        SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.Initialize(reportingService);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException(
                "App",
                "UnhandledException",
                e.Exception,
                $"HResult=0x{e.Exception.HResult:X8}, Message={e.Message}");
            System.Diagnostics.Debug.WriteLine($"[App.UnhandledException] HResult=0x{e.Exception.HResult:X8} - {e.Message}\n{e.Exception}");
        }
        catch
        {
            // Fail-safe: prevent error reporting from throwing during crash handling
        }
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            if (e.ExceptionObject is Exception ex)
            {
                SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException(
                    "AppDomain",
                    "UnhandledException",
                    ex,
                    $"IsTerminating={e.IsTerminating}, HResult=0x{ex.HResult:X8}");
                System.Diagnostics.Debug.WriteLine($"[AppDomain.UnhandledException] IsTerminating={e.IsTerminating} - {ex}");
            }
            else
            {
                SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogWarning("AppDomain", "UnhandledException", $"Non-exception object: {e.ExceptionObject}");
            }
        }
        catch
        {
            // Fail-safe
        }
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException(
                "TaskScheduler",
                "UnobservedTaskException",
                e.Exception,
                $"Observed={e.Observed}, HResult=0x{e.Exception.HResult:X8}");
            System.Diagnostics.Debug.WriteLine($"[TaskScheduler.UnobservedTaskException] {e.Exception}");
            e.SetObserved();
        }
        catch
        {
            // Fail-safe
        }
    }

    public static new App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    public MainWindow? MainWindow => _mainWindow as MainWindow;

    /// <summary>
    /// Activates and brings the primary application window to the foreground, restoring if minimized, and ensures navigation to Dashboard.
    /// </summary>
    public void ActivateMainWindow()
    {
        var lifecycleService = Services.GetService<IApplicationLifecycleService>();
        if (lifecycleService is not null && (lifecycleService.IsShuttingDown || lifecycleService.IsShutdownApproved))
        {
            return;
        }

        var activationService = Services.GetService<IWindowActivationService>();
        bool activated = activationService?.ActivateMainWindow() ?? false;

        if (!activated)
        {
            _mainWindow?.Activate();
        }

        var navigationService = Services.GetService<INavigationService>();
        navigationService?.NavigateToDashboard();
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

        // Media Services
        services.AddSingleton<Services.Media.IMediaDiscoveryService, Services.Media.MediaDiscoveryService>();
        services.AddSingleton<Services.Media.IMediaPickerService, Services.Media.Win32MediaPickerService>();
        services.AddSingleton<Services.Media.IMediaPresentationService, Services.Media.MediaPresentationService>();

        // PowerPoint Services
        services.AddSingleton<Services.PowerPoint.IPresentationSlideRenderer, Services.PowerPoint.PowerPointComSlideRenderer>();
        services.AddSingleton<Services.PowerPoint.IPowerPointPresentationService, Services.PowerPoint.PowerPointPresentationService>();
        services.AddSingleton<Services.PowerPoint.IPowerPointLiveService, Services.PowerPoint.PowerPointLiveService>();

        // Camera Services
        services.AddSingleton<Services.Camera.ICameraDiscoveryService, Services.Camera.CameraDiscoveryService>();
        services.AddSingleton<Services.Camera.ICameraCaptureService, Services.Camera.CameraCaptureService>();

        // Presentation Output & Orchestration Services
        services.AddSingleton<IWindowActivationService, Win32WindowActivationService>();
        services.AddSingleton<IPresentationWindowService, PresentationWindowService>();
        services.AddSingleton<IExternalDisplayService, ExternalDisplayService>();
        services.AddSingleton<IPresentationOutputRenderer, Direct3D11PresentationRenderer>();
        services.AddSingleton<IPresentationCoordinator, PresentationCoordinator>();
        services.AddSingleton<IPresenterDockService, PresenterDockService>();
        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IApplicationLifecycleService, ApplicationLifecycleService>();

        // Diagnostics & Error Reporting Services
        services.AddSingleton<SwitchCast.Services.Diagnostics.IDiagnosticRedactionService, SwitchCast.Services.Diagnostics.DiagnosticRedactionService>();
        services.AddSingleton<SwitchCast.Services.Diagnostics.IDiagnosticReportBuilder, SwitchCast.Services.Diagnostics.DiagnosticReportBuilder>();
        services.AddSingleton<SwitchCast.Services.Diagnostics.IErrorReportingService, SwitchCast.Services.Diagnostics.ErrorReportingService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SourcesViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PresentationViewModel>();
        services.AddTransient<PresenterDockViewModel>();
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

        var powerPointService = Services.GetService<Services.PowerPoint.IPowerPointPresentationService>();
        if (powerPointService is not null && _mainWindow.DispatcherQueue is not null)
        {
            powerPointService.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        var dashboardViewModel = Services.GetService<DashboardViewModel>();
        if (dashboardViewModel is not null && _mainWindow.DispatcherQueue is not null)
        {
            dashboardViewModel.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        var mediaService = Services.GetService<Services.Media.IMediaPresentationService>();
        if (mediaService is not null && _mainWindow.DispatcherQueue is not null)
        {
            mediaService.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        var sourcesViewModel = Services.GetService<SourcesViewModel>();
        if (sourcesViewModel is not null && _mainWindow.DispatcherQueue is not null)
        {
            sourcesViewModel.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        if (_mainWindow.DispatcherQueue is not null)
        {
            presenterDockService.SetDispatcherQueue(_mainWindow.DispatcherQueue);
        }

        _mainWindow.Activate();
    }
}
