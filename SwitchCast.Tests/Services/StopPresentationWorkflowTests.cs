using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Comprehensive unit tests verifying the unified Stop Presenting workflow across all triggers,
/// source types, and presentation states, including window teardown and dashboard activation.
/// </summary>
public class StopPresentationWorkflowTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaPresentationService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<IPresenterDockService> _mockDockService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public StopPresentationWorkflowTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();
        _mockMediaPresentationService = new Mock<IMediaPresentationService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockDockService = new Mock<IPresenterDockService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true });
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>
        {
            new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true },
            new MonitorSource { Id = "mon-1", Title = "Display 1", IsAvailable = true }
        });
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);
        _mockWindowActivationService.Setup(w => w.ActivateMainWindow()).Returns(true);
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
    }

    private PresentationCoordinator CreateCoordinator()
    {
        return new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object,
            _mockNavigationService.Object);
    }

    [Fact]
    public async Task StopFromDashboard_StopsCapture_ClosesOutput_ActivatesDashboard()
    {
        using var coordinator = CreateCoordinator();
        var dashboardVm = new DashboardViewModel(
            _mockStateService.Object,
            _mockNavigationService.Object,
            _mockCaptureCoordinator.Object,
            coordinator,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await dashboardVm.StopPresentationAsync();

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
        _mockNavigationService.Verify(n => n.NavigateToDashboard(null), Times.Once);
    }

    [Fact]
    public async Task StopFromPresenterDock_StopsCapture_ClosesOutput_ActivatesDashboard()
    {
        using var coordinator = CreateCoordinator();
        var dockVm = new PresenterDockViewModel(
            coordinator,
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);

        await dockVm.StopPresentationCommand.ExecuteAsync(null);

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
        _mockNavigationService.Verify(n => n.NavigateToDashboard(null), Times.Once);
    }

    [Fact]
    public async Task StopWhilePresentingWindow_StopsWindowCaptureSafely()
    {
        var windowSource = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(windowSource);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
    }

    [Fact]
    public async Task StopWhilePresentingMonitor_StopsMonitorCaptureSafely()
    {
        var monitorSource = new MonitorSource { Id = "mon-1", Title = "Display 1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(monitorSource);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
    }

    [Fact]
    public async Task StopWhilePresentingImage_ClearsDirectImageSafely()
    {
        var imageSource = new ImageMediaSource { Id = "img-1", Title = "Slide.png", FilePath = @"C:\Test\Slide.png", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(imageSource);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockMediaPresentationService.Verify(m => m.ClearImage(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
    }

    [Fact]
    public async Task StopWhilePlayingVideo_StopsVideoSafely()
    {
        var videoSource = new VideoMediaSource { Id = "vid-1", Title = "Video.mp4", FilePath = @"C:\Test\Video.mp4", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(videoSource);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockMediaPresentationService.Verify(m => m.StopVideoAsync(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
    }

    [Fact]
    public async Task StopWhilePaused_TransitionsDirectlyToIdleAndClosesWindow()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
    }

    [Fact]
    public async Task StopWhileBlackout_TransitionsDirectlyToIdleAndClosesWindow()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Blackout);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
    }

    [Fact]
    public async Task StopWhileOutputAlreadyClosed_IsIdempotentAndSafe()
    {
        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();
        await coordinator.StopPresentationAsync();

        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Exactly(2));
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplicationShutdown_SuppressesMainWindowActivation()
    {
        using var coordinator = CreateCoordinator();
        var lifecycleService = new ApplicationLifecycleService(
            coordinator,
            _mockCaptureCoordinator.Object,
            _mockWindowService.Object,
            _mockDockService.Object,
            new Mock<IHotkeyService>().Object,
            _mockSettingsService.Object);

        await lifecycleService.ExecuteShutdownAsync();

        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.AtLeastOnce());
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Never);
        _mockNavigationService.Verify(n => n.NavigateToDashboard(null), Times.Never);
    }

    [Fact]
    public async Task Stop_DoesNotMutateQueuedSourcesList()
    {
        var queued = new List<CaptureSource>
        {
            new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true },
            new MonitorSource { Id = "mon-1", Title = "Display 1", IsAvailable = true }
        };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queued);

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        Assert.Equal(2, _mockStateService.Object.SelectedSources.Count);
    }
}
