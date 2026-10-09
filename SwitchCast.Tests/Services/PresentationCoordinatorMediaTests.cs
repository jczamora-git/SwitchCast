using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresentationCoordinatorMediaTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IPresentationWindowService> _mockWindowService;
    private readonly Mock<ICaptureCoordinator> _mockCaptureCoordinator;
    private readonly Mock<IPresentationOutputRenderer> _mockOutputRenderer;
    private readonly Mock<IWindowActivationService> _mockWindowActivationService;
    private readonly Mock<IMediaPresentationService> _mockMediaPresentationService;

    public PresentationCoordinatorMediaTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IPresentationWindowService>();
        _mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        _mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        _mockWindowActivationService = new Mock<IWindowActivationService>();
        _mockMediaPresentationService = new Mock<IMediaPresentationService>();

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        _mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(false);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
        _mockWindowActivationService.Setup(w => w.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);
    }

    [Fact]
    public async Task StartPresentationAsync_ImageSource_StopsCaptureAndLoadsImage()
    {
        var image = new ImageMediaSource
        {
            Id = "media:image.png",
            Title = "image.png",
            FilePath = @"C:\image.png",
            IsAvailable = true
        };

        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.StartPresentationAsync(image);

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.StopVideoAsync(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(image), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(image), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
    }

    [Fact]
    public async Task StartPresentationAsync_VideoSource_StopsCaptureClearsImageAndPlaysVideo()
    {
        var video = new VideoMediaSource
        {
            Id = "media:video.mp4",
            Title = "video.mp4",
            FilePath = @"C:\video.mp4",
            IsAvailable = true
        };

        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.StartPresentationAsync(video);

        _mockCaptureCoordinator.Verify(c => c.StopPreviewAsync(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.ClearImage(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(video), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(video), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
    }

    [Fact]
    public async Task SwitchPresentationSourceAsync_FromVideoToWindow_StopsVideoStartsCapture()
    {
        var video = new VideoMediaSource
        {
            Id = "media:video.mp4",
            Title = "video.mp4",
            FilePath = @"C:\video.mp4",
            IsAvailable = true
        };

        var window = new WindowSource
        {
            Id = "win-1",
            Title = "PowerPoint",
            IsAvailable = true
        };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(video);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.SwitchPresentationSourceAsync(window);

        _mockMediaPresentationService.Verify(m => m.ClearImage(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.StopVideoAsync(), Times.Once);
        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(window), Times.Once);
        _mockOutputRenderer.Verify(r => r.Resume(), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(window), Times.Once);
    }

    [Fact]
    public async Task PauseAndResume_WhenVideoActive_ControlsVideoPlayback()
    {
        var video = new VideoMediaSource
        {
            Id = "media:video.mp4",
            Title = "video.mp4",
            FilePath = @"C:\video.mp4",
            IsAvailable = true
        };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(video);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.PausePresentationAsync();

        _mockMediaPresentationService.Verify(m => m.PauseVideo(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Freeze(), Times.Never);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Paused), Times.Once);

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Paused);

        await coordinator.ResumePresentationAsync();

        _mockMediaPresentationService.Verify(m => m.ResumeVideo(), Times.Once);
        _mockOutputRenderer.Verify(r => r.Resume(), Times.Never);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
    }

    [Fact]
    public async Task ToggleBlackout_WhenVideoActive_PausesVideoAndRestoresOnUntoggle()
    {
        var video = new VideoMediaSource
        {
            Id = "media:video.mp4",
            Title = "video.mp4",
            FilePath = @"C:\video.mp4",
            IsAvailable = true
        };

        _mockStateService.SetupGet(s => s.ActiveSource).Returns(video);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ToggleBlackoutAsync();

        _mockMediaPresentationService.Verify(m => m.PauseVideo(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Blackout), Times.Once);

        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Blackout);

        await coordinator.ToggleBlackoutAsync();

        _mockMediaPresentationService.Verify(m => m.ResumeVideo(), Times.Once);
        _mockStateService.Verify(s => s.SetStatus(PresentationStatus.Active), Times.Once);
    }

    [Fact]
    public async Task SwitchToNextSourceAsync_WithMixedQueue_CyclesThroughWindowImageAndVideo()
    {
        var win = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var img = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };

        var queue = new List<CaptureSource> { win, img, vid };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Next from win -> img
        await coordinator.SwitchToNextSourceAsync();
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);

        // Next from img -> vid
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(img);
        await coordinator.SwitchToNextSourceAsync();
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
    }

    [Fact]
    public async Task SwitchToPreviousSourceAsync_With4Sources_CyclesCorrectly()
    {
        var win = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var img = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:demo.mp4", Title = "Demo.mp4", FilePath = @"C:\demo.mp4", IsAvailable = true };
        var mon = new MonitorSource { Id = "mon-1", Title = "Display 1", DeviceName = @"\\.\DISPLAY1", Width = 1920, Height = 1080, IsAvailable = true };

        var queue = new List<CaptureSource> { win, img, vid, mon };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(mon);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Previous from mon -> vid (CaptureCoordinator stops)
        await coordinator.SwitchToPreviousSourceAsync();
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);

        // Previous from vid -> img
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(vid);
        await coordinator.SwitchToPreviousSourceAsync();
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);

        // Previous from img -> win
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(img);
        await coordinator.SwitchToPreviousSourceAsync();
        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(win), Times.Once);
    }

    [Fact]
    public async Task SwitchToSourceIndexAsync_DirectHotkeys_SwitchesDirectlyToMediaSources()
    {
        var win = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var img = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:demo.mp4", Title = "Demo.mp4", FilePath = @"C:\demo.mp4", IsAvailable = true };
        var mon = new MonitorSource { Id = "mon-1", Title = "Display 1", DeviceName = @"\\.\DISPLAY1", Width = 1920, Height = 1080, IsAvailable = true };

        var queue = new List<CaptureSource> { win, img, vid, mon };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Hotkey 2: index 1 -> image
        await coordinator.SwitchToSourceIndexAsync(1);
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);

        // Hotkey 3: index 2 -> video
        await coordinator.SwitchToSourceIndexAsync(2);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);

        // Hotkey 4: index 3 -> monitor
        await coordinator.SwitchToSourceIndexAsync(3);
        _mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(mon), Times.Once);
    }

    [Theory]
    [InlineData(PresenterSwitchMode.ActiveAndLive)]
    [InlineData(PresenterSwitchMode.LiveOnly)]
    public async Task ExecuteSourceSwitchAsync_MediaSource_TakesMediaLive(PresenterSwitchMode mode)
    {
        var img = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(mode);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(img);

        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);
        _mockStateService.Verify(s => s.SetSelectedSource(img), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveOnly_UpdatesSelectedSourceOnlyWithoutSwitchingLiveOutput()
    {
        var img = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(img);

        // In Active Only, live presentation output is NOT changed
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(It.IsAny<ImageMediaSource>()), Times.Never);
        _mockStateService.Verify(s => s.SetSelectedSource(img), Times.Once);
    }

    [Fact]
    public async Task SwitchToNextSourceAsync_WithActiveWindowToVideo_PlaysVideoWithoutWindowActivation()
    {
        var win = new WindowSource { Id = "win-1", Title = "Window 1", WindowHandle = 100, IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var queue = new List<CaptureSource> { win, vid };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.SwitchToNextSourceAsync();

        // Video played, capture stopped, window activation not called on media
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(vid), Times.Never);
        _mockStateService.Verify(s => s.SetForegroundSource(null), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Once);
    }

    [Fact]
    public async Task SwitchToPreviousSourceAsync_WithMixedQueue_CyclesBackwardCorrectly()
    {
        var win = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var img = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var queue = new List<CaptureSource> { win, vid, img };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(img);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Previous from img -> vid
        await coordinator.SwitchToPreviousSourceAsync();
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
    }

    [Fact]
    public async Task SwitchToNextSourceAsync_InActiveOnlyMode_AdvancesSelectedCursorWithoutTakingLive()
    {
        var win = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var queue = new List<CaptureSource> { win, vid };

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.SelectedSource).Returns(win);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.SwitchToNextSourceAsync();

        // Selected source cursor moves to vid, but video is not played live
        _mockStateService.Verify(s => s.SetSelectedSource(vid), Times.Once);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(It.IsAny<VideoMediaSource>()), Times.Never);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveAndLive_WithImageSource_LoadsImageAndActivatesPresentationOutput()
    {
        var img = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2001);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(img);

        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(img), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(img), Times.Never);
        _mockStateService.Verify(s => s.SetForegroundSource(null), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveAndLive_WithVideoSource_PlaysVideoAndActivatesPresentationOutput()
    {
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2002);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(vid), Times.Never);
        _mockStateService.Verify(s => s.SetForegroundSource(null), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveOnly_WithImageSource_ActivatesPresentationOutputWithoutStartingMediaOrChangingActive()
    {
        var win = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var img = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2003);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(img);

        // Verification: Navigation cursor advances, Presentation Output is activated
        _mockStateService.Verify(s => s.SetSelectedSource(img), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);

        // Media is NOT loaded and On Air source is NOT changed
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(It.IsAny<ImageMediaSource>()), Times.Never);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(It.IsAny<VideoMediaSource>()), Times.Never);
        _mockStateService.Verify(s => s.SetActiveSource(img), Times.Never);
        _mockStateService.Verify(s => s.SetForegroundSource(null), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveOnly_WithVideoSource_ActivatesPresentationOutputWithoutStartingMediaOrChangingActive()
    {
        var win = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2004);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        // Verification: Navigation cursor advances, Presentation Output is activated
        _mockStateService.Verify(s => s.SetSelectedSource(vid), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);

        // Video playback is NOT started, not seeked, and On Air source is NOT changed
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(It.IsAny<VideoMediaSource>()), Times.Never);
        _mockMediaPresentationService.Verify(m => m.StopVideoAsync(), Times.Never);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Never);
        _mockStateService.Verify(s => s.SetForegroundSource(null), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_LiveOnly_WithImageSource_LoadsImageWithoutActivatingWindow()
    {
        var img = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2005);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(img);

        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(img), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(img), Times.Once);

        // Window activation is NEVER requested in Live Only mode
        _mockWindowActivationService.Verify(w => w.ActivateWindow(It.IsAny<IntPtr>()), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateSource(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_LiveOnly_WithVideoSource_PlaysVideoWithoutActivatingWindow()
    {
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var outputHwnd = new IntPtr(0x2006);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Once);

        // Window activation is NEVER requested in Live Only mode
        _mockWindowActivationService.Verify(w => w.ActivateWindow(It.IsAny<IntPtr>()), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateSource(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveAndLive_WhenOutputClosed_ShowsPresentationWindow()
    {
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(false);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(vid), Times.Once);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_ActiveOnly_WhenOutputClosed_ShowsPresentationWindowInStandby()
    {
        var win = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(win);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(false);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        // Shows window (Standby screen), but does NOT start video or set ActiveSource
        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Once);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(It.IsAny<VideoMediaSource>()), Times.Never);
        _mockStateService.Verify(s => s.SetActiveSource(vid), Times.Never);
    }

    [Fact]
    public async Task MixedQueueNavigation_ActiveAndLive_CorrectlyActivatesOutputForMediaAndAppsForWindows()
    {
        var chrome = new WindowSource { Id = "win-chrome", Title = "Chrome", WindowHandle = 101, IsAvailable = true };
        var demoVid = new VideoMediaSource { Id = "media:demo.mp4", Title = "Demo.mp4", FilePath = @"C:\demo.mp4", IsAvailable = true };
        var welcomePng = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };
        var vsCode = new WindowSource { Id = "win-vs", Title = "Visual Studio", WindowHandle = 102, IsAvailable = true };

        var queue = new List<CaptureSource> { chrome, demoVid, welcomePng, vsCode };
        var outputHwnd = new IntPtr(0x9999);

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(chrome);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Step 1: Chrome -> Demo.mp4
        await coordinator.SwitchToNextSourceAsync();
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(demoVid), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);

        // Step 2: Demo.mp4 -> Welcome.png
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(demoVid);
        await coordinator.SwitchToNextSourceAsync();
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(welcomePng), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Exactly(2));

        // Step 3: Welcome.png -> Visual Studio
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(welcomePng);
        await coordinator.SwitchToNextSourceAsync();
        _mockWindowActivationService.Verify(w => w.ActivateSource(vsCode), Times.Once);
        // Output window is NOT activated when switching to a window source (target app is activated instead)
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Exactly(2));
    }

    [Fact]
    public async Task MixedQueueNavigation_ActiveOnly_ActivatesOutputForMediaWithoutChangingOnAir()
    {
        var chrome = new WindowSource { Id = "win-chrome", Title = "Chrome", WindowHandle = 101, IsAvailable = true };
        var demoVid = new VideoMediaSource { Id = "media:demo.mp4", Title = "Demo.mp4", FilePath = @"C:\demo.mp4", IsAvailable = true };
        var welcomePng = new ImageMediaSource { Id = "media:welcome.png", Title = "Welcome.png", FilePath = @"C:\welcome.png", IsAvailable = true };
        var vsCode = new WindowSource { Id = "win-vs", Title = "Visual Studio", WindowHandle = 102, IsAvailable = true };

        var queue = new List<CaptureSource> { chrome, demoVid, welcomePng, vsCode };
        var outputHwnd = new IntPtr(0x9999);

        _mockStateService.SetupGet(s => s.SelectedSources).Returns(queue);
        _mockStateService.SetupGet(s => s.SelectedSource).Returns(chrome);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns(chrome);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveOnly);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Step 1: Next -> Demo.mp4
        await coordinator.SwitchToNextSourceAsync();
        _mockStateService.Verify(s => s.SetSelectedSource(demoVid), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);
        _mockMediaPresentationService.Verify(m => m.PlayVideoAsync(It.IsAny<VideoMediaSource>()), Times.Never);

        // Step 2: Next -> Welcome.png
        _mockStateService.SetupGet(s => s.SelectedSource).Returns(demoVid);
        await coordinator.SwitchToNextSourceAsync();
        _mockStateService.Verify(s => s.SetSelectedSource(welcomePng), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Exactly(2));
        _mockMediaPresentationService.Verify(m => m.LoadImageAsync(It.IsAny<ImageMediaSource>()), Times.Never);

        // Step 3: Next -> Visual Studio
        _mockStateService.SetupGet(s => s.SelectedSource).Returns(welcomePng);
        await coordinator.SwitchToNextSourceAsync();
        _mockStateService.Verify(s => s.SetSelectedSource(vsCode), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateSource(vsCode), Times.Once);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Exactly(2));

        // Chrome remained On Air throughout
        _mockStateService.Verify(s => s.SetActiveSource(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_WhenShuttingDown_DoesNotShowOrActivateOutput()
    {
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(false);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        // Initiate shutdown
        await coordinator.StopPresentationAsync(isShuttingDown: true);

        // Subsequent source switch during shutdown must be ignored / guarded
        _mockWindowService.Invocations.Clear();
        _mockWindowActivationService.Invocations.Clear();

        await coordinator.ExecuteSourceSwitchAsync(vid);

        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(It.IsAny<IntPtr>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteSourceSwitchAsync_WhenOutputAlreadyOpen_ReusesExistingWindowWithoutReopening()
    {
        var vid = new VideoMediaSource { Id = "media:vid.mp4", Title = "vid.mp4", FilePath = @"C:\vid.mp4", IsAvailable = true };
        var outputHwnd = new IntPtr(0x5555);

        _mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.ActiveAndLive);
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        _mockWindowService.SetupGet(s => s.IsWindowOpen).Returns(true);
        _mockWindowService.SetupGet(s => s.WindowHandle).Returns(outputHwnd);

        using var coordinator = new PresentationCoordinator(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockCaptureCoordinator.Object,
            _mockOutputRenderer.Object,
            _mockWindowActivationService.Object,
            _mockMediaPresentationService.Object);

        await coordinator.ExecuteSourceSwitchAsync(vid);

        // Reuses existing window by calling ActivateWindow without re-calling ShowPresentationWindow
        _mockWindowService.Verify(w => w.ShowPresentationWindow(), Times.Never);
        _mockWindowActivationService.Verify(w => w.ActivateWindow(outputHwnd), Times.Once);
    }
}
