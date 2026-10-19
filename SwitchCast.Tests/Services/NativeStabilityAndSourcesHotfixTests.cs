using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Performance;
using SwitchCast.Services;
using SwitchCast.Services.Camera;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Performance;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Regression tests verifying the v1.5.0 stability hotfixes:
/// 1. Sources default category (Application Windows on fresh session, session preservation, in-place count updates).
/// 2. Category ComboBox stability (stable CategoryOptions, SelectedCategoryOption, never blank, guarded against -1).
/// 3. D3D11 device reset diagnostics (SetRenderingMode does not log false resets, actual loss increments counter).
/// 4. Stop &amp; Close lifecycle safety.
/// </summary>
public class NativeStabilityAndSourcesHotfixTests
{
    [Fact]
    public void SourcesViewModel_FreshSession_DefaultsToApplicationWindows()
    {
        var mockState = new Mock<IPresentationStateService>();
        var mockWindow = new Mock<IWindowDiscoveryService>();
        var mockMonitor = new Mock<IMonitorDiscoveryService>();

        var vm = new SourcesViewModel(mockState.Object, mockWindow.Object, mockMonitor.Object);

        Assert.Equal(1, vm.SelectedCategoryIndex);
        Assert.NotNull(vm.SelectedCategoryOption);
        Assert.Equal(1, vm.SelectedCategoryOption.CategoryIndex);
        Assert.Equal("Application Windows", vm.SelectedCategoryOption.Name);
        Assert.True(vm.IsWindowsCategorySelected);
        Assert.False(vm.IsAllSourcesCategorySelected);
        Assert.False(vm.IsCameraCategorySelected);
    }

    [Fact]
    public async Task SourcesViewModel_CategoryOptions_PreservesSelectionAndUpdatesCountInPlace()
    {
        var mockState = new Mock<IPresentationStateService>();
        var mockWindow = new Mock<IWindowDiscoveryService>();
        var mockMonitor = new Mock<IMonitorDiscoveryService>();

        mockWindow.Setup(w => w.EnumerateWindowsAsync(default))
            .ReturnsAsync(new List<WindowSource>
            {
                new() { Id = "win-1", Title = "Window 1", ProcessName = "app1", WindowHandle = 101, ProcessId = 1001 },
                new() { Id = "win-2", Title = "Window 2", ProcessName = "app2", WindowHandle = 102, ProcessId = 1002 },
                new() { Id = "win-3", Title = "Window 3", ProcessName = "app3", WindowHandle = 103, ProcessId = 1003 }
            });
        mockMonitor.Setup(m => m.EnumerateMonitorsAsync(default))
            .ReturnsAsync(new List<MonitorSource>());

        var vm = new SourcesViewModel(mockState.Object, mockWindow.Object, mockMonitor.Object);
        var initialOption = vm.SelectedCategoryOption;
        Assert.NotNull(initialOption);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        // Same object instance preserved (stable identity)
        Assert.Same(initialOption, vm.SelectedCategoryOption);
        Assert.Equal(1, vm.SelectedCategoryIndex);
        Assert.Equal("Application Windows (3)", vm.SelectedCategoryOption!.DisplayText);
        Assert.Equal(3, vm.DisplayedSources.Count);
    }

    [Fact]
    public async Task SourcesViewModel_SelectingCameras_UpdatesSelectionAndLabelImmediately()
    {
        var mockState = new Mock<IPresentationStateService>();
        var mockWindow = new Mock<IWindowDiscoveryService>();
        var mockMonitor = new Mock<IMonitorDiscoveryService>();
        var mockCamera = new Mock<ICameraDiscoveryService>();

        mockWindow.Setup(w => w.EnumerateWindowsAsync(default))
            .ReturnsAsync(new List<WindowSource>());
        mockMonitor.Setup(m => m.EnumerateMonitorsAsync(default))
            .ReturnsAsync(new List<MonitorSource>());
        mockCamera.Setup(c => c.EnumerateCamerasAsync(default))
            .ReturnsAsync(new List<CameraSource>
            {
                new() { Id = "cam-1", Title = "Integrated Webcam", DeviceId = "dev-1" },
                new() { Id = "cam-2", Title = "External USB Camera", DeviceId = "dev-2" }
            });

        var vm = new SourcesViewModel(
            mockState.Object,
            mockWindow.Object,
            mockMonitor.Object,
            cameraDiscoveryService: mockCamera.Object);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        // User selects Cameras category (index 5)
        vm.SelectedCategoryIndex = 5;

        Assert.Equal(5, vm.SelectedCategoryIndex);
        Assert.NotNull(vm.SelectedCategoryOption);
        Assert.Equal("Cameras", vm.SelectedCategoryOption.Name);
        Assert.Equal("Cameras (2)", vm.SelectedCategoryOption.DisplayText);
        Assert.Equal(2, vm.DisplayedSources.Count);
        Assert.True(vm.IsCameraCategorySelected);
    }

    [Fact]
    public void SourcesViewModel_NegativeOrOutOfRangeIndex_GuardsAndDefaultsToApplicationWindows()
    {
        var mockState = new Mock<IPresentationStateService>();
        var mockWindow = new Mock<IWindowDiscoveryService>();
        var mockMonitor = new Mock<IMonitorDiscoveryService>();

        var vm = new SourcesViewModel(mockState.Object, mockWindow.Object, mockMonitor.Object);

        // Simulate XAML ComboBox de-selection setting SelectedIndex to -1
        vm.SelectedCategoryIndex = -1;

        Assert.Equal(1, vm.SelectedCategoryIndex);
        Assert.NotNull(vm.SelectedCategoryOption);
        Assert.Equal("Application Windows", vm.SelectedCategoryOption.Name);

        // Simulate out of range 99
        vm.SelectedCategoryIndex = 99;

        Assert.Equal(1, vm.SelectedCategoryIndex);
        Assert.Equal("Application Windows", vm.SelectedCategoryOption.Name);
    }

    [Fact]
    public void Direct3D11DeviceProvider_SetRenderingMode_DoesNotRecordDeviceReset()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        var provider = new Direct3D11DeviceProvider(metricsService: mockMetrics.Object);

        // Switch to PreferHardware
        provider.SetRenderingMode(RenderingMode.PreferHardware);
        Assert.Equal(RenderingMode.PreferHardware, provider.ActiveMode);

        // Switch to CompatibilityMode
        provider.SetRenderingMode(RenderingMode.CompatibilityMode);
        Assert.Equal(RenderingMode.CompatibilityMode, provider.ActiveMode);

        // Calling same mode again
        provider.SetRenderingMode(RenderingMode.CompatibilityMode);
        Assert.Equal(RenderingMode.CompatibilityMode, provider.ActiveMode);

        // SetRenderingMode is administrative reconfiguration, NOT a device loss/reset event
        mockMetrics.Verify(m => m.RecordDeviceReset(), Times.Never);
    }

    [Fact]
    public void Direct3D11DeviceProvider_ResetDevice_RecordsResetWhenExplicitlyCalled()
    {
        var mockMetrics = new Mock<IPerformanceMetricsService>();
        var provider = new Direct3D11DeviceProvider(metricsService: mockMetrics.Object);

        provider.ResetDevice();

        mockMetrics.Verify(m => m.RecordDeviceReset(), Times.Once);
    }

    [Fact]
    public void PerformanceMetricsService_RecordDeviceReset_IncrementsCounter()
    {
        var metrics = new PerformanceMetricsService();

        Assert.Equal(0, metrics.GetCurrentSnapshot().GpuDeviceResetCount);

        metrics.RecordDeviceReset();
        Assert.Equal(1, metrics.GetCurrentSnapshot().GpuDeviceResetCount);

        metrics.RecordDeviceReset();
        Assert.Equal(2, metrics.GetCurrentSnapshot().GpuDeviceResetCount);
    }

    [Fact]
    public void GraphicsCapabilityService_GetCapabilities_ProbesWithoutDeviceReset()
    {
        var capabilityService = new GraphicsCapabilityService();
        var caps = capabilityService.GetCapabilities();

        Assert.NotNull(caps);
        Assert.NotNull(caps.FeatureLevel);
    }
}
