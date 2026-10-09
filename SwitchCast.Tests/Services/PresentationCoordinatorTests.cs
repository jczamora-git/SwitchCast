using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresentationCoordinatorTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;

    public PresentationCoordinatorTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
        _mockWindowActivationService.Setup(w => w.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);
    }

    [Fact]
    public void InitialState_ReflectsInjectedServices()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        Assert.Equal(PresentationStatus.Idle, coordinator.Status);
        Assert.False(coordinator.IsOutputWindowOpen);
        Assert.False(coordinator.IsLive);
        Assert.False(coordinator.IsPaused);
        Assert.False(coordinator.IsBlackout);
        Assert.Null(coordinator.CurrentPresentationSource);
        Assert.Equal(PresenterSwitchMode.LiveOnly, coordinator.SwitchMode);
    }

    [Fact]
    public async Task OpenOutputWindowAsync_CallsWindowService()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.OpenOutputWindowAsync();

        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task CloseOutputWindowAsync_CallsWindowService()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.CloseOutputWindowAsync();

        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task StartPresentationAsync_ValidSource_StartsCaptureAndSetsActive()
    {
        var source = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.StartPresentationAsync(source);

        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(source), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(source), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
        _mockOutputRenderer.Verify(r => r.Resume(), Times.Once);
    }

    [Fact]
    public async Task StartPresentationAsync_NoAvailableSource_ThrowsException()
    {
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartPresentationAsync(null));
    }

    [Fact]
    public async Task StopPresentationAsync_ClearsRenderer_ResetsStatus_ClosesOutputWindow_AndActivatesMainWindow()
    {
        var mockNav = new Mock<INavigationService>();

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            mediaPresentationService: null,
            navigationService: mockNav.Object);

        await coordinator.StopPresentationAsync();

        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
        mockNav.Verify(n => n.NavigateToDashboard(null), Times.Once);
    }

    [Fact]
    public async Task StopPresentationAsync_WhenShuttingDown_SuppressesMainWindowActivation()
    {
        var mockNav = new Mock<INavigationService>();

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            mediaPresentationService: null,
            navigationService: mockNav.Object);

        await coordinator.StopPresentationAsync(isShuttingDown: true);

        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Never);
        mockNav.Verify(n => n.NavigateToDashboard(null), Times.Never);
    }

    [Fact]
    public async Task PausePresentationAsync_WhenActive_FreezesRendererAndSetsPaused()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.PausePresentationAsync();

        _mockOutputRenderer.Verify(r => r.Freeze(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Paused), Times.Once);
    }

    [Fact]
    public async Task ResumePresentationAsync_WhenPaused_ResumesRendererAndSetsActive()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.ResumePresentationAsync();

        _mockOutputRenderer.Verify(r => r.Resume(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
    }

    [Fact]
    public async Task ToggleBlackoutAsync_TransitionsToBlackout_AndRestoresPreviousState()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        // Toggle to Blackout
        await coordinator.ToggleBlackoutAsync();
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Blackout), Times.Once);

        // Update mock state to Blackout
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Blackout);

        // Toggle back from Blackout
        await coordinator.ToggleBlackoutAsync();
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
        _mockOutputRenderer.Verify(r => r.Resume(), Times.Once);
    }

    [Fact]
    public async Task SwitchPresentationSourceAsync_SwitchesCaptureSourceAndUpdatesActive()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var source2 = new WindowSource { Id = "win-2", Title = "Visual Studio", IsAvailable = true };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.SwitchPresentationSourceAsync(source2);

        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(source2), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(source2), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveAndLive_ActivatesWindowAndSwitchesPresentation()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "PowerPoint", IsAvailable = true, WindowHandle = 0x1001 };
        var source2 = new WindowSource { Id = "win-2", Title = "Chrome", IsAvailable = true, WindowHandle = 0x1002 };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(source2);

        _mockStateService.Verify(s => s.SetSelectedSource(source2), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(source2), Times.Once);
        _mockStateService.Verify(s => s.SetForegroundSource(source2), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(source2), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveOnly_ActivatesWindowWithoutSwitchingPresentation()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "PowerPoint", IsAvailable = true, WindowHandle = 0x1001 };
        var source2 = new WindowSource { Id = "win-2", Title = "VS Code", IsAvailable = true, WindowHandle = 0x1002 };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(source2);

        _mockStateService.Verify(s => s.SetSelectedSource(source2), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(source2), Times.Once);
        _mockStateService.Verify(s => s.SetForegroundSource(source2), Times.Once);

        // Verify capture was NOT changed
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(It.IsAny<CaptureSource>()), Times.Never);
        _mockStateService.Verify(s => s.SetActiveSource(source2), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_LiveOnly_SwitchesPresentationWithoutActivatingWindow()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "PowerPoint", IsAvailable = true, WindowHandle = 0x1001 };
        var source2 = new WindowSource { Id = "win-2", Title = "Chrome", IsAvailable = true, WindowHandle = 0x1002 };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(source1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(source2);

        _mockStateService.Verify(s => s.SetSelectedSource(source2), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(It.IsAny<CaptureSource>()), Times.Never);
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(source2), Times.Once);
    }

    [Fact]
    public async Task SetSwitchModeAsync_UpdatesStateServiceSwitchMode()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.SetSwitchModeAsync(PresenterSwitchMode.ActiveAndLive);

        _mockStateService.Verify(s => s.SetSwitchMode(PresenterSwitchMode.ActiveAndLive), Times.Once);
    }

    [Fact]
    public void OnCaptureFrameArrived_WhenActive_RendersSharedBitmapToOutput()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        using var rawBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        using var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Raise FrameArrived on capture coordinator
        _mockCaptureCoordinator.Raise(c => c.FrameArrived += null, _mockCaptureCoordinator.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        _mockOutputRenderer.Verify(r => r.RenderSharedBitmapAsync(sharedBitmap, It.IsAny<long>()), Times.Once);
    }

    [Fact]
    public void OnCaptureFrameArrived_WhenNotActive_DoesNotRenderToOutput()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        using var rawBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        using var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        _mockCaptureCoordinator.Raise(c => c.FrameArrived += null, _mockCaptureCoordinator.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        _mockOutputRenderer.Verify(r => r.RenderSharedBitmapAsync(It.IsAny<RefCountedSoftwareBitmap>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task SwitchToNextSourceAsync_CyclesThroughQueuedSources()
    {
        var src1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var src2 = new WindowSource { Id = "win-2", Title = "Window 2", IsAvailable = true };
        var src3 = new WindowSource { Id = "win-3", Title = "Window 3", IsAvailable = true };
        var sources = new List<CaptureSource> { src1, src2, src3 };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(src1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.SwitchToNextSourceAsync();
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(src2), Times.Once);
    }

    [Fact]
    public async Task SwitchToPreviousSourceAsync_CyclesBackwardThroughQueuedSources()
    {
        var src1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var src2 = new WindowSource { Id = "win-2", Title = "Window 2", IsAvailable = true };
        var src3 = new WindowSource { Id = "win-3", Title = "Window 3", IsAvailable = true };
        var sources = new List<CaptureSource> { src1, src2, src3 };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(src1);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.SwitchToPreviousSourceAsync();
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(src3), Times.Once);
    }

    [Fact]
    public async Task SwitchToSourceIndexAsync_SwitchesToTargetSlot()
    {
        var src1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var src2 = new WindowSource { Id = "win-2", Title = "Window 2", IsAvailable = true };
        var sources = new List<CaptureSource> { src1, src2 };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(sources);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object);

        await coordinator.SwitchToSourceIndexAsync(1);
        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(src2), Times.Once);
    }
}
