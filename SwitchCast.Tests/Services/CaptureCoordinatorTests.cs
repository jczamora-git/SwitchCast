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
}
