using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using Windows.Graphics.Capture;
using Xunit;

namespace SwitchCast.Tests.Services;

public class CaptureCoordinatorTests
{
    private readonly Mock<IGraphicsCaptureItemFactory> _mockItemFactory;
    private readonly Mock<IDirect3D11DeviceProvider> _mockDeviceProvider;
    private readonly Mock<ICaptureSessionManager> _mockSessionManager;
    private readonly Mock<ICapturePreviewRenderer> _mockPreviewRenderer;
    private readonly Mock<IPresentationStateService> _mockPresentationStateService;

    public CaptureCoordinatorTests()
    {
        _mockItemFactory = new Mock<IGraphicsCaptureItemFactory>();
        _mockDeviceProvider = new Mock<IDirect3D11DeviceProvider>();
        _mockSessionManager = new Mock<ICaptureSessionManager>();
        _mockPreviewRenderer = new Mock<ICapturePreviewRenderer>();
        _mockPresentationStateService = new Mock<IPresentationStateService>();
    }

    [Fact]
    public void InitialState_IsIdle_AndCurrentPreviewSourceNull()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        Assert.Equal(CaptureState.Idle, coordinator.State);
        Assert.Null(coordinator.CurrentPreviewSource);
        Assert.Null(coordinator.LastErrorMessage);
    }

    [Fact]
    public async Task StartPreviewAsync_WhenSourceUnavailable_ThrowsInvalidOperationException()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        var source = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = false };

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartPreviewAsync(source));
        Assert.Equal(CaptureState.Failed, coordinator.State);
    }

    [Fact]
    public async Task StopPreviewAsync_WhenAlreadyIdle_CompletesSafely()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StopPreviewAsync();

        Assert.Equal(CaptureState.Idle, coordinator.State);
        _mockSessionManager.Verify(s => s.StopCapture(), Times.Once);
        _mockPreviewRenderer.Verify(r => r.Clear(), Times.Once);
    }

    [Fact]
    public void SessionManager_CaptureError_TransitionsStateToFailed()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        // Raise CaptureError on session manager
        _mockSessionManager.Raise(s => s.CaptureError += null, _mockSessionManager.Object, new Exception("GPU Device Lost"));

        Assert.Equal(CaptureState.Failed, coordinator.State);
        Assert.Contains("GPU Device Lost", coordinator.LastErrorMessage);
    }

    [Fact]
    public void SessionManager_SourceClosed_TransitionsStateToFailed()
    {
        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        // Raise SourceClosed on session manager
        _mockSessionManager.Raise(s => s.SourceClosed += null, _mockSessionManager.Object, EventArgs.Empty);

        Assert.Equal(CaptureState.Failed, coordinator.State);
        Assert.Contains("closed or removed", coordinator.LastErrorMessage);
    }

    [Fact]
    public async Task SwitchPreviewSourceAsync_ValidSource_SwitchesSession()
    {
        var source1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var source2 = new WindowSource { Id = "win-2", Title = "Window 2", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(source1);
        Assert.Equal(CaptureState.Capturing, coordinator.State);

        await coordinator.SwitchPreviewSourceAsync(source2);
        Assert.Equal(CaptureState.Capturing, coordinator.State);
        Assert.Equal(source2.Id, coordinator.CurrentPreviewSource?.Id);
        _mockPresentationStateService.Verify(s => s.SetActiveSource(source2), Times.Once);
    }

    [Fact]
    public async Task FrameArrived_WithSharedBitmap_RendersAndPropagatesEvent()
    {
        var source = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };

        using var coordinator = new CaptureCoordinator(
            _mockItemFactory.Object,
            _mockDeviceProvider.Object,
            _mockSessionManager.Object,
            _mockPreviewRenderer.Object,
            _mockPresentationStateService.Object);

        await coordinator.StartPreviewAsync(source);

        using var rawBitmap = new Windows.Graphics.Imaging.SoftwareBitmap(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, 10, 10, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
        using var sharedBitmap = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        bool eventFired = false;
        coordinator.FrameArrived += (s, e) =>
        {
            eventFired = true;
            Assert.Same(sharedBitmap, e.SharedBitmap);
        };

        // Raise FrameArrived on session manager
        _mockSessionManager.Raise(s => s.FrameArrived += null, _mockSessionManager.Object, new FrameArrivedEventArgs(sharedBitmap, 1));

        Assert.True(eventFired);
        _mockPreviewRenderer.Verify(r => r.RenderSharedBitmapAsync(sharedBitmap, It.IsAny<long>()), Times.Once);
    }
}
