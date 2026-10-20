using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Models.Diagnostics;
using SwitchCast.Services;
using SwitchCast.Services.Diagnostics;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing application theme configuration, preferences, presenter settings, shortcuts, version metadata, and diagnostics.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationSettingsService _settingsService;
    private readonly IHotkeyService? _hotkeyService;
    private readonly IErrorReportingService? _errorReportingService;
    private readonly IDiagnosticReportBuilder? _reportBuilder;
    private readonly Services.Performance.IGraphicsCapabilityService? _capabilityService;
    private readonly Services.Capture.IDirect3D11DeviceProvider? _deviceProvider;
    private readonly Services.Performance.IPerformanceMetricsService? _metricsService;
    private readonly IPresentationStateService? _presentationStateService;
    private Models.Performance.GraphicsCapabilities? _graphicsCapabilities;

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _telemetryTimer;
    private bool _isPageLoaded;
    private bool _isDisposed;

    [ObservableProperty]
    private int _selectedCategoryIndex = 0; // 0=General, 1=Appearance, 2=Window, 3=Presenter, 4=Shortcuts, 5=Diagnostics, 6=Performance

    [ObservableProperty]
    private int _selectedRenderingModeIndex; // 0=Auto, 1=PreferHardware, 2=CompatibilityMode

    [ObservableProperty]
    private bool _enablePerformanceDiagnostics;

    [ObservableProperty]
    private Models.Performance.PerformanceSnapshot? _currentPerformanceSnapshot;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _rememberWindowDimensions;

    [ObservableProperty]
    private bool _enableGlobalHotkeys;

    [ObservableProperty]
    private bool _autoOpenPresenterDock;

    [ObservableProperty]
    private bool _dockAlwaysOnTop;

    [ObservableProperty]
    private bool _startDockInCompactMode;

    [ObservableProperty]
    private int _selectedSwitchModeIndex;

    [ObservableProperty]
    private bool _autoDetectExternalDisplay;

    [ObservableProperty]
    private bool _autoFullscreenOnExternalDisplay;

    [ObservableProperty]
    private int _selectedPreferredDisplayIndex;

    [ObservableProperty]
    private string _shortcutSearchQuery = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<HotkeyBinding> _hotkeyBindings;

    private readonly IMonitorDiscoveryService? _monitorDiscoveryService;
    private readonly List<MonitorSource> _discoveredMonitors = [];

    public ObservableCollection<string> AvailableDisplays { get; } = [];

    public ObservableCollection<DiagnosticIncident> RecentIncidents { get; } = [];

    public string LogsDirectoryPath => _errorReportingService?.LogsDirectory ?? string.Empty;

    public SettingsViewModel(
        IApplicationSettingsService settingsService,
        IHotkeyService? hotkeyService = null,
        IMonitorDiscoveryService? monitorDiscoveryService = null,
        IErrorReportingService? errorReportingService = null,
        IDiagnosticReportBuilder? reportBuilder = null,
        Services.Performance.IGraphicsCapabilityService? capabilityService = null,
        Services.Capture.IDirect3D11DeviceProvider? deviceProvider = null,
        Services.Performance.IPerformanceMetricsService? metricsService = null,
        IPresentationStateService? presentationStateService = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _hotkeyService = hotkeyService;
        _monitorDiscoveryService = monitorDiscoveryService;
        _errorReportingService = errorReportingService;
        _reportBuilder = reportBuilder;
        _capabilityService = capabilityService;
        _deviceProvider = deviceProvider;
        _metricsService = metricsService;
        _presentationStateService = presentationStateService;

        if (_presentationStateService is not null)
        {
            _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        }

        var current = _settingsService.CurrentSettings;
        _selectedThemeIndex = (int)current.Theme;
        _rememberWindowDimensions = current.RememberWindowDimensions;
        _enableGlobalHotkeys = current.EnableGlobalHotkeys;
        _autoOpenPresenterDock = current.AutoOpenPresenterDock;
        _dockAlwaysOnTop = current.DockAlwaysOnTop;
        _startDockInCompactMode = current.StartDockInCompactMode;
        _selectedSwitchModeIndex = (int)current.SwitchMode;
        _autoDetectExternalDisplay = current.AutoDetectExternalDisplay;
        _autoFullscreenOnExternalDisplay = current.AutoFullscreenOnExternalDisplay;
        _hotkeyBindings = current.HotkeyBindings.ToList();
        _selectedRenderingModeIndex = (int)current.RenderingMode;
        _enablePerformanceDiagnostics = current.EnablePerformanceDiagnostics;

        if (_metricsService is not null)
        {
            _metricsService.IsDiagnosticsEnabled = _enablePerformanceDiagnostics;
        }

        if (_enablePerformanceDiagnostics && _metricsService is not null)
        {
            _currentPerformanceSnapshot = _metricsService.GetCurrentSnapshot();
        }

        if (_capabilityService is not null)
        {
            _graphicsCapabilities = _capabilityService.GetCapabilities(forceRefresh: false);
        }

        InitializeDisplayOptions(current.PreferredDisplayDeviceName);
        RefreshIncidents();
    }

    private async void InitializeDisplayOptions(string? preferredDeviceName)
    {
        AvailableDisplays.Clear();
        AvailableDisplays.Add("Auto (Recommended)");

        if (_monitorDiscoveryService is not null)
        {
            try
            {
                var monitors = await _monitorDiscoveryService.EnumerateMonitorsAsync();
                _discoveredMonitors.Clear();
                _discoveredMonitors.AddRange(monitors);

                int selectedIdx = 0;
                for (int i = 0; i < monitors.Count; i++)
                {
                    var mon = monitors[i];
                    AvailableDisplays.Add(mon.Title);
                    if (!string.IsNullOrEmpty(preferredDeviceName) &&
                        (string.Equals(mon.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(mon.Id, preferredDeviceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        selectedIdx = i + 1;
                    }
                }

                SelectedPreferredDisplayIndex = selectedIdx;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InitializeDisplayOptions error: {ex.Message}");
            }
        }
    }

    public string AppName => "SwitchCast";

    public string AppSubtitle => "Screen Sharing & Presentation Manager";

    public string AppVersion
    {
        get
        {
            var infoVer = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                return infoVer.Split('+')[0];
            }

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.5.0";
        }
    }

    public string Creator => "John Christopher King Zamora";

    public string WebsiteUrl => "https://switchcast.wasmer.app/";

    public string RepositoryUrl => WebsiteUrl;

    public string BuiltWith => "C# / .NET 8 / WinUI 3 / Windows App SDK";

    public string TargetFramework => ".NET 8.0 (WinUI 3 / Windows App SDK)";

    public string PlatformDescription => $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    public string AppDescription =>
        "SwitchCast enables presenters to capture multiple running application windows or connected monitors and switch seamlessly between them inside a single, dedicated, shareable presentation output window.";

    public string PrivacyStatement => "100% Offline & Local • Zero Telemetry • No Network Access";

    public string PrivacyDetails => "All video frames, graphics buffers, window titles, and settings are processed strictly in local device memory. No network connections, analytics, or telemetry are ever made.";

    public Visibility GeneralCategoryVisibility => SelectedCategoryIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AppearanceCategoryVisibility => SelectedCategoryIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WindowCategoryVisibility => SelectedCategoryIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PresenterCategoryVisibility => SelectedCategoryIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShortcutsCategoryVisibility => SelectedCategoryIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DiagnosticsCategoryVisibility => SelectedCategoryIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PerformanceCategoryVisibility => SelectedCategoryIndex == 6 ? Visibility.Visible : Visibility.Collapsed;

    public string PrimaryGpuName => _graphicsCapabilities?.PrimaryAdapter?.Name ?? "Default Display Adapter";
    public string GpuVendorName => _graphicsCapabilities?.PrimaryAdapter?.VendorName ?? "Unknown";
    public string DedicatedVideoMemory => _graphicsCapabilities?.PrimaryAdapter?.DedicatedMemoryMB ?? "—";
    public string SharedVideoMemory => _graphicsCapabilities?.PrimaryAdapter?.SharedMemoryMB ?? "—";
    public string Direct3DFeatureLevel => _graphicsCapabilities?.FeatureLevel ?? "Direct3D 11.0";
    public string ActiveBackendText => _deviceProvider?.ActiveBackendDescription ?? "Direct3D 11 Hardware";
    public string HardwareAccelerationStatus => (_deviceProvider?.IsHardwareAccelerated ?? true) ? "Hardware Accelerated" : "Software (WARP)";
    public string CapabilitiesSummaryText => _graphicsCapabilities?.SummaryText ?? string.Empty;
    public string FallbackNote => _deviceProvider?.ActiveMode == Models.Performance.RenderingMode.CompatibilityMode
        ? "Compatibility Mode uses Direct3D 11 WARP software rasterization for reliable operation on devices with unstable or restricted display drivers."
        : _graphicsCapabilities?.IsIntegratedGpu == true
            ? "Integrated GPU detected. SwitchCast adapts frame queue pacing and eliminates redundant surface conversions automatically."
            : "Direct3D 11 hardware pipeline active. Presentation Output is rendered with zero CPU readback.";

    // --- LIVE PERFORMANCE TELEMETRY MONITOR PROPERTIES ---
    public bool IsTelemetryActive => EnablePerformanceDiagnostics;
    public Visibility TelemetryActiveVisibility => EnablePerformanceDiagnostics ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TelemetryDisabledVisibility => EnablePerformanceDiagnostics ? Visibility.Collapsed : Visibility.Visible;

    public string ProcessCpuUsageText => (EnablePerformanceDiagnostics && CurrentPerformanceSnapshot is not null)
        ? $"{CurrentPerformanceSnapshot.ProcessCpuPercent:F1}%"
        : "—";

    public string WorkingSetMemoryText => (EnablePerformanceDiagnostics && CurrentPerformanceSnapshot is not null)
        ? $"{CurrentPerformanceSnapshot.WorkingSetMB:F1} MB"
        : "—";

    public string GpuUtilizationText => "Unsupported";
    public string GpuUtilizationDetail => "Zero-Overhead • ETW tracing required for %";

    public string LiveOutputFpsText
    {
        get
        {
            if (!EnablePerformanceDiagnostics)
            {
                return "—";
            }

            bool isPresenting = _presentationStateService?.Status == PresentationStatus.Active;
            if (!isPresenting)
            {
                return "Not Presenting";
            }

            double fps = CurrentPerformanceSnapshot?.OutputFps ?? 0.0;
            return $"{fps:F1} FPS";
        }
    }

    public string LivePreviewFpsText
    {
        get
        {
            if (!EnablePerformanceDiagnostics)
            {
                return "—";
            }

            double fps = CurrentPerformanceSnapshot?.PreviewFps ?? 0.0;
            if (fps <= 0.0)
            {
                return "Idle";
            }

            return $"{fps:F1} FPS";
        }
    }

    public string DroppedFramesText => (EnablePerformanceDiagnostics && CurrentPerformanceSnapshot is not null)
        ? $"{CurrentPerformanceSnapshot.FramesDropped:N0} frames"
        : "—";

    public string PresentationStartupLatencyText
    {
        get
        {
            if (!EnablePerformanceDiagnostics || CurrentPerformanceSnapshot is null)
            {
                return "—";
            }

            if (CurrentPerformanceSnapshot.PresentationStartupLatencyMs <= 0)
            {
                return "Not Available";
            }

            return $"{CurrentPerformanceSnapshot.PresentationStartupLatencyMs:F1} ms";
        }
    }

    public string SourceSwitchLatencyText
    {
        get
        {
            if (!EnablePerformanceDiagnostics || CurrentPerformanceSnapshot is null)
            {
                return "—";
            }

            if (CurrentPerformanceSnapshot.LastSourceSwitchLatencyMs <= 0)
            {
                return "Not Available";
            }

            return $"{CurrentPerformanceSnapshot.LastSourceSwitchLatencyMs:F1} ms";
        }
    }

    public string LiveActiveBackendText
    {
        get
        {
            if (_deviceProvider is null || !_deviceProvider.IsInitialized)
            {
                return "Not Initialized";
            }

            return _deviceProvider.ActiveBackendDescription;
        }
    }

    public string LiveHardwareAccelerationText
    {
        get
        {
            if (_deviceProvider is null || !_deviceProvider.IsInitialized)
            {
                return "Not Initialized";
            }

            return _deviceProvider.IsHardwareAccelerated ? "Hardware Accelerated" : "Software (WARP)";
        }
    }

    public string DeviceResetCountText => (EnablePerformanceDiagnostics && CurrentPerformanceSnapshot is not null)
        ? $"{CurrentPerformanceSnapshot.GpuDeviceResetCount}"
        : "0";

    public string PipelineRecoveryStatusText
    {
        get
        {
            if (_deviceProvider is null || !_deviceProvider.IsInitialized)
            {
                return "Not Initialized";
            }

            int resets = CurrentPerformanceSnapshot?.GpuDeviceResetCount ?? 0;
            if (_deviceProvider.ActiveBackendDescription.Contains("Fallback", StringComparison.OrdinalIgnoreCase))
            {
                return "WARP Fallback Active";
            }

            if (_deviceProvider.ActiveMode == Models.Performance.RenderingMode.CompatibilityMode)
            {
                return "Compatibility Mode (WARP)";
            }

            if (resets > 0)
            {
                return $"Recovered ({resets} reset{(resets == 1 ? "" : "s")})";
            }

            return "Operational";
        }
    }
    public Visibility NoIncidentsVisibility => RecentIncidents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => RecentIncidents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasIncidentsVisibility => RecentIncidents.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public string IncidentsCountText => RecentIncidents.Count.ToString();
    public string LastRecordedErrorText => RecentIncidents.FirstOrDefault()?.SafeMessage ?? "None recorded";
    public string LastRecordedErrorTimestamp => RecentIncidents.FirstOrDefault()?.FormattedTimestamp ?? "—";

    public IEnumerable<HotkeyBinding> FilteredHotkeyBindings
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ShortcutSearchQuery))
            {
                return HotkeyBindings;
            }

            return HotkeyBindings.Where(b =>
                b.Name.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                b.Description.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                b.DisplayString.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(GeneralCategoryVisibility));
        OnPropertyChanged(nameof(AppearanceCategoryVisibility));
        OnPropertyChanged(nameof(WindowCategoryVisibility));
        OnPropertyChanged(nameof(PresenterCategoryVisibility));
        OnPropertyChanged(nameof(ShortcutsCategoryVisibility));
        OnPropertyChanged(nameof(DiagnosticsCategoryVisibility));
        OnPropertyChanged(nameof(PerformanceCategoryVisibility));

        if (value == 5)
        {
            RefreshIncidents();
        }
        else if (value == 6)
        {
            RefreshPerformanceData();
        }

        UpdateTelemetryTimerState();
    }

    partial void OnShortcutSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredHotkeyBindings));
    }

    partial void OnHotkeyBindingsChanged(IReadOnlyList<HotkeyBinding> value)
    {
        OnPropertyChanged(nameof(FilteredHotkeyBindings));
    }

    async partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = (ApplicationThemeOption)value;
        await _settingsService.SetThemeAsync(theme);
    }

    async partial void OnRememberWindowDimensionsChanged(bool value)
    {
        _settingsService.CurrentSettings.RememberWindowDimensions = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnEnableGlobalHotkeysChanged(bool value)
    {
        _settingsService.CurrentSettings.EnableGlobalHotkeys = value;
        _hotkeyService?.SetEnabled(value);
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnAutoOpenPresenterDockChanged(bool value)
    {
        _settingsService.CurrentSettings.AutoOpenPresenterDock = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnDockAlwaysOnTopChanged(bool value)
    {
        _settingsService.CurrentSettings.DockAlwaysOnTop = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnStartDockInCompactModeChanged(bool value)
    {
        _settingsService.CurrentSettings.StartDockInCompactMode = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnSelectedSwitchModeIndexChanged(int value)
    {
        var mode = (PresenterSwitchMode)value;
        _settingsService.CurrentSettings.SwitchMode = mode;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnAutoDetectExternalDisplayChanged(bool value)
    {
        _settingsService.CurrentSettings.AutoDetectExternalDisplay = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnAutoFullscreenOnExternalDisplayChanged(bool value)
    {
        _settingsService.CurrentSettings.AutoFullscreenOnExternalDisplay = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnSelectedPreferredDisplayIndexChanged(int value)
    {
        if (value <= 0 || value > _discoveredMonitors.Count)
        {
            _settingsService.CurrentSettings.PreferredDisplayDeviceName = null;
        }
        else
        {
            _settingsService.CurrentSettings.PreferredDisplayDeviceName = _discoveredMonitors[value - 1].DeviceName;
        }
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnSelectedRenderingModeIndexChanged(int value)
    {
        var mode = (Models.Performance.RenderingMode)value;
        _settingsService.CurrentSettings.RenderingMode = mode;
        _deviceProvider?.SetRenderingMode(mode);
        await _settingsService.SaveSettingsAsync();
        RefreshPerformanceData();
    }

    async partial void OnEnablePerformanceDiagnosticsChanged(bool value)
    {
        _settingsService.CurrentSettings.EnablePerformanceDiagnostics = value;
        if (_metricsService is not null)
        {
            _metricsService.IsDiagnosticsEnabled = value;
        }
        await _settingsService.SaveSettingsAsync();

        if (!value)
        {
            StopTelemetryTimer();
            CurrentPerformanceSnapshot = null;
        }
        else
        {
            SampleTelemetry();
            UpdateTelemetryTimerState();
        }

        NotifyTelemetryPropertiesChanged();
        OnPropertyChanged(nameof(IsTelemetryActive));
        OnPropertyChanged(nameof(TelemetryActiveVisibility));
        OnPropertyChanged(nameof(TelemetryDisabledVisibility));
    }

    public void SetDispatcherQueue(DispatcherQueue? dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        UpdateTelemetryTimerState();
    }

    public void OnPageLoaded()
    {
        _isPageLoaded = true;
        UpdateTelemetryTimerState();
    }

    public void OnPageUnloaded()
    {
        _isPageLoaded = false;
        StopTelemetryTimer();
    }

    private void UpdateTelemetryTimerState()
    {
        if (_isDisposed)
        {
            StopTelemetryTimer();
            return;
        }

        bool shouldRun = EnablePerformanceDiagnostics && (_isPageLoaded || _dispatcherQueue is null) && SelectedCategoryIndex == 6;

        if (shouldRun)
        {
            StartTelemetryTimer();
        }
        else
        {
            StopTelemetryTimer();
        }
    }

    private void StartTelemetryTimer()
    {
        if (_telemetryTimer is not null && _telemetryTimer.IsRunning)
        {
            return;
        }

        SampleTelemetry();

        if (_dispatcherQueue is not null)
        {
            try
            {
                if (_telemetryTimer is null)
                {
                    _telemetryTimer = _dispatcherQueue.CreateTimer();
                    _telemetryTimer.Interval = TimeSpan.FromSeconds(1);
                    _telemetryTimer.IsRepeating = true;
                    _telemetryTimer.Tick += (s, e) => SampleTelemetry();
                }
                _telemetryTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Failed to start telemetry timer: {ex.Message}");
            }
        }
    }

    private void StopTelemetryTimer()
    {
        if (_telemetryTimer is not null)
        {
            try
            {
                _telemetryTimer.Stop();
            }
            catch
            {
                // Ignore cleanup error
            }
        }
    }

    public void SampleTelemetry()
    {
        if (_metricsService is not null)
        {
            CurrentPerformanceSnapshot = _metricsService.GetCurrentSnapshot();
        }
        NotifyTelemetryPropertiesChanged();
    }

    private void NotifyTelemetryPropertiesChanged()
    {
        OnPropertyChanged(nameof(ProcessCpuUsageText));
        OnPropertyChanged(nameof(WorkingSetMemoryText));
        OnPropertyChanged(nameof(LiveOutputFpsText));
        OnPropertyChanged(nameof(LivePreviewFpsText));
        OnPropertyChanged(nameof(DroppedFramesText));
        OnPropertyChanged(nameof(PresentationStartupLatencyText));
        OnPropertyChanged(nameof(SourceSwitchLatencyText));
        OnPropertyChanged(nameof(LiveActiveBackendText));
        OnPropertyChanged(nameof(LiveHardwareAccelerationText));
        OnPropertyChanged(nameof(DeviceResetCountText));
        OnPropertyChanged(nameof(PipelineRecoveryStatusText));
    }

    private void OnPresentationStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status))
        {
            OnPropertyChanged(nameof(LiveOutputFpsText));
        }
    }

    [RelayCommand]
    public void RefreshPerformanceData()
    {
        if (_capabilityService is not null)
        {
            _graphicsCapabilities = _capabilityService.GetCapabilities(forceRefresh: false);
        }
        if (_metricsService is not null)
        {
            CurrentPerformanceSnapshot = _metricsService.GetCurrentSnapshot();
        }
        OnPropertyChanged(nameof(PrimaryGpuName));
        OnPropertyChanged(nameof(GpuVendorName));
        OnPropertyChanged(nameof(DedicatedVideoMemory));
        OnPropertyChanged(nameof(SharedVideoMemory));
        OnPropertyChanged(nameof(Direct3DFeatureLevel));
        OnPropertyChanged(nameof(ActiveBackendText));
        OnPropertyChanged(nameof(HardwareAccelerationStatus));
        OnPropertyChanged(nameof(CapabilitiesSummaryText));
        OnPropertyChanged(nameof(FallbackNote));
        NotifyTelemetryPropertiesChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopTelemetryTimer();

        if (_presentationStateService is not null)
        {
            _presentationStateService.PropertyChanged -= OnPresentationStatePropertyChanged;
        }
    }

    [RelayCommand]
    public async Task ResetHotkeysToDefaultAsync()
    {
        var defaultHotkeys = UserSettings.GetDefaultHotkeys();
        _settingsService.CurrentSettings.HotkeyBindings = defaultHotkeys;
        HotkeyBindings = defaultHotkeys.ToList();
        _hotkeyService?.ReloadSettings();
        await _settingsService.SaveSettingsAsync();
    }

    [RelayCommand]
    public async Task ToggleHotkeyBindingAsync(HotkeyBinding? binding)
    {
        if (binding is null)
        {
            return;
        }

        binding.IsEnabled = !binding.IsEnabled;
        if (binding.IsEnabled)
        {
            _hotkeyService?.RegisterHotkey(binding);
        }
        else
        {
            _hotkeyService?.UnregisterHotkey(binding.Action);
        }

        HotkeyBindings = _settingsService.CurrentSettings.HotkeyBindings.ToList();
        await _settingsService.SaveSettingsAsync();
    }

    [RelayCommand]
    public void OpenLogsFolder()
    {
        if (_errorReportingService is null) return;
        try
        {
            Directory.CreateDirectory(_errorReportingService.LogsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _errorReportingService.LogsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] OpenLogsFolder failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RefreshIncidents()
    {
        RecentIncidents.Clear();
        if (_errorReportingService is not null)
        {
            foreach (var inc in _errorReportingService.GetRecentIncidents())
            {
                RecentIncidents.Add(inc);
            }
        }
        OnPropertyChanged(nameof(NoIncidentsVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(HasIncidentsVisibility));
        OnPropertyChanged(nameof(IncidentsCountText));
        OnPropertyChanged(nameof(LastRecordedErrorText));
        OnPropertyChanged(nameof(LastRecordedErrorTimestamp));
    }

    [RelayCommand]
    public async Task ClearDiagnosticsHistoryAsync()
    {
        if (_errorReportingService is not null)
        {
            await _errorReportingService.ClearIncidentHistoryAsync();
            RefreshIncidents();
        }
    }

    [RelayCommand]
    public async Task OpenWebsiteAsync()
    {
        try
        {
            var uri = new Uri(WebsiteUrl);
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to open website link: {ex.Message}");
        }
    }

    [RelayCommand]
    public Task OpenRepositoryAsync() => OpenWebsiteAsync();
}
