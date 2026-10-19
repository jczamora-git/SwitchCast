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
}
