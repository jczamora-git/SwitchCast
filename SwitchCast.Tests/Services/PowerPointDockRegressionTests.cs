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

public class PowerPointDockRegressionTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaService;
    private readonly Mock<IPowerPointPresentationService> _mockPowerPointService;
    private readonly Mock<IPowerPointLiveService> _mockPowerPointLiveService;
    private readonly Mock<ICameraCaptureService> _mockCameraCaptureService;
    private readonly Mock<IPresenterDockService> _mockDockService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public PowerPointDockRegressionTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();
        _mockMediaService = new Mock<IMediaPresentationService>();
        _mockPowerPointService = new Mock<IPowerPointPresentationService>();
        _mockPowerPointLiveService = new Mock<IPowerPointLiveService>();
        _mockCameraCaptureService = new Mock<ICameraCaptureService>();
        _mockDockService = new Mock<IPresenterDockService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
        _mockWindowActivationService.Setup(w => w.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);

        var settings = new UserSettings { AutoOpenPresenterDock = true };
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(settings);
    }

    private PresentationCoordinator CreateCoordinator()
    {
        return new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaService.Object,
            navigationService: null,
            externalDisplayService: null,
            _mockPowerPointService.Object,
            _mockPowerPointLiveService.Object,
            _mockCameraCaptureService.Object,
            _mockDockService.Object,
            _mockSettingsService.Object);
    }

    [Theory]
    [InlineData(SourceType.Window)]
    [InlineData(SourceType.Display)]
    [InlineData(SourceType.Image)]
    [InlineData(SourceType.Video)]
    [InlineData(SourceType.PowerPoint)]
    [InlineData(SourceType.Camera)]
    public async Task StartPresentation_AllSourceTypes_OpensPresenterDock(SourceType type)
    {
        using var coordinator = CreateCoordinator();

        CaptureSource source = type switch
        {
            SourceType.Window => new WindowSource { Id = "win1", Title = "Window", IsAvailable = true },
            SourceType.Display => new MonitorSource { Id = "disp1", Title = "Display", IsAvailable = true },
            SourceType.Image => new ImageMediaSource { Id = "img1", Title = "Image", FilePath = "C:\\test.png", IsAvailable = true },
            SourceType.Video => new VideoMediaSource { Id = "vid1", Title = "Video", FilePath = "C:\\test.mp4", IsAvailable = true },
            SourceType.PowerPoint => new PowerPointSource { Id = "ppt1", Title = "Deck", FilePath = "C:\\test.pptx", SlideCount = 5, IsAvailable = true },
            SourceType.Camera => new CameraSource { Id = "cam1", Title = "Cam", DeviceId = "dev1", IsAvailable = true },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        if (source is CameraSource cam)
        {
            _mockCameraCaptureService.Setup(c => c.StartCaptureAsync(cam, default)).ReturnsAsync(true);
        }

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });

        await coordinator.StartPresentationAsync(source);

        _mockDockService.Verify(d => d.ShowDock(), Times.Once);
        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task StartPresentation_WhenAutoOpenPresenterDockDisabled_DoesNotShowDock()
    {
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings { AutoOpenPresenterDock = false });
        using var coordinator = CreateCoordinator();

        var source = new WindowSource { Id = "win1", Title = "Window", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source });

        await coordinator.StartPresentationAsync(source);

        _mockDockService.Verify(d => d.ShowDock(), Times.Never);
        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
    }

    [Fact]
    public async Task SwitchPresentationSource_FromCameraToStaticPowerPoint_SucceedsAndStopsCamera()
    {
        using var coordinator = CreateCoordinator();

        var cameraSource = new CameraSource { Id = "cam1", Title = "Webcam", DeviceId = "dev1", IsAvailable = true };
        _mockCameraCaptureService.Setup(c => c.StartCaptureAsync(cameraSource, default)).ReturnsAsync(true);
        _mockCameraCaptureService.Setup(c => c.StopCaptureAsync()).Returns(Task.CompletedTask);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { cameraSource });

        await coordinator.StartPresentationAsync(cameraSource);
        _mockCameraCaptureService.Verify(c => c.StartCaptureAsync(cameraSource, default), Times.Once);

        // Switch to static PowerPoint
        var pptSource = new PowerPointSource
        {
            Id = "ppt1",
            Title = "Demo",
            FilePath = "C:\\Demo.pptx",
            SlideCount = 3,
            SlideImagePaths = new List<string> { "C:\\slide_1.png", "C:\\slide_2.png", "C:\\slide_3.png" },
            CurrentSlideIndex = 1,
            IsAvailable = true
        };

        _mockPowerPointService.Setup(p => p.LoadPresentationAsync(pptSource)).Returns(Task.CompletedTask);

        await coordinator.SwitchPresentationSourceAsync(pptSource);

        _mockCameraCaptureService.Verify(c => c.StopCaptureAsync(), Times.Once);
        _mockPowerPointService.Verify(p => p.LoadPresentationAsync(pptSource), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(pptSource), Times.Once);
    }

    [Fact]
    public async Task SwitchPresentationSource_FromCameraToPowerPointLive_StartsLiveSlideshow()
    {
        using var coordinator = CreateCoordinator();

        var cameraSource = new CameraSource { Id = "cam1", Title = "Webcam", DeviceId = "dev1", IsAvailable = true };
        _mockCameraCaptureService.Setup(c => c.StartCaptureAsync(cameraSource, default)).ReturnsAsync(true);
        _mockCameraCaptureService.Setup(c => c.StopCaptureAsync()).Returns(Task.CompletedTask);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { cameraSource });

        await coordinator.StartPresentationAsync(cameraSource);

        var pptLiveSource = new PowerPointSource
        {
            Id = "ppt_live",
            Title = "Live Deck",
            FilePath = "C:\\Demo.pptx",
            IsLiveSlideshow = true,
            IsAvailable = true
        };

        _mockPowerPointLiveService.Setup(p => p.StartSlideshowAsync(pptLiveSource.FilePath, default)).ReturnsAsync(true);
        _mockPowerPointLiveService.SetupGet(p => p.SlideshowWindowHandle).Returns((nint)0x1234);

        await coordinator.SwitchPresentationSourceAsync(pptLiveSource);

        _mockCameraCaptureService.Verify(c => c.StopCaptureAsync(), Times.Once);
        _mockPowerPointLiveService.Verify(p => p.StartSlideshowAsync(pptLiveSource.FilePath, default), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(It.Is<WindowSource>(w => w.WindowHandle == (nint)0x1234)), Times.Once);
    }

    [Fact]
    public async Task SwitchPresentationSource_DoesNotReopenManuallyClosedDock()
    {
        using var coordinator = CreateCoordinator();

        var source1 = new WindowSource { Id = "win1", Title = "Window 1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source1 });

        await coordinator.StartPresentationAsync(source1);
        _mockDockService.Verify(d => d.ShowDock(), Times.Once);

        // Reset mock invocation count to simulate user manually closing the dock
        _mockDockService.Invocations.Clear();

        var source2 = new PowerPointSource
        {
            Id = "ppt1",
            Title = "Deck",
            FilePath = "C:\\test.pptx",
            SlideCount = 3,
            SlideImagePaths = new List<string> { "C:\\slide_1.png" },
            IsAvailable = true
        };

        await coordinator.SwitchPresentationSourceAsync(source2);

        // Switching must NOT call ShowDock again
        _mockDockService.Verify(d => d.ShowDock(), Times.Never);
    }

    [Fact]
    public async Task StopPresentation_AndSubsequentStart_ReopensPresenterDock()
    {
        using var coordinator = CreateCoordinator();

        var source1 = new WindowSource { Id = "win1", Title = "Window 1", IsAvailable = true };
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { source1 });

        await coordinator.StartPresentationAsync(source1);
        _mockDockService.Verify(d => d.ShowDock(), Times.Once);

        await coordinator.StopPresentationAsync();
        _mockWindowService.Verify(w => w.ClosePresentationWindow(), Times.Once);

        // Subsequent start should trigger ShowDock again
        await coordinator.StartPresentationAsync(source1);
        _mockDockService.Verify(d => d.ShowDock(), Times.Exactly(2));
    }

    [Fact]
    public async Task SwitchPresentationSource_FailedSwitch_KeepsSourceQueuedAndTransitionsToError()
    {
        using var coordinator = CreateCoordinator();

        var pptSource = new PowerPointSource
        {
            Id = "ppt_fail",
            Title = "Failing Deck",
            FilePath = "C:\\bad.pptx",
            IsAvailable = true
        };

        _mockPowerPointService.Setup(p => p.LoadPresentationAsync(pptSource))
            .ThrowsAsync(new System.Runtime.InteropServices.COMException("COM error", unchecked((int)0x80004005)));

        await Assert.ThrowsAsync<System.Runtime.InteropServices.COMException>(() => coordinator.SwitchPresentationSourceAsync(pptSource));

        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Error), Times.Once);
        Assert.NotNull(coordinator.LastErrorMessage);
    }

    [Fact]
    public void PresenterDockViewModel_PowerPointRowVisibility_VisibleOnlyWhenPowerPointActive()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        mockCoord.SetupGet(c => c.IsActiveSourcePowerPoint).Returns(true);
        mockCoord.SetupGet(c => c.IsActiveSourceVideo).Returns(false);

        using var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.PowerPointRowVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.VideoPlaybackRowVisibility);
        Assert.True(vm.HasSecondaryRow);
    }

    [Fact]
    public void PresenterDockViewModel_VideoPlaybackRowVisibility_VisibleOnlyWhenVideoActive()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        mockCoord.SetupGet(c => c.IsActiveSourcePowerPoint).Returns(false);
        mockCoord.SetupGet(c => c.IsActiveSourceVideo).Returns(true);

        using var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.PowerPointRowVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.VideoPlaybackRowVisibility);
        Assert.True(vm.HasSecondaryRow);
    }

    [Theory]
    [InlineData(false, false)]
    public void PresenterDockViewModel_NoSecondaryRow_ForCameraWindowDisplayImage(bool isPowerPoint, bool isVideo)
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        mockCoord.SetupGet(c => c.IsActiveSourcePowerPoint).Returns(isPowerPoint);
        mockCoord.SetupGet(c => c.IsActiveSourceVideo).Returns(isVideo);

        using var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.PowerPointRowVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.VideoPlaybackRowVisibility);
        Assert.False(vm.HasSecondaryRow);
    }

    [Fact]
    public async Task PresenterDockViewModel_GuardedCommands_DoNotThrowUnhandledExceptions()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        mockCoord.Setup(c => c.ExecuteSourceSwitchAsync(It.IsAny<CaptureSource>()))
            .ThrowsAsync(new System.Runtime.InteropServices.COMException("Test COM error", unchecked((int)0x8001010E)));
        mockCoord.Setup(c => c.PowerPointNextSlideAsync())
            .ThrowsAsync(new System.Runtime.InteropServices.COMException("Test COM error", unchecked((int)0x8001010E)));
        mockCoord.Setup(c => c.PowerPointPreviousSlideAsync())
            .ThrowsAsync(new System.Runtime.InteropServices.COMException("Test COM error", unchecked((int)0x8001010E)));

        using var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        var source = new PowerPointSource { Id = "ppt1", Title = "Deck", FilePath = "C:\\test.pptx", IsAvailable = true };

        // Should not throw
        await vm.SwitchSourceCommand.ExecuteAsync(source);
        await vm.PowerPointNextSlideCommand.ExecuteAsync(null);
        await vm.PowerPointPreviousSlideCommand.ExecuteAsync(null);
    }
}
