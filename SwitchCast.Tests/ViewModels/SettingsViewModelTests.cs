using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
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
    public void AboutMetadata_ContainsCreatorAndRepositoryUrl()
    {
        var vm = new SettingsViewModel(_mockSettingsService.Object);

        Assert.Equal("John Christopher King Zamora", vm.Creator);
        Assert.Equal("https://github.com/jczamora-git/SwitchCast", vm.RepositoryUrl);
        Assert.Equal("Screen Sharing & Presentation Manager", vm.AppSubtitle);
        Assert.Contains(".NET 8", vm.BuiltWith);
        Assert.Contains("WinUI 3", vm.BuiltWith);
        Assert.Contains("Zero Telemetry", vm.PrivacyStatement);
        Assert.False(string.IsNullOrWhiteSpace(vm.AppVersion));
    }
}
