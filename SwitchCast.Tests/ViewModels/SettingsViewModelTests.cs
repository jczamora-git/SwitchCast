using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Performance;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Performance;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public SettingsViewModelTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings
        {
            Theme = ApplicationThemeOption.Dark,
            RememberWindowDimensions = true
        });
    }

    [Fact]
    public void InitialValues_ReflectSettingsService()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);

        Assert.Equal((int)ApplicationThemeOption.Dark, vm.SelectedThemeIndex);
        Assert.True(vm.RememberWindowDimensions);
        Assert.Equal("SwitchCast", vm.AppName);
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
        Assert.False(string.IsNullOrWhiteSpace(vm.PlatformDescription));
    }

    [Fact]
    public void ChangingSelectedThemeIndex_CallsSetThemeAsync()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object)
        {
            SelectedThemeIndex = (int)ApplicationThemeOption.Light
        };

        _mockSettingsService.Verify(s => s.SetThemeAsync(ApplicationThemeOption.Light), Times.Once);
    }

    [Fact]
    public void CategoryNavigation_UpdatesCategoryVisibilityProperly()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);

        Assert.Equal(0, vm.SelectedCategoryIndex);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.GeneralCategoryVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.AppearanceCategoryVisibility);

        vm.SelectedCategoryIndex = 1;
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.GeneralCategoryVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.AppearanceCategoryVisibility);

        vm.SelectedCategoryIndex = 4;
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.ShortcutsCategoryVisibility);
    }

    [Fact]
    public void ShortcutSearchQuery_FiltersHotkeyBindings()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);
        var initialCount = vm.FilteredHotkeyBindings.Count();
        Assert.True(initialCount > 0);

        vm.ShortcutSearchQuery = "Pause";
        var filtered = vm.FilteredHotkeyBindings.ToList();
        Assert.Single(filtered);
        Assert.Contains("Pause", filtered[0].Name);

        vm.ShortcutSearchQuery = string.Empty;
        Assert.Equal(initialCount, vm.FilteredHotkeyBindings.Count());
    }

    [Fact]
    public void AboutMetadata_ContainsCreatorAndWebsiteUrl()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);

        Assert.Equal("John Christopher King Zamora", vm.Creator);
        Assert.Equal("https://switchcast.wasmer.app/", vm.WebsiteUrl);
        Assert.Equal("https://switchcast.wasmer.app/", vm.RepositoryUrl);
        Assert.Equal("Screen Sharing & Presentation Manager", vm.AppSubtitle);
        Assert.Contains(".NET 8", vm.BuiltWith);
        Assert.Contains("WinUI 3", vm.BuiltWith);
        Assert.Contains("Zero Telemetry", vm.PrivacyStatement);
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
        Assert.NotNull(vm.OpenWebsiteCommand);
    }

    [Fact]
    public void PerformanceCategory_VisibilityToggles()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.PerformanceCategoryVisibility);

        vm.SelectedCategoryIndex = 6;
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.PerformanceCategoryVisibility);
    }

    [Fact]
    public void ChangingSelectedRenderingModeIndex_UpdatesSettingsAndDeviceProvider()
    {
        var mockDevice = new Mock<IDirect3D11DeviceProvider>();
        var vm = new SettingsViewModel(
            _mockSettingsService.Object,
            deviceProvider: mockDevice.Object);

        vm.SelectedRenderingModeIndex = (int)RenderingMode.CompatibilityMode;

        Assert.Equal(RenderingMode.CompatibilityMode, _mockSettingsService.Object.CurrentSettings.RenderingMode);
        mockDevice.Verify(d => d.SetRenderingMode(RenderingMode.CompatibilityMode), Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public void ChangingEnablePerformanceDiagnostics_UpdatesMetricsServiceAndSettings()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        var vm = new SettingsViewModel(
            _mockSettingsService.Object,
            metricsService: mockMetrics.Object);

        vm.EnablePerformanceDiagnostics = true;

        Assert.True(_mockSettingsService.Object.CurrentSettings.EnablePerformanceDiagnostics);
        mockMetrics.VerifySet(m => m.IsDiagnosticsEnabled = true, Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public void TelemetryDisabled_ShowsDisabledPlaceholderAndHidesMonitor()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);
        vm.EnablePerformanceDiagnostics = false;

        Assert.False(vm.IsTelemetryActive);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.TelemetryActiveVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.TelemetryDisabledVisibility);
        Assert.Equal("—", vm.ProcessCpuUsageText);
        Assert.Equal("—", vm.WorkingSetMemoryText);
        Assert.Equal("—", vm.LiveOutputFpsText);
        Assert.Equal("—", vm.LivePreviewFpsText);
    }

    [Fact]
    public void TelemetryEnabled_IdleStates_DisplayCorrectPlaceholderValues()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        mockMetrics.Setup(m => m.GetCurrentSnapshot()).Returns(new PerformanceSnapshot
        {
            ProcessCpuPercent = 4.2,
            ProcessWorkingSetBytes = 120 * 1024 * 1024,
            OutputFps = 0.0,
            PreviewFps = 0.0,
            FramesDropped = 0,
            PresentationStartupLatencyMs = 0,
            LastSourceSwitchLatencyMs = 0,
            GpuDeviceResetCount = 0
        });

        var mockState = new Mock<IPresentationStateService>();
        mockState.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);

        var mockDevice = new Mock<IDirect3D11DeviceProvider>();
        mockDevice.SetupGet(d => d.IsInitialized).Returns(false);

        var vm = new SettingsViewModel(
            _mockSettingsService.Object,
            deviceProvider: mockDevice.Object,
            metricsService: mockMetrics.Object,
            presentationStateService: mockState.Object);

        vm.EnablePerformanceDiagnostics = true;

        Assert.True(vm.IsTelemetryActive);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.TelemetryActiveVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.TelemetryDisabledVisibility);

        // Idle states
        Assert.Equal("4.2%", vm.ProcessCpuUsageText);
        Assert.Equal("120.0 MB", vm.WorkingSetMemoryText);
        Assert.Equal("Not Presenting", vm.LiveOutputFpsText);
        Assert.Equal("Idle", vm.LivePreviewFpsText);
        Assert.Equal("0 frames", vm.DroppedFramesText);
        Assert.Equal("Not Available", vm.PresentationStartupLatencyText);
        Assert.Equal("Not Available", vm.SourceSwitchLatencyText);
        Assert.Equal("Not Initialized", vm.LiveActiveBackendText);
        Assert.Equal("Not Initialized", vm.LiveHardwareAccelerationText);
        Assert.Equal("0", vm.DeviceResetCountText);
        Assert.Equal("Not Initialized", vm.PipelineRecoveryStatusText);
        Assert.Equal("Unsupported", vm.GpuUtilizationText);
    }

    [Fact]
    public void TelemetryEnabled_ActivePresentation_DisplaysMeasuredRates()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        mockMetrics.Setup(m => m.GetCurrentSnapshot()).Returns(new PerformanceSnapshot
        {
            ProcessCpuPercent = 12.5,
            ProcessWorkingSetBytes = 150 * 1024 * 1024,
            OutputFps = 59.8,
            PreviewFps = 15.0,
            FramesDropped = 2,
            PresentationStartupLatencyMs = 120.4,
            LastSourceSwitchLatencyMs = 45.2,
            GpuDeviceResetCount = 0
        });

        var mockState = new Mock<IPresentationStateService>();
        mockState.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        var mockDevice = new Mock<IDirect3D11DeviceProvider>();
        mockDevice.SetupGet(d => d.IsInitialized).Returns(true);
        mockDevice.SetupGet(d => d.ActiveBackendDescription).Returns("Direct3D 11 Hardware (Auto)");
        mockDevice.SetupGet(d => d.IsHardwareAccelerated).Returns(true);

        var vm = new SettingsViewModel(
            _mockSettingsService.Object,
            deviceProvider: mockDevice.Object,
            metricsService: mockMetrics.Object,
            presentationStateService: mockState.Object);

        vm.EnablePerformanceDiagnostics = true;

        Assert.Equal("12.5%", vm.ProcessCpuUsageText);
        Assert.Equal("150.0 MB", vm.WorkingSetMemoryText);
        Assert.Equal("59.8 FPS", vm.LiveOutputFpsText);
        Assert.Equal("15.0 FPS", vm.LivePreviewFpsText);
        Assert.Equal("2 frames", vm.DroppedFramesText);
        Assert.Equal("120.4 ms", vm.PresentationStartupLatencyText);
        Assert.Equal("45.2 ms", vm.SourceSwitchLatencyText);
        Assert.Equal("Direct3D 11 Hardware (Auto)", vm.LiveActiveBackendText);
        Assert.Equal("Hardware Accelerated", vm.LiveHardwareAccelerationText);
        Assert.Equal("Operational", vm.PipelineRecoveryStatusText);
    }

    [Fact]
    public void PresentationStatusChange_UpdatesLiveOutputFpsText()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        mockMetrics.Setup(m => m.GetCurrentSnapshot()).Returns(new PerformanceSnapshot
        {
            OutputFps = 60.0
        });

        var mockState = new Mock<IPresentationStateService>();
        mockState.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);

        var vm = new SettingsViewModel(
            _mockSettingsService.Object,
            metricsService: mockMetrics.Object,
            presentationStateService: mockState.Object);

        vm.EnablePerformanceDiagnostics = true;
        Assert.Equal("Not Presenting", vm.LiveOutputFpsText);

        mockState.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockState.Raise(s => s.PropertyChanged += null, new System.ComponentModel.PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));

        Assert.Equal("60.0 FPS", vm.LiveOutputFpsText);
    }
}
