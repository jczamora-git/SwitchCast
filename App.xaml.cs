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

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core & Application Services
        services.AddSingleton<IPresentationStateService, PresentationStateService>();
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IWindowDiscoveryService, Win32WindowDiscoveryService>();
        services.AddSingleton<IMonitorDiscoveryService, Win32MonitorDiscoveryService>();

        // Native Capture Pipeline Services
        services.AddSingleton<IDirect3D11DeviceProvider, Direct3D11DeviceProvider>();
        services.AddSingleton<IGraphicsCaptureItemFactory, GraphicsCaptureItemFactory>();
        services.AddSingleton<ICaptureSessionManager, CaptureSessionManager>();
        services.AddSingleton<ICapturePreviewRenderer, Direct3D11PreviewRenderer>();
        services.AddSingleton<ICaptureCoordinator, CaptureCoordinator>();

        // Presentation Output & Orchestration Services
        services.AddSingleton<IPresentationWindowService, PresentationWindowService>();
        services.AddSingleton<IPresentationOutputRenderer, Direct3D11PresentationRenderer>();
        services.AddSingleton<IPresentationCoordinator, PresentationCoordinator>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PresentationViewModel>();
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

        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
