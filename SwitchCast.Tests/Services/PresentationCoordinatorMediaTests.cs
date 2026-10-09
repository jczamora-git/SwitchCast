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
}
