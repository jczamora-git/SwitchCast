using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Media;
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
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);
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
        Assert.Equal("L", vm.SwitchModeBadge);
        Assert.True(vm.IsModeLiveOnly);
        Assert.False(vm.IsModeActiveAndLive);
        Assert.False(vm.IsModeActiveOnly);
        Assert.False(vm.IsLive);
        Assert.False(vm.IsPaused);
        Assert.False(vm.IsBlackout);
        Assert.False(vm.HasActiveSource);
    }

    [Fact]
    public async Task SetSwitchModeCommand_UpdatesStateServiceAndSavesSettings()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await vm.SetSwitchModeCommand.ExecuteAsync(PresenterSwitchMode.ActiveAndLive);

        _mockStateService.Verify(s => s.SetSwitchMode(PresenterSwitchMode.ActiveAndLive), Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public void SwitchModeBadge_And_Tooltip_ReflectMode()
    {
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);

        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        Assert.Equal("A+L", vm.SwitchModeBadge);
        Assert.Contains("Active + Live", vm.SwitchModeTooltip);
        Assert.True(vm.IsModeActiveAndLive);
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
    public async Task SwitchSourceCommand_CallsCoordinatorExecuteSourceSwitchAsync()
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

        _mockCoordinator.Verify(c => c.ExecuteSourceSwitchAsync(source), Times.Once);
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

        _mockCoordinator.Verify(c => c.StopPresentationAsync(false), Times.Once);
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
    public void VideoPlaybackControls_ExecuteAgainstMediaService()
    {
        var mockMediaService = new Mock<IMediaPresentationService>();
        _mockCoordinator.SetupGet(c => c.MediaPresentationService).Returns(mockMediaService.Object);
        _mockCoordinator.SetupGet(c => c.IsActiveSourceVideo).Returns(true);
        mockMediaService.SetupGet(m => m.Duration).Returns(TimeSpan.FromSeconds(120));
        mockMediaService.SetupGet(m => m.Position).Returns(TimeSpan.FromSeconds(30));
        mockMediaService.SetupGet(m => m.IsVideoPlaying).Returns(true);

        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        Assert.True(vm.IsActiveSourceVideo);
        Assert.True(vm.IsVideoPlaying);
        Assert.Equal(120.0, vm.VideoDurationSeconds);
        Assert.Equal(30.0, vm.VideoPositionSeconds);
        Assert.Equal("00:30 / 02:00", vm.VideoPositionText);
        Assert.Equal("\uE769", vm.VideoPlaybackButtonGlyph); // Pause icon when playing

        // 1. Toggle playback (pause)
        vm.ToggleVideoPlaybackCommand.Execute(null);
        mockMediaService.Verify(m => m.PauseVideo(), Times.Once);

        // 2. Restart video
        vm.RestartVideoCommand.Execute(null);
        mockMediaService.Verify(m => m.RestartVideo(), Times.Once);

        // 3. Seek backward 10s (30s -> 20s)
        vm.SeekBackward10Command.Execute(null);
        mockMediaService.Verify(m => m.Seek(TimeSpan.FromSeconds(20)), Times.Once);

        // 4. Seek forward 10s (30s -> 40s)
        vm.SeekForward10Command.Execute(null);
        mockMediaService.Verify(m => m.Seek(TimeSpan.FromSeconds(40)), Times.Once);

        // 5. Scrubbing
        vm.StartScrubbing(45.0);
        Assert.Equal(45.0, vm.VideoPositionSeconds);
        Assert.Equal("00:45 / 02:00", vm.VideoPositionText);

        vm.CompleteScrubbing(75.0);
        mockMediaService.Verify(m => m.Seek(TimeSpan.FromSeconds(75)), Times.Once);
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

    [Fact]
    public void ShowDashboardCommand_CallsDockServiceShowDashboard()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        vm.ShowDashboardCommand.Execute(null);

        _mockDockService.Verify(d => d.ShowDashboard(), Times.Once);
    }

    [Fact]
    public void SetDispatcherQueue_And_Dispose_ManagesTimerLifecycleSafely()
    {
        var vm = new PresenterDockViewModel(
            _mockCoordinator.Object,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        // Safe to call SetDispatcherQueue with null or non-UI dispatcher in headless test
        vm.SetDispatcherQueue(null);

        // Safe to dispose multiple times
        vm.Dispose();
        vm.Dispose();

        // Calling SetDispatcherQueue after dispose is a safe no-op
        vm.SetDispatcherQueue(null);
    }
}
