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

    public PresentationCoordinatorTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());

        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
    }

    [Fact]
    public void InitialState_ReflectsInjectedServices()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

        Assert.Equal(PresentationStatus.Idle, coordinator.Status);
        Assert.False(coordinator.IsOutputWindowOpen);
        Assert.False(coordinator.IsLive);
        Assert.False(coordinator.IsPaused);
        Assert.False(coordinator.IsBlackout);
        Assert.Null(coordinator.CurrentPresentationSource);
    }

    [Fact]
    public async Task OpenOutputWindowAsync_CallsWindowService()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

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
            _mockOutputRenderer.Object);

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
            _mockOutputRenderer.Object);

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
            _mockOutputRenderer.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartPresentationAsync(null));
    }

    [Fact]
    public async Task StopPresentationAsync_ClearsRendererAndResetsStatus()
    {
        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

        await coordinator.StopPresentationAsync();

        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Idle), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
    }

    [Fact]
    public async Task PausePresentationAsync_WhenActive_FreezesRendererAndSetsPaused()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

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
            _mockOutputRenderer.Object);

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
            _mockOutputRenderer.Object);

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

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

        await coordinator.SwitchPresentationSourceAsync(source2);

        _mockCaptureCoordinator.Verify(c => c.SwitchPreviewSourceAsync(source2), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(source2), Times.Once);
    }

    [Fact]
    public void OnCaptureFrameArrived_WhenActive_RendersSharedBitmapToOutput()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

        using var rawBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        using var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        // Raise FrameArrived on capture coordinator
        _mockCaptureCoordinator.Raise(c => c.FrameArrived += null, _mockCaptureCoordinator.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        _mockOutputRenderer.Verify(r => r.RenderSharedBitmapAsync(sharedBitmap), Times.Once);
    }

    [Fact]
    public void OnCaptureFrameArrived_WhenNotActive_DoesNotRenderToOutput()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object);

        using var rawBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        using var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        _mockCaptureCoordinator.Raise(c => c.FrameArrived += null, _mockCaptureCoordinator.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        _mockOutputRenderer.Verify(r => r.RenderSharedBitmapAsync(It.IsAny<RefCountedSoftwareBitmap>()), Times.Never);
    }
}
