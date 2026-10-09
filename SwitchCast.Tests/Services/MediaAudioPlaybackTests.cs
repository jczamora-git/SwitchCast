using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

public class MediaAudioPlaybackTests
{
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly UserSettings _userSettings;

    public MediaAudioPlaybackTests()
    {
        _userSettings = new UserSettings
        {
            MediaVolume = 0.8,
            IsMediaMuted = false
        };

        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(_userSettings);
        _mockSettingsService.Setup(s => s.SaveSettingsAsync()).Returns(Task.CompletedTask);
    }

    [Fact]
    public void MediaPresentationService_InitializesAudioSettings_FromUserSettings()
    {
        using var service = new MediaPresentationService(_mockSettingsService.Object);

        Assert.Equal(0.8, service.Volume, precision: 2);
        Assert.False(service.IsMuted);
    }

    [Fact]
    public void MediaPresentationService_SetVolume_ClampsAndPersists()
    {
        using var service = new MediaPresentationService(_mockSettingsService.Object);

        bool stateChangedFired = false;
        service.MediaStateChanged += (s, e) => stateChangedFired = true;

        service.SetVolume(0.5);

        Assert.Equal(0.5, service.Volume, precision: 2);
        Assert.Equal(0.5, _userSettings.MediaVolume, precision: 2);
        Assert.True(stateChangedFired);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);

        // Clamping upper
        service.SetVolume(1.5);
        Assert.Equal(1.0, service.Volume, precision: 2);
        Assert.Equal(1.0, _userSettings.MediaVolume, precision: 2);

        // Clamping lower
        service.SetVolume(-0.2);
        Assert.Equal(0.0, service.Volume, precision: 2);
        Assert.Equal(0.0, _userSettings.MediaVolume, precision: 2);
    }

    [Fact]
    public void MediaPresentationService_SetMuted_And_ToggleMute_UpdatesAndPersists()
    {
        using var service = new MediaPresentationService(_mockSettingsService.Object);

        service.SetMuted(true);
        Assert.True(service.IsMuted);
        Assert.True(_userSettings.IsMediaMuted);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);

        service.ToggleMute();
        Assert.False(service.IsMuted);
        Assert.False(_userSettings.IsMediaMuted);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Exactly(2));
    }

    [Fact]
    public void DashboardViewModel_MediaVolume_And_MuteCommands_Synchronize()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockNavigationService = new Mock<INavigationService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockPresentationCoordinator = new Mock<IPresentationCoordinator>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        mockMediaService.SetupProperty(m => m.Volume, 0.75);
        mockMediaService.SetupProperty(m => m.IsMuted, false);

        mockPresentationCoordinator.SetupGet(p => p.MediaPresentationService).Returns(mockMediaService.Object);
        mockPresentationCoordinator.SetupGet(p => p.IsActiveSourceVideo).Returns(true);
        mockPresentationCoordinator.SetupGet(p => p.IsLive).Returns(true);

        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());

        var vm = new DashboardViewModel(
            mockStateService.Object,
            mockNavigationService.Object,
            mockCaptureCoordinator.Object,
            mockPresentationCoordinator.Object,
            null,
            _mockSettingsService.Object);

        Assert.Equal(75.0, vm.MediaVolume, precision: 1);
        Assert.Equal("75%", vm.MediaVolumePercentText);
        Assert.False(vm.IsMediaMuted);
        Assert.Equal("\uE767", vm.MediaMuteButtonGlyph);

        // Change volume via slider property (0..100)
        vm.MediaVolume = 50.0;
        mockMediaService.Verify(m => m.SetVolume(0.5), Times.Once);

        // Toggle mute via command
        vm.ToggleMediaMuteCommand.Execute(null);
        mockMediaService.Verify(m => m.ToggleMute(), Times.Once);
    }

    [Fact]
    public void PresenterDockViewModel_MediaAudio_ReflectsStateAndTogglesMute()
    {
        var mockCoordinator = new Mock<IPresentationCoordinator>();
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        mockMediaService.SetupProperty(m => m.Volume, 0.6);
        mockMediaService.SetupProperty(m => m.IsMuted, true);

        mockCoordinator.SetupGet(p => p.MediaPresentationService).Returns(mockMediaService.Object);
        mockCoordinator.SetupGet(p => p.IsActiveSourceVideo).Returns(true);
        mockCoordinator.SetupGet(p => p.IsLive).Returns(true);

        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        var vm = new PresenterDockViewModel(
            mockCoordinator.Object,
            mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            _mockSettingsService.Object);

        Assert.True(vm.IsActiveSourceVideo);
        Assert.True(vm.IsMediaMuted);
        Assert.Equal("\uE74F", vm.MediaMuteButtonGlyph);
        Assert.Equal("Unmute Video Audio", vm.MediaMuteButtonTooltip);

        // Toggle Mute Command
        vm.ToggleMediaMuteCommand.Execute(null);
        mockMediaService.Verify(m => m.ToggleMute(), Times.Once);

        // Adjust Volume
        vm.MediaVolume = 90.0;
        mockMediaService.Verify(m => m.SetVolume(0.9), Times.Once);
    }

    [Fact]
    public async Task PresentationCoordinator_VideoAudioLifecycle_PauseResumeBlackoutStop()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        var currentStatus = PresentationStatus.Idle;
        CaptureSource? activeSource = null;

        mockStateService.Setup(s => s.SetStatus(It.IsAny<PresentationStatus>()))
            .Callback<PresentationStatus>(st => currentStatus = st);
        mockStateService.SetupGet(s => s.Status).Returns(() => currentStatus);

        mockStateService.Setup(s => s.SetActiveSource(It.IsAny<CaptureSource?>()))
            .Callback<CaptureSource?>(src => activeSource = src);
        mockStateService.SetupGet(s => s.ActiveSource).Returns(() => activeSource);
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);

        var videoSource = new VideoMediaSource
        {
            Id = "media:demo.mp4",
            Title = "demo.mp4",
            FilePath = @"C:\demo.mp4",
            IsAvailable = true
        };

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockOutputRenderer.Object,
            mockWindowActivationService.Object,
            mockMediaService.Object);

        // Start presentation with video
        await coordinator.StartPresentationAsync(videoSource);
        mockMediaService.Verify(m => m.PlayVideoAsync(videoSource), Times.Once);

        // Pause presentation (suspends video + audio)
        await coordinator.PausePresentationAsync();
        mockMediaService.Verify(m => m.PauseVideo(), Times.Once);

        // Resume presentation (resumes video + audio)
        await coordinator.ResumePresentationAsync();
        mockMediaService.Verify(m => m.ResumeVideo(), Times.Once);

        // Blackout presentation (suspends video + audio)
        await coordinator.ToggleBlackoutAsync();
        mockMediaService.Verify(m => m.PauseVideo(), Times.Exactly(2));

        // Restore from Blackout (restores video + audio)
        await coordinator.ToggleBlackoutAsync();
        mockMediaService.Verify(m => m.ResumeVideo(), Times.Exactly(2));

        // Stop presentation (stops & unloads video + audio)
        await coordinator.StopPresentationAsync();
        mockMediaService.Verify(m => m.StopVideoAsync(), Times.AtLeastOnce());
    }

    [Fact]
    public async Task PresentationCoordinator_SwitchingBetweenMediaAndCapture_StopsOldAudio()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivationService = new Mock<IWindowActivationService>();
        var mockMediaService = new Mock<IMediaPresentationService>();

        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        mockStateService.SetupGet(s => s.SwitchMode).Returns(PresenterSwitchMode.LiveOnly);

        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);

        var video1 = new VideoMediaSource { Id = "media:vid1.mp4", Title = "vid1.mp4", FilePath = @"C:\vid1.mp4", IsAvailable = true };
        var video2 = new VideoMediaSource { Id = "media:vid2.mp4", Title = "vid2.mp4", FilePath = @"C:\vid2.mp4", IsAvailable = true };
        var image = new ImageMediaSource { Id = "media:img.png", Title = "img.png", FilePath = @"C:\img.png", IsAvailable = true };
        var window = new WindowSource { Id = "win-1", Title = "Chrome", IsAvailable = true };

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockOutputRenderer.Object,
            mockWindowActivationService.Object,
            mockMediaService.Object);

        // 1. Start Video 1
        await coordinator.StartPresentationAsync(video1);
        mockMediaService.Verify(m => m.PlayVideoAsync(video1), Times.Once);

        // 2. Switch Video 1 -> Image (must stop video audio)
        await coordinator.SwitchPresentationSourceAsync(image);
        mockMediaService.Verify(m => m.StopVideoAsync(), Times.AtLeastOnce());
        mockMediaService.Verify(m => m.LoadImageAsync(image), Times.Once);

        // 3. Switch Image -> Video 2 (must clear image and start video 2)
        await coordinator.SwitchPresentationSourceAsync(video2);
        mockMediaService.Verify(m => m.ClearImage(), Times.AtLeastOnce());
        mockMediaService.Verify(m => m.PlayVideoAsync(video2), Times.Once);

        // 4. Switch Video 2 -> Window (must stop video 2 audio and start window capture)
        await coordinator.SwitchPresentationSourceAsync(window);
        mockMediaService.Verify(m => m.StopVideoAsync(), Times.AtLeast(2));
        mockCaptureCoordinator.Verify(c => c.StartPreviewAsync(window), Times.Once);
    }
}
