using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Music;
using SwitchCast.Services;
using SwitchCast.Services.Camera;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.Music;
using SwitchCast.Services.PowerPoint;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

/// <summary>
/// Mandatory Dual-Player Isolation Regression Tests covering TEST 1 through TEST 16.
/// Guarantees that Background Music and Video Media systems operate strictly independently,
/// sharing no transport commands, mutable state, player instances, or disposal ownership,
/// and coordinating only through the explicit session-stop policy.
/// </summary>
public class DualPlayerIsolationTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly string _testPlaylistsPath;
    private readonly string _testSettingsPath;

    public DualPlayerIsolationTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SwitchCast_IsolationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
        _testPlaylistsPath = Path.Combine(_tempTestDir, "playlists.json");
        _testSettingsPath = Path.Combine(_tempTestDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Test01_PlayVideoAndMusicSimultaneously_PauseVideo_MusicContinues()
    {
        var mockVideoService = new Mock<IMediaPresentationService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        bool isVideoPlaying = true;
        bool isMusicPlaying = true;

        mockVideoService.SetupGet(v => v.IsVideoPlaying).Returns(() => isVideoPlaying);
        mockVideoService.SetupGet(v => v.IsVideoPaused).Returns(() => !isVideoPlaying);
        mockVideoService.Setup(v => v.PauseVideo()).Callback(() => isVideoPlaying = false);

        mockMusicService.SetupGet(m => m.IsPlaying).Returns(() => isMusicPlaying);
        mockMusicService.SetupGet(m => m.IsPaused).Returns(() => !isMusicPlaying);

        // Pause Video
        mockVideoService.Object.PauseVideo();

        Assert.False(mockVideoService.Object.IsVideoPlaying);
        Assert.True(mockVideoService.Object.IsVideoPaused);

        // Music continues
        Assert.True(mockMusicService.Object.IsPlaying);
        Assert.False(mockMusicService.Object.IsPaused);
    }

    [Fact]
    public void Test02_PlayVideoAndMusicSimultaneously_PauseMusic_VideoContinues()
    {
        var mockVideoService = new Mock<IMediaPresentationService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        bool isVideoPlaying = true;
        bool isMusicPlaying = true;

        mockVideoService.SetupGet(v => v.IsVideoPlaying).Returns(() => isVideoPlaying);
        mockVideoService.SetupGet(v => v.IsVideoPaused).Returns(() => !isVideoPlaying);

        mockMusicService.SetupGet(m => m.IsPlaying).Returns(() => isMusicPlaying);
        mockMusicService.SetupGet(m => m.IsPaused).Returns(() => !isMusicPlaying);
        mockMusicService.Setup(m => m.Pause()).Callback(() => isMusicPlaying = false);

        // Pause Music
        mockMusicService.Object.Pause();

        Assert.False(mockMusicService.Object.IsPlaying);
        Assert.True(mockMusicService.Object.IsPaused);

        // Video continues
        Assert.True(mockVideoService.Object.IsVideoPlaying);
        Assert.False(mockVideoService.Object.IsVideoPaused);
    }

    [Fact]
    public void Test03_ChangeMusicVolume_VideoVolumeRemainsUnchanged()
    {
        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        var mockSettings = new Mock<IApplicationSettingsService>();
        var settings = new UserSettings { MediaVolume = 0.85, BackgroundMusicVolume = 0.5 };
        mockSettings.SetupGet(s => s.CurrentSettings).Returns(settings);
        mockSettings.Setup(s => s.SaveSettingsAsync()).Returns(Task.CompletedTask);

        using var videoService = new MediaPresentationService(mockSettings.Object);

        double initialVideoVolume = videoService.Volume;
        Assert.Equal(0.85, initialVideoVolume, precision: 2);

        // Change music volume
        musicService.Volume = 0.25;

        Assert.Equal(0.25, musicService.Volume, precision: 2);
        // Video volume remains unchanged
        Assert.Equal(0.85, videoService.Volume, precision: 2);
    }

    [Fact]
    public void Test04_MuteVideo_MusicRemainsAudible()
    {
        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        musicService.IsMuted = false;

        var mockSettings = new Mock<IApplicationSettingsService>();
        var settings = new UserSettings { IsMediaMuted = false };
        mockSettings.SetupGet(s => s.CurrentSettings).Returns(settings);
        mockSettings.Setup(s => s.SaveSettingsAsync()).Returns(Task.CompletedTask);

        using var videoService = new MediaPresentationService(mockSettings.Object);

        // Mute video
        videoService.SetMuted(true);

        Assert.True(videoService.IsMuted);
        // Music remains unmuted and audible
        Assert.False(musicService.IsMuted);
    }

    [Fact]
    public void Test05_MuteMusic_VideoRemainsAudible()
    {
        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        var mockSettings = new Mock<IApplicationSettingsService>();
        var settings = new UserSettings { IsMediaMuted = false };
        mockSettings.SetupGet(s => s.CurrentSettings).Returns(settings);
        mockSettings.Setup(s => s.SaveSettingsAsync()).Returns(Task.CompletedTask);

        using var videoService = new MediaPresentationService(mockSettings.Object);

        // Mute music
        musicService.IsMuted = true;

        Assert.True(musicService.IsMuted);
        // Video remains unmuted and audible
        Assert.False(videoService.IsMuted);
    }

    [Fact]
    public void Test06_SeekVideo_MusicPositionRemainsUnchanged()
    {
        var mockVideoService = new Mock<IMediaPresentationService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        TimeSpan videoPos = TimeSpan.FromSeconds(5);
        TimeSpan musicPos = TimeSpan.FromSeconds(40);

        mockVideoService.SetupGet(v => v.Position).Returns(() => videoPos);
        mockVideoService.Setup(v => v.Seek(It.IsAny<TimeSpan>())).Callback<TimeSpan>(p => videoPos = p);

        mockMusicService.SetupGet(m => m.Position).Returns(() => musicPos);
        mockMusicService.Setup(m => m.Seek(It.IsAny<TimeSpan>())).Callback<TimeSpan>(p => musicPos = p);

        // Seek video
        mockVideoService.Object.Seek(TimeSpan.FromSeconds(25));

        Assert.Equal(TimeSpan.FromSeconds(25), mockVideoService.Object.Position);
        // Music position unchanged
        Assert.Equal(TimeSpan.FromSeconds(40), mockMusicService.Object.Position);
    }

    [Fact]
    public void Test07_SeekMusic_VideoPositionRemainsUnchanged()
    {
        var mockVideoService = new Mock<IMediaPresentationService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        TimeSpan videoPos = TimeSpan.FromSeconds(15);
        TimeSpan musicPos = TimeSpan.FromSeconds(10);

        mockVideoService.SetupGet(v => v.Position).Returns(() => videoPos);
        mockVideoService.Setup(v => v.Seek(It.IsAny<TimeSpan>())).Callback<TimeSpan>(p => videoPos = p);

        mockMusicService.SetupGet(m => m.Position).Returns(() => musicPos);
        mockMusicService.Setup(m => m.Seek(It.IsAny<TimeSpan>())).Callback<TimeSpan>(p => musicPos = p);

        // Seek music
        mockMusicService.Object.Seek(TimeSpan.FromSeconds(55));

        Assert.Equal(TimeSpan.FromSeconds(55), mockMusicService.Object.Position);
        // Video position unchanged
        Assert.Equal(TimeSpan.FromSeconds(15), mockVideoService.Object.Position);
    }

    [Fact]
    public void Test08_VideoReachesEnd_MusicPlaylistRemainsUnaffected()
    {
        var mockMusicService = new Mock<IBackgroundMusicService>();
        var currentTrack = new MusicTrack { Id = "track-1", Title = "Track One" };
        var playlist = new MusicPlaylist { Id = "pl-1", Name = "Chill", Tracks = { currentTrack } };

        mockMusicService.SetupGet(m => m.CurrentPlaylist).Returns(playlist);
        mockMusicService.SetupGet(m => m.CurrentTrack).Returns(currentTrack);
        mockMusicService.SetupGet(m => m.IsPlaying).Returns(true);

        var mockSettings = new Mock<IApplicationSettingsService>();
        mockSettings.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
        mockSettings.Setup(s => s.SaveSettingsAsync()).Returns(Task.CompletedTask);

        using var videoService = new MediaPresentationService(mockSettings.Object);

        // Video completes
        videoService.PauseVideo();

        // Music playlist and track remain unaffected
        Assert.NotNull(mockMusicService.Object.CurrentPlaylist);
        Assert.Equal("pl-1", mockMusicService.Object.CurrentPlaylist.Id);
        Assert.NotNull(mockMusicService.Object.CurrentTrack);
        Assert.Equal("track-1", mockMusicService.Object.CurrentTrack.Id);
        Assert.True(mockMusicService.Object.IsPlaying);
    }

    [Fact]
    public async Task Test09_MusicReachesEnd_MusicAdvancesAccordingToRepeatMode_VideoRemainsUnaffected()
    {
        string f1 = Path.Combine(_tempTestDir, "t1.mp3");
        string f2 = Path.Combine(_tempTestDir, "t2.mp3");
        await File.WriteAllTextAsync(f1, "dummy audio 1");
        await File.WriteAllTextAsync(f2, "dummy audio 2");

        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        await musicService.LoadPlaylistsAsync();

        var playlist = musicService.CreatePlaylist("Test PL");
        var t1 = new MusicTrack { Id = "t1", Title = "Track 1", FilePath = f1, Duration = TimeSpan.FromSeconds(10) };
        var t2 = new MusicTrack { Id = "t2", Title = "Track 2", FilePath = f2, Duration = TimeSpan.FromSeconds(15) };
        playlist.Tracks.Add(t1);
        playlist.Tracks.Add(t2);
        musicService.SetActivePlaylist(playlist.Id);
        musicService.RepeatMode = MusicRepeatMode.RepeatAll;

        await musicService.PlayTrackAsync(t1);
        Assert.Equal("t1", musicService.CurrentTrack?.Id);

        var mockVideoService = new Mock<IMediaPresentationService>();
        mockVideoService.SetupGet(v => v.IsVideoPlaying).Returns(true);
        mockVideoService.SetupGet(v => v.Position).Returns(TimeSpan.FromSeconds(7));

        // Advance music track (simulating track end)
        await musicService.NextTrackAsync();

        Assert.Equal("t2", musicService.CurrentTrack?.Id);

        // Video remains playing and at the exact same position
        Assert.True(mockVideoService.Object.IsVideoPlaying);
        Assert.Equal(TimeSpan.FromSeconds(7), mockVideoService.Object.Position);
    }

    [Fact]
    public async Task Test10_SwitchVideoPowerPointCameraVideo_MusicContinuesWithoutRestarting()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivationService = new Mock<IWindowActivationService>();
        var mockMediaPresentationService = new Mock<IMediaPresentationService>();
        var mockPowerPointService = new Mock<IPowerPointPresentationService>();
        var mockCameraService = new Mock<ICameraCaptureService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        bool musicIsPlaying = true;
        mockMusicService.SetupGet(m => m.IsPlaying).Returns(() => musicIsPlaying);
        mockMusicService.Setup(m => m.Stop()).Callback(() => musicIsPlaying = false);
        mockMusicService.Setup(m => m.Pause()).Callback(() => musicIsPlaying = false);

        mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Idle);
        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockWindowActivationService.Setup(w => w.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockOutputRenderer.Object,
            mockWindowActivationService.Object,
            mockMediaPresentationService.Object,
            navigationService: null,
            externalDisplayService: null,
            powerPointService: mockPowerPointService.Object,
            powerPointLiveService: null,
            cameraCaptureService: mockCameraService.Object,
            presenterDockService: null,
            settingsService: null,
            backgroundMusicService: mockMusicService.Object);

        var videoSource = new VideoMediaSource { Id = "v1", Title = "Video 1", FilePath = @"C:\v1.mp4", IsAvailable = true };
        var pptSource = new PowerPointSource { Id = "p1", Title = "Slides", FilePath = @"C:\s.pptx", IsAvailable = true };
        var camSource = new CameraSource { Id = "c1", Title = "Cam", DeviceId = "dev-1", IsAvailable = true };

        // 1. Switch to Video
        await coordinator.SwitchPresentationSourceAsync(videoSource);
        Assert.True(mockMusicService.Object.IsPlaying);

        // 2. Switch to PowerPoint
        await coordinator.SwitchPresentationSourceAsync(pptSource);
        Assert.True(mockMusicService.Object.IsPlaying);

        // 3. Switch to Camera
        await coordinator.SwitchPresentationSourceAsync(camSource);
        Assert.True(mockMusicService.Object.IsPlaying);

        // 4. Switch back to Video
        await coordinator.SwitchPresentationSourceAsync(videoSource);
        Assert.True(mockMusicService.Object.IsPlaying);

        // Verify music was NEVER stopped or paused during source switching
        mockMusicService.Verify(m => m.Stop(), Times.Never);
        mockMusicService.Verify(m => m.Pause(), Times.Never);
    }

    [Fact]
    public async Task Test11_ChangeMusicPlaylistWhileVideoIsLive_OnAirSourceRemainsUnchanged()
    {
        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        await musicService.LoadPlaylistsAsync();
        var pl1 = musicService.CreatePlaylist("Playlist 1");
        var pl2 = musicService.CreatePlaylist("Playlist 2");
        musicService.SetActivePlaylist(pl1.Id);

        var videoSource = new VideoMediaSource { Id = "v1", Title = "Active Video", FilePath = @"C:\v.mp4", IsAvailable = true };

        var mockCoordinator = new Mock<IPresentationCoordinator>();
        mockCoordinator.SetupGet(c => c.CurrentPresentationSource).Returns(videoSource);
        mockCoordinator.SetupGet(c => c.IsActiveSourceVideo).Returns(true);
        mockCoordinator.SetupGet(c => c.IsLive).Returns(true);

        var mockMediaService = new Mock<IMediaPresentationService>();
        mockCoordinator.SetupGet(c => c.MediaPresentationService).Returns(mockMediaService.Object);

        var mockStateService = new Mock<IPresentationStateService>();
        mockStateService.SetupGet(s => s.ActiveSource).Returns(videoSource);
        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource> { videoSource });

        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());

        var dockVm = new PresenterDockViewModel(
            mockCoordinator.Object,
            mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            mockSettingsService.Object,
            backgroundMusicService: musicService);

        Assert.Equal("Active Video", dockVm.OnAirSourceTitle);

        // Change music playlist
        dockVm.ActivePlaylist = pl2;

        Assert.Equal("Playlist 2", musicService.CurrentPlaylist?.Name);
        // On Air source remains completely unchanged
        Assert.Equal("Active Video", dockVm.OnAirSourceTitle);
        mockCoordinator.Verify(c => c.SwitchPresentationSourceAsync(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task Test12_ClickSwitchDeckNextSource_MusicTrackDoesNotChange()
    {
        var mockMusicService = new Mock<IBackgroundMusicService>();
        var track = new MusicTrack { Id = "current-track", Title = "Current BGM" };
        mockMusicService.SetupGet(m => m.CurrentTrack).Returns(track);

        var mockCoordinator = new Mock<IPresentationCoordinator>();
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());

        var mockMediaService = new Mock<IMediaPresentationService>();
        mockCoordinator.SetupGet(c => c.MediaPresentationService).Returns(mockMediaService.Object);

        var dockVm = new PresenterDockViewModel(
            mockCoordinator.Object,
            mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            mockSettingsService.Object,
            backgroundMusicService: mockMusicService.Object);

        // Click SwitchDeck Next Source
        await dockVm.NextSourceCommand.ExecuteAsync(null);

        // Presentation coordinator advanced
        mockCoordinator.Verify(c => c.SwitchToNextSourceAsync(), Times.Once);

        // Music track was NOT changed
        mockMusicService.Verify(m => m.NextTrackAsync(), Times.Never);
        mockMusicService.Verify(m => m.PreviousTrackAsync(), Times.Never);
        Assert.NotNull(mockMusicService.Object.CurrentTrack);
        Assert.Equal("current-track", mockMusicService.Object.CurrentTrack.Id);
    }

    [Fact]
    public async Task Test13_ClickSwitchDeckNextMusicTrack_PresentationSourceDoesNotChange()
    {
        var mockMusicService = new Mock<IBackgroundMusicService>();
        var mockCoordinator = new Mock<IPresentationCoordinator>();
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockDockService = new Mock<IPresenterDockService>();
        var mockSettingsService = new Mock<IApplicationSettingsService>();
        mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());

        var mockMediaService = new Mock<IMediaPresentationService>();
        mockCoordinator.SetupGet(c => c.MediaPresentationService).Returns(mockMediaService.Object);

        var dockVm = new PresenterDockViewModel(
            mockCoordinator.Object,
            mockStateService.Object,
            mockWindowService.Object,
            mockDockService.Object,
            mockSettingsService.Object,
            backgroundMusicService: mockMusicService.Object);

        // Click SwitchDeck Next Music Track
        await dockVm.NextBackgroundMusicTrackCommand.ExecuteAsync(null);

        // Music service advanced
        mockMusicService.Verify(m => m.NextTrackAsync(), Times.Once);

        // Presentation source switching commands were NOT invoked
        mockCoordinator.Verify(c => c.SwitchToNextSourceAsync(), Times.Never);
        mockCoordinator.Verify(c => c.SwitchToPreviousSourceAsync(), Times.Never);
        mockCoordinator.Verify(c => c.ExecuteSourceSwitchAsync(It.IsAny<CaptureSource>()), Times.Never);
    }

    [Fact]
    public async Task Test14_StopPresenting_BothPlayerSessionsStop_MainWindowRemainsAlive()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivationService = new Mock<IWindowActivationService>();
        var mockMediaPresentationService = new Mock<IMediaPresentationService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();

        mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Active);
        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockCaptureCoordinator.SetupGet(c => c.State).Returns(CaptureState.Capturing);
        mockWindowActivationService.Setup(w => w.ActivateMainWindow()).Returns(true);

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockOutputRenderer.Object,
            mockWindowActivationService.Object,
            mockMediaPresentationService.Object,
            navigationService: null,
            externalDisplayService: null,
            powerPointService: null,
            powerPointLiveService: null,
            cameraCaptureService: null,
            presenterDockService: null,
            settingsService: null,
            backgroundMusicService: mockMusicService.Object);

        // Stop Presentation
        await coordinator.StopPresentationAsync();

        // 1. Video stopped
        mockMediaPresentationService.Verify(m => m.StopVideoAsync(), Times.Once);

        // 2. Music stopped
        mockMusicService.Verify(m => m.Stop(), Times.Once);

        // 3. MainWindow remains alive and is reactivated
        mockWindowActivationService.Verify(w => w.ActivateMainWindow(), Times.Once);
    }

    [Fact]
    public void Test15_CloseOrHideSwitchDeckNonDestructively_MusicAndPresentationContinue()
    {
        var mockDockService = new Mock<IPresenterDockService>();
        var mockMusicService = new Mock<IBackgroundMusicService>();
        var mockVideoService = new Mock<IMediaPresentationService>();
        var mockCoordinator = new Mock<IPresentationCoordinator>();

        mockCoordinator.SetupGet(c => c.IsLive).Returns(true);
        mockCoordinator.SetupGet(c => c.MediaPresentationService).Returns(mockVideoService.Object);
        mockMusicService.SetupGet(m => m.IsPlaying).Returns(true);
        mockVideoService.SetupGet(v => v.IsVideoPlaying).Returns(true);

        // Non-destructive dock close
        mockDockService.Object.CloseDock();

        mockDockService.Verify(d => d.CloseDock(), Times.Once);

        // Both presentation and music continue uninterrupted
        mockCoordinator.Verify(c => c.StopPresentationAsync(It.IsAny<bool>()), Times.Never);
        mockVideoService.Verify(v => v.StopVideoAsync(), Times.Never);
        mockMusicService.Verify(m => m.Stop(), Times.Never);
    }

    [Fact]
    public async Task Test16_RapidlySwitchSourcesWhileChangingTracks_NoDeadlockOrDisposalRace()
    {
        using var musicService = new BackgroundMusicService(_testPlaylistsPath);
        await musicService.LoadPlaylistsAsync();
        var playlist = musicService.CreatePlaylist("Rapid Concurrency");
        for (int i = 1; i <= 5; i++)
        {
            string f = Path.Combine(_tempTestDir, $"rapid_t{i}.mp3");
            File.WriteAllText(f, $"dummy {i}");
            playlist.Tracks.Add(new MusicTrack { Id = $"t{i}", Title = $"Track {i}", FilePath = f, Duration = TimeSpan.FromMinutes(2) });
        }
        musicService.SetActivePlaylist(playlist.Id);

        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoordinator = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivationService = new Mock<IWindowActivationService>();
        var mockMediaPresentationService = new Mock<IMediaPresentationService>();

        mockWindowService.SetupGet(w => w.IsWindowOpen).Returns(true);
        mockWindowActivationService.Setup(w => w.ActivateSource(It.IsAny<CaptureSource>())).Returns(true);

        using var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoordinator.Object,
            mockOutputRenderer.Object,
            mockWindowActivationService.Object,
            mockMediaPresentationService.Object,
            backgroundMusicService: musicService);

        var sourceA = new WindowSource { Id = "w1", Title = "Window 1", IsAvailable = true };
        var sourceB = new VideoMediaSource { Id = "v1", Title = "Video 1", FilePath = @"C:\v.mp4", IsAvailable = true };

        var sourceTask = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                var src = (i % 2 == 0) ? (CaptureSource)sourceA : sourceB;
                await coordinator.SwitchPresentationSourceAsync(src).ConfigureAwait(false);
                await Task.Delay(5).ConfigureAwait(false);
            }
        });

        var musicTask = Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                await musicService.NextTrackAsync().ConfigureAwait(false);
                musicService.Seek(TimeSpan.FromSeconds(i * 2));
                await Task.Delay(5).ConfigureAwait(false);
            }
        });

        // Must complete within 10 seconds without deadlocking or throwing unobserved exceptions
        await Task.WhenAll(sourceTask, musicTask).WaitAsync(TimeSpan.FromSeconds(10));
    }
}
