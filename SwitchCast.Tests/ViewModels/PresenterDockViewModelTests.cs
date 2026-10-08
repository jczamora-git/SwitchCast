using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class PresenterDockViewModelTests
{
    private readonly Mock<IPresentationCoordinator> _mockCoordinator;
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<IPresenterDockService> _mockDockService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public PresenterDockViewModelTests()
    {
        _mockCoordinator = new Mock<IPresentationCoordinator>();
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockDockService = new Mock<IPresenterDockService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
    }

    [Fact]
    public void InitialState_ReflectsServices()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        Assert.Equal(PresentationStatus.Idle, vm.Status);
        Assert.Equal("STANDBY", vm.StatusDisplayText);
        Assert.Equal("No Active Source", vm.ActiveSourceTitle);
        Assert.False(vm.IsLive);
        Assert.False(vm.IsPaused);
        Assert.False(vm.IsBlackout);
        Assert.False(vm.HasActiveSource);
    }

    [Fact]
    public async Task NextSourceCommand_CallsCoordinatorSwitchToNextSourceAsync()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.NextSourceCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.SwitchToNextSourceAsync(), Times.Once);
    }

    [Fact]
    public async Task PreviousSourceCommand_CallsCoordinatorSwitchToPreviousSourceAsync()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.PreviousSourceCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.SwitchToPreviousSourceAsync(), Times.Once);
    }

    [Fact]
    public async Task SwitchSourceCommand_CallsCoordinatorSwitchPresentationSourceAsync()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        var source = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        await vm.SwitchSourceCommand.ExecuteAsync(source);

        _mockCoordinator.Verify(c => c.SwitchPresentationSourceAsync(source), Times.Once);
    }

    [Fact]
    public async Task TogglePauseCommand_WhenLive_CallsPausePresentationAsync()
    {
        _mockCoordinator.SetupGet(c => c.IsLive).Returns(true);
        _mockCoordinator.SetupGet(c => c.IsPaused).Returns(false);

        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.PausePresentationAsync(), Times.Once);
    }

    [Fact]
    public async Task TogglePauseCommand_WhenPaused_CallsResumePresentationAsync()
    {
        _mockCoordinator.SetupGet(c => c.IsLive).Returns(false);
        _mockCoordinator.SetupGet(c => c.IsPaused).Returns(true);

        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.TogglePauseCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.ResumePresentationAsync(), Times.Once);
    }

    [Fact]
    public async Task ToggleBlackoutCommand_CallsCoordinatorToggleBlackoutAsync()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.ToggleBlackoutCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.ToggleBlackoutAsync(), Times.Once);
    }

    [Fact]
    public async Task StopPresentationCommand_CallsCoordinatorStopPresentationAsync()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.StopPresentationCommand.ExecuteAsync(null);

        _mockCoordinator.Verify(c => c.StopPresentationAsync(), Times.Once);
    }

    [Fact]
    public void ShowOutputWindowCommand_CallsWindowServiceShowPresentationWindow()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        vm.ShowOutputWindowCommand.Execute(null);

        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
    }

    [Fact]
    public void ToggleCompactModeCommand_TogglesIsCompactMode()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        Assert.False(vm.IsCompactMode);

        vm.ToggleCompactModeCommand.Execute(null);
        Assert.True(vm.IsCompactMode);

        vm.ToggleCompactModeCommand.Execute(null);
        Assert.False(vm.IsCompactMode);
    }

    [Fact]
    public void CloseDockCommand_CallsDockServiceCloseDock()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        vm.CloseDockCommand.Execute(null);

        _mockDockService.Verify(d => d.CloseDock(), Times.Once);
    }
}
