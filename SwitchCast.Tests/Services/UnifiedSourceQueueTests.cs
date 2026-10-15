using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.PowerPoint;
using SwitchCast.Services.Camera;
using Xunit;

namespace SwitchCast.Tests.Services;

public class UnifiedSourceQueueTests
{
    private readonly PresentationStateService _stateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaService;
    private readonly Mock<IPowerPointPresentationService> _mockPowerPointService;
    private readonly Mock<IPowerPointLiveService> _mockPowerPointLiveService;
    private readonly Mock<ICameraCaptureService> _mockCameraCaptureService;
    private readonly PresentationCoordinator _coordinator;

    public UnifiedSourceQueueTests()
    {
        _stateService = new PresentationStateService();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockActivationService = new Mock<IWindowActivationService>();
        _mockMediaService = new Mock<IMediaPresentationService>();
        _mockPowerPointService = new Mock<IPowerPointPresentationService>();
        _mockPowerPointLiveService = new Mock<IPowerPointLiveService>();
        _mockCameraCaptureService = new Mock<ICameraCaptureService>();

        _mockWindowService.Setup(w => w.IsWindowOpen).Returns(true);
        _mockActivationService.Setup(a => a.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);

        _coordinator = new PresentationCoordinator(
            _stateService,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockActivationService.Object,
            _mockMediaService.Object,
            null,
            null,
            _mockPowerPointService.Object,
            _mockPowerPointLiveService.Object,
            _mockCameraCaptureService.Object);
    }

    [Fact]
    public async Task UnifiedSourceQueue_CyclesThroughAllSixSourceTypes()
    {
        // Assemble 6 different source types into the authoritative queue
        var windowSource = new WindowSource { Id = "win_1", Title = "Edge Browser", WindowHandle = new nint(1234), IsAvailable = true };
        var monitorSource = new MonitorSource { Id = "mon_1", Title = "Display 2", DeviceName = @"\\.\DISPLAY2", IsAvailable = true };
        var imageSource = new ImageMediaSource { Id = "img_1", Title = "Diagram.png", FilePath = @"C:\Diagram.png", IsAvailable = true };
        var videoSource = new VideoMediaSource { Id = "vid_1", Title = "Demo.mp4", FilePath = @"C:\Demo.mp4", IsAvailable = true };
        var pptSource = new PowerPointSource(@"C:\Deck.pptx") { Id = "ppt_1", Title = "Deck.pptx", SlideCount = 5, IsAvailable = true };
        var cameraSource = new CameraSource { Id = "cam_1", Title = "Webcam", DeviceId = "usb_cam_1", IsAvailable = true };

        _stateService.AddSelectedSource(windowSource);
        _stateService.AddSelectedSource(monitorSource);
        _stateService.AddSelectedSource(imageSource);
        _stateService.AddSelectedSource(videoSource);
        _stateService.AddSelectedSource(pptSource);
        _stateService.AddSelectedSource(cameraSource);

        Assert.Equal(6, _stateService.SelectedSourceCount);

        // Start presentation on first source (Window)
        await _coordinator.StartPresentationAsync(windowSource);
        Assert.Equal("win_1", _coordinator.CurrentPresentationSource?.Id);

        // Next -> Monitor
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("mon_1", _coordinator.CurrentPresentationSource?.Id);

        // Next -> Image
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("img_1", _coordinator.CurrentPresentationSource?.Id);

        // Next -> Video
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("vid_1", _coordinator.CurrentPresentationSource?.Id);

        // Next -> PowerPoint
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("ppt_1", _coordinator.CurrentPresentationSource?.Id);
        Assert.True(_coordinator.IsActiveSourcePowerPoint);

        // Next -> Camera
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("cam_1", _coordinator.CurrentPresentationSource?.Id);
        Assert.True(_coordinator.IsActiveSourceCamera);

        // Wrap around -> Window
        await _coordinator.SwitchToNextSourceAsync();
        Assert.Equal("win_1", _coordinator.CurrentPresentationSource?.Id);

        // Previous -> Camera
        await _coordinator.SwitchToPreviousSourceAsync();
        Assert.Equal("cam_1", _coordinator.CurrentPresentationSource?.Id);
    }

    [Fact]
    public async Task PowerPointSlideNavigation_DoesNotAlterSelectedPresentationQueue()
    {
        var pptSource = new PowerPointSource(@"C:\Deck.pptx") { Id = "ppt_1", Title = "Deck.pptx", SlideCount = 10, CurrentSlideIndex = 1, IsAvailable = true };
        var videoSource = new VideoMediaSource { Id = "vid_1", Title = "Demo.mp4", FilePath = @"C:\Demo.mp4", IsAvailable = true };

        _stateService.AddSelectedSource(pptSource);
        _stateService.AddSelectedSource(videoSource);

        await _coordinator.StartPresentationAsync(pptSource);
        Assert.Equal("ppt_1", _coordinator.CurrentPresentationSource?.Id);

        _mockPowerPointService.Setup(p => p.NextSlideAsync()).ReturnsAsync(true);

        // Advance slide in PowerPoint
        bool advanced = await _coordinator.PowerPointNextSlideAsync();
        Assert.True(advanced);

        // Active presentation queue source MUST remain the PowerPoint deck
        Assert.Equal("ppt_1", _coordinator.CurrentPresentationSource?.Id);
    }
}
