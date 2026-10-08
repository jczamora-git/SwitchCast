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
}
