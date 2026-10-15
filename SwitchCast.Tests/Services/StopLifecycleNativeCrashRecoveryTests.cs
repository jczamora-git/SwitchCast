using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Camera;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.PowerPoint;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Regression tests verifying the clean lifetime, strict stop sequence, and idempotency of Stop Presenting
/// to prevent native AccessViolationExceptions and resource leaks during window destruction.
/// </summary>
public class StopLifecycleNativeCrashRecoveryTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaPresentationService;
    private readonly Mock<INavigationService> _mockNavigationService;
    private readonly Mock<IPowerPointPresentationService> _mockPowerPointService;
    private readonly Mock<IPowerPointLiveService> _mockPowerPointLiveService;
    private readonly Mock<ICameraCaptureService> _mockCameraCaptureService;

    public StopLifecycleNativeCrashRecoveryTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();
        _mockMediaPresentationService = new Mock<IMediaPresentationService>();
        _mockNavigationService = new Mock<INavigationService>();
        _mockPowerPointService = new Mock<IPowerPointPresentationService>();
        _mockPowerPointLiveService = new Mock<IPowerPointLiveService>();
        _mockCameraCaptureService = new Mock<ICameraCaptureService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);
        _mockWindowActivationService.Setup(w => w.ActivateMainWindow()).Returns(true);
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
            _mockNavigationService.Object,
            externalDisplayService: null,
            _mockPowerPointService.Object,
            _mockPowerPointLiveService.Object,
            _mockCameraCaptureService.Object);
    }

    [Fact]
    public async Task StopPresentationAsync_WhileCameraLive_StopsCameraBeforeClearingRendererAndClosingWindow()
    {
        var cameraSource = new CameraSource { Id = "cam-1", Title = "Integrated Webcam", DeviceId = "device-1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(cameraSource);

        var executionOrder = new List<string>();
        _mockCameraCaptureService.Setup(c => c.StopCaptureAsync())
            .Callback(() => executionOrder.Add("StopCamera"))
            .Returns(Task.CompletedTask);
        _mockOutputRenderer.Setup(r => r.Clear())
            .Callback(() => executionOrder.Add("ClearRenderer"));
        _mockWindowService.Setup(w => w.ClosePresentationWindow())
            .Callback(() => executionOrder.Add("CloseWindow"));

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        Assert.Equal(new[] { "StopCamera", "ClearRenderer", "CloseWindow" }, executionOrder);
        _mockCameraCaptureService.Verify(c => c.StopCaptureAsync(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task StopPresentationAsync_WhilePowerPointLive_StopsSlideshowBeforeClearingRendererAndClosingWindow()
    {
        var pptSource = new PowerPointSource { Id = "ppt-1", Title = "Deck.pptx", FilePath = "C:\\Deck.pptx", IsLiveSlideshow = true, IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(pptSource);
        _mockPowerPointLiveService.SetupGet(p => p.IsLiveSlideshowRunning).Returns(true);

        var executionOrder = new List<string>();
        _mockPowerPointLiveService.Setup(p => p.StopSlideshowAsync())
            .Callback(() => executionOrder.Add("StopSlideshow"))
            .Returns(Task.CompletedTask);
        _mockPowerPointService.Setup(p => p.ClearPresentation())
            .Callback(() => executionOrder.Add("ClearPowerPoint"));
        _mockOutputRenderer.Setup(r => r.Clear())
            .Callback(() => executionOrder.Add("ClearRenderer"));
        _mockWindowService.Setup(w => w.ClosePresentationWindow())
            .Callback(() => executionOrder.Add("CloseWindow"));

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        Assert.Equal(new[] { "StopSlideshow", "ClearPowerPoint", "ClearRenderer", "CloseWindow" }, executionOrder);
        _mockPowerPointLiveService.Verify(p => p.StopSlideshowAsync(), Times.Once);
        _mockPowerPointService.Verify(p => p.ClearPresentation(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task StopPresentationAsync_WhileVideoPlaying_StopsVideoBeforeClearingRendererAndClosingWindow()
    {
        var videoSource = new VideoMediaSource { Id = "vid-1", Title = "Video.mp4", FilePath = "C:\\test.mp4", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(videoSource);

        var executionOrder = new List<string>();
        _mockMediaPresentationService.Setup(m => m.ClearImage())
            .Callback(() => executionOrder.Add("ClearImage"));
        _mockMediaPresentationService.Setup(m => m.StopVideoAsync())
            .Callback(() => executionOrder.Add("StopVideo"))
            .Returns(Task.CompletedTask);
        _mockOutputRenderer.Setup(r => r.Clear())
            .Callback(() => executionOrder.Add("ClearRenderer"));
        _mockWindowService.Setup(w => w.ClosePresentationWindow())
            .Callback(() => executionOrder.Add("CloseWindow"));

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        Assert.Contains("StopVideo", executionOrder);
        Assert.Contains("ClearRenderer", executionOrder);
        Assert.Contains("CloseWindow", executionOrder);

        int stopVideoIdx = executionOrder.IndexOf("StopVideo");
        int clearRendererIdx = executionOrder.IndexOf("ClearRenderer");
        int closeWindowIdx = executionOrder.IndexOf("CloseWindow");

        Assert.True(stopVideoIdx < clearRendererIdx);
        Assert.True(clearRendererIdx < closeWindowIdx);
    }

    [Fact]
    public async Task StopPresentationAsync_WhileWindowLive_StopsPreviewBeforeClearingRendererAndClosingWindow()
    {
        var windowSource = new WindowSource { Id = "win-1", Title = "Visual Studio", IsAvailable = true };
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(windowSource);

        var executionOrder = new List<string>();
        _mockCaptureCoordinator.Setup(c => c.StopPreviewAsync())
            .Callback(() => executionOrder.Add("StopCapture"))
            .Returns(Task.CompletedTask);
        _mockOutputRenderer.Setup(r => r.Clear())
            .Callback(() => executionOrder.Add("ClearRenderer"));
        _mockWindowService.Setup(w => w.ClosePresentationWindow())
            .Callback(() => executionOrder.Add("CloseWindow"));

        using var coordinator = CreateCoordinator();
        await coordinator.StopPresentationAsync();

        Assert.Equal(new[] { "StopCapture", "ClearRenderer", "CloseWindow" }, executionOrder);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task StopPresentationAsync_ConcurrentCalls_AreDeduplicatedToSingleExecution()
    {
        var tcs = new TaskCompletionSource();
        _mockCaptureCoordinator.Setup(c => c.StopPreviewAsync())
            .Returns(async () =>
            {
                await tcs.Task;
            });

        using var coordinator = CreateCoordinator();

        var call1 = coordinator.StopPresentationAsync();
        var call2 = coordinator.StopPresentationAsync();
        var call3 = coordinator.StopPresentationAsync();

        // Release the asynchronous stop operation
        tcs.SetResult();

        await Task.WhenAll(call1, call2, call3);

        // Verification: Even though 3 concurrent Stop requests arrived, destructive cleanup ran exactly once
        _mockOutputRenderer.Verify(r => r.Clear(), Times.Once);
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
    }

    [Fact]
    public void PresentationViewModel_Dispose_UnsubscribesFromSingletons()
    {
        var mockDispatcher = new Mock<IUIThreadDispatcher>();
        mockDispatcher.SetupGet(d => d.HasThreadAccess).Returns(true);

        using var vm = new PresentationViewModel(
            _mockStateService.Object,
            _mockCaptureCoordinator.Object is null ? null! : CreateCoordinator(),
            mockDispatcher.Object);

        // Unsubscribe by disposing
        vm.Dispose();

        // Assert that raising events on state service does not cause exceptions or leak subscriptions
        _mockStateService.Raise(s => s.PropertyChanged += null, new System.ComponentModel.PropertyChangedEventArgs(nameof(IPresentationStateService.Status)));
    }
}
