using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly Mock<IPresentationStateService> _mockPresentationStateService;

    public MainViewModelTests()
    {
        _mockNavigationService = new Mock<INavigationService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockPresentationStateService = new Mock<IPresentationStateService>();

        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings
        {
            Theme = ApplicationThemeOption.Dark
        });
    }

    [Fact]
    public void InitialValues_ReflectSettingsAndDefaults()
    {
        var vm = new MainViewModel(
            _mockNavigationService.Object,
            _mockSettingsService.Object,
            _mockPresentationStateService.Object);

        Assert.Equal("SwitchCast — Screen Sharing Manager", vm.WindowTitle);
        Assert.Equal(ApplicationThemeOption.Dark, vm.CurrentTheme);
        Assert.Same(_mockNavigationService.Object, vm.NavigationService);
        Assert.Same(_mockPresentationStateService.Object, vm.PresentationStateService);
    }

    [Fact]
    public void CoffeeUrl_MatchesConfiguredPayPalAddress()
    {
        Assert.Equal("https://paypal.me/itsjczamora", MainViewModel.CoffeeUrl);
    }

    [Fact]
    public void OpenCoffeeLinkCommand_ExistsAndIsExecutable()
    {
        var vm = new MainViewModel(
            _mockNavigationService.Object,
            _mockSettingsService.Object,
            _mockPresentationStateService.Object);

        Assert.NotNull(vm.OpenCoffeeLinkCommand);
        Assert.True(vm.OpenCoffeeLinkCommand.CanExecute(null));
    }

    [Fact]
    public void ThemeChangedEvent_UpdatesCurrentTheme()
    {
        var vm = new MainViewModel(
            _mockNavigationService.Object,
            _mockSettingsService.Object,
            _mockPresentationStateService.Object);

        _mockSettingsService.Raise(s => s.ThemeChanged += null, _mockSettingsService.Object, ApplicationThemeOption.Light);

        Assert.Equal(ApplicationThemeOption.Light, vm.CurrentTheme);
    }
}
