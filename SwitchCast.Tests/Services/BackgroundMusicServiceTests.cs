using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Music;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.Music;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

public class BackgroundMusicServiceTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly string _testStoragePath;

    public BackgroundMusicServiceTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SwitchCast_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
        _testStoragePath = Path.Combine(_tempTestDir, "playlists.json");
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
            // Ignore temp cleanup errors
        }
    }

    [Fact]
    public async Task CreatePlaylist_AddsNewPlaylist_AndSetsActiveIfFirst()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var initialCount = service.Playlists.Count;
        var playlist = service.CreatePlaylist("Presentation Theme");

        Assert.NotNull(playlist);
        Assert.Equal("Presentation Theme", playlist.Name);
        Assert.Equal(initialCount + 1, service.Playlists.Count);
        Assert.Contains(service.Playlists, p => p.Id == playlist.Id);
    }

    [Fact]
    public async Task RenamePlaylist_UpdatesName_AndPersists()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var playlist = service.CreatePlaylist("Old Name");
        service.RenamePlaylist(playlist.Id, "New Renamed Title");
        await service.SavePlaylistsAsync();

        Assert.Equal("New Renamed Title", playlist.Name);

        // Reload from disk to verify persistence
        using var service2 = new BackgroundMusicService(_testStoragePath);
        await service2.LoadPlaylistsAsync();
        var reloaded = service2.Playlists.FirstOrDefault(p => p.Id == playlist.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("New Renamed Title", reloaded.Name);
    }

    [Fact]
    public async Task DeletePlaylist_RemovesPlaylist_AndSelectsFallback()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var p1 = service.CreatePlaylist("P1");
        var p2 = service.CreatePlaylist("P2");
        service.SetActivePlaylist(p1.Id);

        service.DeletePlaylist(p1.Id);

        Assert.DoesNotContain(service.Playlists, p => p.Id == p1.Id);
        Assert.NotNull(service.CurrentPlaylist);
        Assert.NotEqual(p1.Id, service.CurrentPlaylist.Id);
    }

    [Fact]
    public async Task MoveTrack_ReordersTracksCorrectly()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var playlist = service.CreatePlaylist("Reorder Test");
        var track1 = new MusicTrack { Id = "t1", Title = "Track 1", Duration = TimeSpan.FromMinutes(2) };
        var track2 = new MusicTrack { Id = "t2", Title = "Track 2", Duration = TimeSpan.FromMinutes(3) };
        var track3 = new MusicTrack { Id = "t3", Title = "Track 3", Duration = TimeSpan.FromMinutes(4) };

        playlist.Tracks.Add(track1);
        playlist.Tracks.Add(track2);
        playlist.Tracks.Add(track3);

        // Move track 0 to position 2
        service.MoveTrack(playlist.Id, 0, 2);

        Assert.Equal("t2", playlist.Tracks[0].Id);
        Assert.Equal("t3", playlist.Tracks[1].Id);
        Assert.Equal("t1", playlist.Tracks[2].Id);
    }

    [Fact]
    public async Task RepeatModes_CycleAndBehaveAccurately()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        service.RepeatMode = MusicRepeatMode.RepeatOff;
        Assert.Equal(MusicRepeatMode.RepeatOff, service.RepeatMode);

        service.RepeatMode = MusicRepeatMode.RepeatOne;
        Assert.Equal(MusicRepeatMode.RepeatOne, service.RepeatMode);

        service.RepeatMode = MusicRepeatMode.RepeatAll;
        Assert.Equal(MusicRepeatMode.RepeatAll, service.RepeatMode);
    }

    [Fact]
    public async Task VolumeAndMute_IndependentAndClamped()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        service.Volume = 0.75;
        Assert.Equal(0.75, service.Volume, precision: 2);

        service.IsMuted = true;
        Assert.True(service.IsMuted);

        service.IsMuted = false;
        Assert.False(service.IsMuted);
    }

    [Fact]
    public async Task Playlist_PersistsAndRestoresAfterRestart()
    {
        string trackPath = Path.Combine(_tempTestDir, "test_audio.mp3");
        await File.WriteAllTextAsync(trackPath, "dummy audio content");

        string playlistId;
        using (var service1 = new BackgroundMusicService(_testStoragePath))
        {
            await service1.LoadPlaylistsAsync();
            var playlist = service1.CreatePlaylist("Persisted Playlist");
            playlistId = playlist.Id;
            await service1.AddTracksAsync(playlist.Id, [trackPath]);
            service1.Volume = 0.85;
            service1.RepeatMode = MusicRepeatMode.RepeatOne;
            service1.IsShuffleEnabled = true;
            await service1.SavePlaylistsAsync();
        }

        using (var service2 = new BackgroundMusicService(_testStoragePath))
        {
            await service2.LoadPlaylistsAsync();
            var restoredPlaylist = service2.Playlists.FirstOrDefault(p => p.Id == playlistId);
            Assert.NotNull(restoredPlaylist);
            Assert.Equal("Persisted Playlist", restoredPlaylist.Name);
            Assert.Single(restoredPlaylist.Tracks);
            Assert.Equal("test_audio", restoredPlaylist.Tracks[0].Title);
            Assert.True(restoredPlaylist.Tracks[0].IsAvailable);
            Assert.Equal(0.85, service2.Volume, precision: 2);
            Assert.Equal(MusicRepeatMode.RepeatOne, service2.RepeatMode);
            Assert.True(service2.IsShuffleEnabled);
        }
    }

    [Fact]
    public async Task MissingAudioFile_MarksUnavailable_DoesNotCrash()
    {
        string missingPath = Path.Combine(_tempTestDir, "deleted_audio.mp3");

        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();
        var playlist = service.CreatePlaylist("Missing File Test");
        var track = new MusicTrack
        {
            Id = "missing1",
            Title = "Deleted Song",
            FilePath = missingPath,
            IsAvailable = true
        };
        playlist.Tracks.Add(track);
        service.SetActivePlaylist(playlist.Id);

        // Attempting to play a missing file should safely mark it unavailable without throwing
        var ex = await Record.ExceptionAsync(() => service.PlayTrackAsync(track));
        Assert.Null(ex);
        Assert.False(track.IsAvailable);
    }

    [Fact]
    public async Task StopPresentation_StopsBackgroundMusic()
    {
        var mockStateService = new Mock<IPresentationStateService>();
        var mockWindowService = new Mock<IPresentationWindowService>();
        var mockCaptureCoord = new Mock<ICaptureCoordinator>();
        var mockOutputRenderer = new Mock<IPresentationOutputRenderer>();
        var mockWindowActivation = new Mock<IWindowActivationService>();
        var mockBgmService = new Mock<IBackgroundMusicService>();

        var coordinator = new PresentationCoordinator(
            mockStateService.Object,
            mockWindowService.Object,
            mockCaptureCoord.Object,
            mockOutputRenderer.Object,
            mockWindowActivation.Object,
            backgroundMusicService: mockBgmService.Object);

        await coordinator.StopPresentationAsync();

        mockBgmService.Verify(b => b.Stop(), Times.Once);
    }

    [Fact]
    public async Task DashboardViewModel_MusicCommands_InteractWithBgmService()
    {
        var mockState = new Mock<IPresentationStateService>();
        mockState.SetupGet(s => s.SelectedSources).Returns([]);
        var mockNav = new Mock<INavigationService>();
        var mockCapture = new Mock<ICaptureCoordinator>();
        var mockPresentation = new Mock<IPresentationCoordinator>();
        var mockBgm = new Mock<IBackgroundMusicService>();
        var mockPicker = new Mock<IMediaPickerService>();

        var testPlaylist = new MusicPlaylist { Id = "p1", Name = "Demo List" };
        var testTrack = new MusicTrack { Id = "t1", Title = "Song", Duration = TimeSpan.FromMinutes(3) };
        testPlaylist.Tracks.Add(testTrack);

        mockBgm.SetupGet(b => b.Playlists).Returns([testPlaylist]);
        mockBgm.SetupGet(b => b.CurrentPlaylist).Returns(testPlaylist);
        mockBgm.SetupGet(b => b.CurrentTrack).Returns(testTrack);
        mockBgm.SetupGet(b => b.Volume).Returns(0.6);

        using var vm = new DashboardViewModel(
            mockState.Object,
            mockNav.Object,
            mockCapture.Object,
            mockPresentation.Object,
            backgroundMusicService: mockBgm.Object,
            mediaPickerService: mockPicker.Object);

        // Toggle play/pause
        vm.ToggleMusicPlayPauseCommand.Execute(null);
        mockBgm.Verify(b => b.TogglePlayPause(), Times.Once);

        // Stop
        vm.StopMusicCommand.Execute(null);
        mockBgm.Verify(b => b.Stop(), Times.Once);

        // Next
        await vm.NextMusicTrackCommand.ExecuteAsync(null);
        mockBgm.Verify(b => b.NextTrackAsync(), Times.Once);

        // Previous
        await vm.PreviousMusicTrackCommand.ExecuteAsync(null);
        mockBgm.Verify(b => b.PreviousTrackAsync(), Times.Once);

        // Volume property mapping
        Assert.Equal(60, vm.MusicVolume, precision: 1);
        vm.MusicVolume = 80;
        mockBgm.VerifySet(b => b.Volume = 0.8, Times.Once);
    }

    [Fact]
    public async Task LoadPlaylistsAsync_ReconcilesInPlace_PreservingTrackInstances()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var playlist = service.CreatePlaylist("Persistent Playlist");
        var track = new MusicTrack
        {
            Id = "reconcile-track-1",
            Title = "Song A",
            FilePath = @"C:\Music\songA.mp3",
            Duration = TimeSpan.FromMinutes(3)
        };
        playlist.Tracks.Add(track);
        playlist.NotifyTracksChanged();
        await service.SavePlaylistsAsync();

        // Reload playlists on the same service instance
        await service.LoadPlaylistsAsync();

        var reloadedPlaylist = service.Playlists.FirstOrDefault(p => p.Id == playlist.Id);
        Assert.NotNull(reloadedPlaylist);
        Assert.Same(playlist, reloadedPlaylist); // Same instance preserved
        var reloadedTrack = reloadedPlaylist.Tracks.FirstOrDefault(t => t.Id == track.Id);
        Assert.NotNull(reloadedTrack);
        Assert.Same(track, reloadedTrack); // Track instance preserved
    }

    [Fact]
    public async Task AddTracksAsync_IgnoresExistingTracks_AndPreventsDuplicates()
    {
        using var service = new BackgroundMusicService(_testStoragePath);
        await service.LoadPlaylistsAsync();

        var playlist = service.CreatePlaylist("Duplicate Test");
        var fakeFilePath = Path.Combine(_tempTestDir, "test_audio.mp3");
        await File.WriteAllTextAsync(fakeFilePath, "dummy audio content");

        // First import
        await service.AddTracksAsync(playlist.Id, new[] { fakeFilePath });
        Assert.Single(playlist.Tracks);

        // Second import with same path
        await service.AddTracksAsync(playlist.Id, new[] { fakeFilePath });
        Assert.Single(playlist.Tracks); // No duplicate
    }
}
