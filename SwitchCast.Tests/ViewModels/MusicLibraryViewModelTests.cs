using Moq;
using SwitchCast.Models;
using SwitchCast.Models.Music;
using SwitchCast.Services;
using SwitchCast.Services.Media;
using SwitchCast.Services.Music;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class MusicLibraryViewModelTests
{
    private readonly Mock<IBackgroundMusicService> _mockMusicService;
    private readonly Mock<IMediaPickerService> _mockPickerService;
    private readonly Mock<IUIThreadDispatcher> _mockDispatcher;
    private readonly List<MusicPlaylist> _playlists;
    private readonly MusicPlaylist _defaultPlaylist;

    public MusicLibraryViewModelTests()
    {
        _mockMusicService = new Mock<IBackgroundMusicService>();
        _mockPickerService = new Mock<IMediaPickerService>();
        _mockDispatcher = new Mock<IUIThreadDispatcher>();

        _mockDispatcher.Setup(d => d.TryEnqueue(It.IsAny<Action>()))
            .Callback<Action>(a => a())
            .Returns(true);

        _defaultPlaylist = new MusicPlaylist
        {
            Id = "default-playlist",
            Name = "Default Playlist"
        };

        _playlists = new List<MusicPlaylist> { _defaultPlaylist };

        _mockMusicService.SetupGet(m => m.Playlists).Returns(_playlists);
        _mockMusicService.SetupGet(m => m.CurrentPlaylist).Returns(_defaultPlaylist);
        _mockMusicService.SetupGet(m => m.Volume).Returns(0.75);
        _mockMusicService.SetupGet(m => m.IsMuted).Returns(false);
        _mockMusicService.SetupGet(m => m.IsPlaying).Returns(false);
        _mockMusicService.SetupGet(m => m.IsPaused).Returns(false);
        _mockMusicService.SetupGet(m => m.Duration).Returns(TimeSpan.FromMinutes(3));
        _mockMusicService.SetupGet(m => m.Position).Returns(TimeSpan.FromSeconds(30));
    }

    private MusicLibraryViewModel CreateViewModel()
    {
        return new MusicLibraryViewModel(
            _mockMusicService.Object,
            _mockPickerService.Object,
            _mockDispatcher.Object);
    }

    [Fact]
    public void InitialState_SelectsActiveOrDefaultPlaylist()
    {
        using var vm = CreateViewModel();

        Assert.NotNull(vm.SelectedPlaylist);
        Assert.Equal("Default Playlist", vm.SelectedPlaylist.Name);
        Assert.True(vm.IsSelectedPlaylistActive);
        Assert.Equal("ACTIVE ON AIR PLAYLIST", vm.ActivePlaylistBadgeText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.ActivePlaylistBadgeVisibility);
        Assert.Equal("1", vm.PlaylistsCountText);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.EmptyPlaylistVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.PlaylistTracksVisibility);
    }

    [Fact]
    public void CreatePlaylist_InvokesServiceAndSelectsNewPlaylist()
    {
        var newPlaylist = new MusicPlaylist
        {
            Id = "playlist-2",
            Name = "Conference Intro"
        };

        _mockMusicService.Setup(m => m.CreatePlaylist("Conference Intro"))
            .Callback(() => _playlists.Add(newPlaylist))
            .Returns(newPlaylist);

        using var vm = CreateViewModel();
        vm.CreatePlaylist("Conference Intro");

        _mockMusicService.Verify(m => m.CreatePlaylist("Conference Intro"), Times.Once);
        Assert.Equal("Conference Intro", vm.SelectedPlaylist?.Name);
    }

    [Fact]
    public void RenamePlaylist_InvokesService_WithValidName()
    {
        using var vm = CreateViewModel();
        vm.RenamePlaylist("Renamed Playlist");

        _mockMusicService.Verify(m => m.RenamePlaylist(_defaultPlaylist.Id, "Renamed Playlist"), Times.Once);
    }

    [Fact]
    public void DeletePlaylist_InvokesService_AndSelectsFallback()
    {
        var secondaryPlaylist = new MusicPlaylist { Id = "p2", Name = "Intermission" };
        _playlists.Add(secondaryPlaylist);

        using var vm = CreateViewModel();
        vm.SelectedPlaylist = secondaryPlaylist;

        _mockMusicService.Setup(m => m.DeletePlaylist("p2"))
            .Callback(() => _playlists.Remove(secondaryPlaylist));

        vm.DeletePlaylist(secondaryPlaylist);

        _mockMusicService.Verify(m => m.DeletePlaylist("p2"), Times.Once);
        Assert.Equal(_defaultPlaylist, vm.SelectedPlaylist);
    }

    [Fact]
    public void SetActivePlaylist_UpdatesOnAirSelection()
    {
        var secondaryPlaylist = new MusicPlaylist { Id = "p2", Name = "Keynote Music" };
        _playlists.Add(secondaryPlaylist);

        using var vm = CreateViewModel();
        vm.SelectedPlaylist = secondaryPlaylist;

        _mockMusicService.Setup(m => m.SetActivePlaylist(secondaryPlaylist.Id))
            .Callback(() => _mockMusicService.SetupGet(m => m.CurrentPlaylist).Returns(secondaryPlaylist));

        vm.SetActivePlaylist(secondaryPlaylist);

        _mockMusicService.Verify(m => m.SetActivePlaylist(secondaryPlaylist.Id), Times.Once);
        Assert.True(vm.IsSelectedPlaylistActive);
    }

    [Fact]
    public async Task ImportAudio_ValidFiles_AddsTracksToSelectedPlaylist()
    {
        var pickedFiles = new[]
        {
            @"C:\Music\track1.mp3",
            @"C:\Music\track2.wav",
            @"C:\Music\track3.m4a"
        };

        _mockPickerService.Setup(p => p.PickAudioFilesAsync())
            .ReturnsAsync(pickedFiles);

        var importedTracks = pickedFiles.Select(f => new MusicTrack
        {
            FilePath = f,
            Title = Path.GetFileNameWithoutExtension(f),
            Duration = TimeSpan.FromMinutes(2)
        }).ToList();

        _mockMusicService.Setup(m => m.AddTracksAsync(_defaultPlaylist.Id, pickedFiles))
            .Callback(() =>
            {
                foreach (var t in importedTracks)
                {
                    _defaultPlaylist.Tracks.Add(t);
                }
            })
            .Returns(Task.CompletedTask);

        using var vm = CreateViewModel();
        await vm.ImportAudioAsync();

        _mockMusicService.Verify(m => m.AddTracksAsync(_defaultPlaylist.Id, pickedFiles), Times.Once);
        Assert.Contains("Imported 3 track(s).", vm.StatusMessage);
        Assert.True(vm.HasStatusMessage);
        Assert.Equal(3, vm.SelectedPlaylistTracks.Count);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.PlaylistTracksVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.EmptyPlaylistVisibility);
    }

    [Fact]
    public async Task ImportAudio_CancelledPicker_DoesNotShowError()
    {
        _mockPickerService.Setup(p => p.PickAudioFilesAsync())
            .ReturnsAsync(Array.Empty<string>());

        using var vm = CreateViewModel();
        await vm.ImportAudioAsync();

        _mockMusicService.Verify(m => m.AddTracksAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        Assert.Null(vm.StatusMessage);
        Assert.False(vm.HasStatusMessage);
    }

    [Fact]
    public void MoveTrackUpAndDown_ValidatesBounds_AndReorders()
    {
        var track1 = new MusicTrack { Id = "t1", Title = "First" };
        var track2 = new MusicTrack { Id = "t2", Title = "Second" };
        var track3 = new MusicTrack { Id = "t3", Title = "Third" };

        _defaultPlaylist.Tracks.Add(track1);
        _defaultPlaylist.Tracks.Add(track2);
        _defaultPlaylist.Tracks.Add(track3);

        using var vm = CreateViewModel();

        // Top track cannot move up
        vm.MoveTrackUp(track1);
        _mockMusicService.Verify(m => m.MoveTrack(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);

        // Bottom track cannot move down
        vm.MoveTrackDown(track3);
        _mockMusicService.Verify(m => m.MoveTrack(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);

        // Middle track can move up: index 1 -> 0
        vm.MoveTrackUp(track2);
        _mockMusicService.Verify(m => m.MoveTrack(_defaultPlaylist.Id, 1, 0), Times.Once);

        // Middle track can move down: index 1 -> 2
        vm.MoveTrackDown(track2);
        _mockMusicService.Verify(m => m.MoveTrack(_defaultPlaylist.Id, 1, 2), Times.Once);
    }

    [Fact]
    public void RemoveTrack_RemovesFromSelectedPlaylist()
    {
        var track1 = new MusicTrack { Id = "t1", Title = "Track To Remove" };
        _defaultPlaylist.Tracks.Add(track1);

        using var vm = CreateViewModel();

        _mockMusicService.Setup(m => m.RemoveTrack(_defaultPlaylist.Id, "t1"))
            .Callback(() => _defaultPlaylist.Tracks.Remove(track1));

        vm.RemoveTrack(track1);

        _mockMusicService.Verify(m => m.RemoveTrack(_defaultPlaylist.Id, "t1"), Times.Once);
        Assert.Empty(_defaultPlaylist.Tracks);
    }

    [Fact]
    public async Task PlayTrack_InvokesServicePlayTrack()
    {
        var track = new MusicTrack { Id = "t1", Title = "Song", FilePath = @"C:\Music\song.mp3" };
        _defaultPlaylist.Tracks.Add(track);

        using var vm = CreateViewModel();
        await vm.PlayTrackAsync(track);

        _mockMusicService.Verify(m => m.PlayTrackAsync(track), Times.Once);
    }

    [Fact]
    public void VolumeAndMute_Setters_UpdateMusicService()
    {
        _mockMusicService.SetupProperty(m => m.Volume, 0.75);
        _mockMusicService.SetupProperty(m => m.IsMuted, false);

        using var vm = CreateViewModel();

        vm.Volume = 80;
        _mockMusicService.VerifySet(m => m.Volume = 0.8, Times.Once);
        Assert.Equal("80%", vm.VolumePercentText);

        vm.IsMuted = true;
        _mockMusicService.VerifySet(m => m.IsMuted = true, Times.Once);
    }

    [Fact]
    public void Scrubbing_UpdatesPosition_AndInvokesSeekOnCompletion()
    {
        using var vm = CreateViewModel();

        vm.StartScrubbing();
        vm.PositionSeconds = 45.0;
        vm.CompleteScrubbing(45.0);

        _mockMusicService.Verify(m => m.Seek(TimeSpan.FromSeconds(45)), Times.Once);
    }

    [Fact]
    public void TrackMissingIndicator_ReflectsFileAvailability()
    {
        var availableTrack = new MusicTrack { IsAvailable = true };
        var missingTrack = new MusicTrack { IsAvailable = false };

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, availableTrack.MissingBadgeVisibility);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, missingTrack.MissingBadgeVisibility);
    }
}
