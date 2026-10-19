using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SwitchCast.Models.Music;
using SwitchCast.Services;
using SwitchCast.Services.Media;
using SwitchCast.Services.Music;

namespace SwitchCast.ViewModels;

/// <summary>
/// Authoritative ViewModel for the dedicated Music & Playlists library view.
/// Governs audio importing, playlist CRUD, track reordering, and independent background music transport.
/// </summary>
public partial class MusicLibraryViewModel : ObservableObject, IDisposable
{
    private readonly IBackgroundMusicService _musicService;
    private readonly IMediaPickerService _mediaPickerService;
    private DispatcherQueue? _dispatcherQueue;
    private IUIThreadDispatcher? _dispatcher;
    private bool _isScrubbing;
    private double _scrubbingPositionSeconds;
    private bool _disposed;

    [ObservableProperty]
    private MusicPlaylist? _selectedPlaylist;

    [ObservableProperty]
    private MusicTrack? _selectedTrack;

    [ObservableProperty]
    private string? _statusMessage;

    public MusicLibraryViewModel(
        IBackgroundMusicService musicService,
        IMediaPickerService mediaPickerService,
        IUIThreadDispatcher? uiDispatcher = null)
    {
        _musicService = musicService ?? throw new ArgumentNullException(nameof(musicService));
        _mediaPickerService = mediaPickerService ?? throw new ArgumentNullException(nameof(mediaPickerService));
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _dispatcher = UIThreadDispatcherProvider.Resolve(uiDispatcher);

        _musicService.PlaybackStateChanged += OnPlaybackStateChanged;
        _musicService.TrackChanged += OnTrackChanged;
        _musicService.PlaylistChanged += OnPlaylistChanged;
        _musicService.PositionChanged += OnPositionChanged;

        // Default to active playlist or first available
        _selectedPlaylist = _musicService.CurrentPlaylist ?? _musicService.Playlists.FirstOrDefault();
    }

    public void SetDispatcherQueue(DispatcherQueue? dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _dispatcher = dispatcherQueue is not null ? new DispatcherQueueAdapter(dispatcherQueue) : null;
    }

    public void SetUIThreadDispatcher(IUIThreadDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    #region Properties

    private static readonly System.Collections.ObjectModel.ObservableCollection<MusicTrack> s_emptyTracks = [];

    public IReadOnlyList<MusicPlaylist> Playlists => _musicService.Playlists;

    public string PlaylistsCountText => Playlists.Count.ToString();

    public System.Collections.ObjectModel.ObservableCollection<MusicTrack> SelectedPlaylistTracks => SelectedPlaylist?.Tracks ?? s_emptyTracks;

    public string SelectedPlaylistTracksCountText => $"{SelectedPlaylistTracks.Count} tracks";

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public Microsoft.UI.Xaml.Visibility ActivePlaylistBadgeVisibility => IsSelectedPlaylistActive ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public MusicTrack? CurrentTrack => _musicService.CurrentTrack;

    public string CurrentTrackTitle => CurrentTrack?.Title ?? "No Track Playing";

    public string CurrentTrackArtist => !string.IsNullOrWhiteSpace(CurrentTrack?.Artist) ? CurrentTrack.Artist : "Local Audio";

    public bool IsPlaying => _musicService.IsPlaying;

    public bool IsPaused => _musicService.IsPaused;

    public string PlaybackStatusText => IsPlaying ? "Playing" : (IsPaused ? "Paused" : "Stopped");

    public double Volume
    {
        get => _musicService.Volume * 100.0;
        set
        {
            double normalized = Math.Clamp(value / 100.0, 0.0, 1.0);
            if (Math.Abs(_musicService.Volume - normalized) > 0.001)
            {
                _musicService.Volume = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VolumePercentText));
            }
        }
    }

    public string VolumePercentText => $"{(int)Math.Round(Volume)}%";

    public bool IsMuted
    {
        get => _musicService.IsMuted;
        set
        {
            if (_musicService.IsMuted != value)
            {
                _musicService.IsMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MuteGlyph));
                OnPropertyChanged(nameof(MuteTooltip));
            }
        }
    }

    public double PositionSeconds
    {
        get => _isScrubbing ? _scrubbingPositionSeconds : _musicService.Position.TotalSeconds;
        set
        {
            if (_isScrubbing)
            {
                _scrubbingPositionSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionText));
            }
        }
    }

    public double DurationSeconds => _musicService.Duration.TotalSeconds;

    public string PositionText
    {
        get
        {
            var pos = _isScrubbing ? TimeSpan.FromSeconds(_scrubbingPositionSeconds) : _musicService.Position;
            var dur = _musicService.Duration;
            return $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
    }

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string PlayPauseTooltip => IsPlaying ? "Pause (Space)" : "Play (Space)";

    public string MuteGlyph => IsMuted ? "\uE74F" : "\uE767";

    public string MuteTooltip => IsMuted ? "Unmute Background Music" : "Mute Background Music";

    public MusicRepeatMode RepeatMode => _musicService.RepeatMode;

    public string RepeatGlyph => RepeatMode switch
    {
        MusicRepeatMode.RepeatOne => "\uE8ED",
        MusicRepeatMode.RepeatAll => "\uE8EE",
        _ => "\uF5E7"
    };

    public string RepeatTooltip => RepeatMode switch
    {
        MusicRepeatMode.RepeatOne => "Repeat: One Track",
        MusicRepeatMode.RepeatAll => "Repeat: All Tracks",
        _ => "Repeat: Off"
    };

    public double RepeatOpacity => RepeatMode != MusicRepeatMode.RepeatOff ? 1.0 : 0.45;

    public bool IsShuffleEnabled => _musicService.IsShuffleEnabled;

    public double ShuffleOpacity => IsShuffleEnabled ? 1.0 : 0.45;

    public string ShuffleTooltip => IsShuffleEnabled ? "Shuffle: On" : "Shuffle: Off";

    public bool HasSelectedPlaylist => SelectedPlaylist is not null;

    public bool HasTracks => (SelectedPlaylist?.Tracks.Count ?? 0) > 0;

    public Visibility EmptyPlaylistVisibility => HasTracks ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PlaylistTracksVisibility => HasTracks ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelectedPlaylistActive => SelectedPlaylist is not null && _musicService.CurrentPlaylist?.Id == SelectedPlaylist.Id;

    public string ActivePlaylistBadgeText => IsSelectedPlaylistActive ? "ACTIVE ON AIR PLAYLIST" : "SET AS ACTIVE";

    #endregion

    #region Commands

    [RelayCommand]
    public async Task ImportAudioAsync()
    {
        try
        {
            var files = await _mediaPickerService.PickAudioFilesAsync();
            if (files is null || files.Count == 0)
            {
                return; // User canceled or no files selected
            }

            var target = SelectedPlaylist;
            if (target is null)
            {
                target = _musicService.Playlists.FirstOrDefault();
                if (target is null)
                {
                    target = _musicService.CreatePlaylist("Default Playlist");
                }
                SelectedPlaylist = target;
            }

            await _musicService.AddTracksAsync(target.Id, files);
            NotifySelectedPlaylistChanged();
            StatusMessage = $"Imported {files.Count} track(s).";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] ImportAudioAsync error: {ex.Message}");
            StatusMessage = $"Failed to import audio: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CreatePlaylist(string? playlistName = null)
    {
        try
        {
            string name = string.IsNullOrWhiteSpace(playlistName)
                ? $"Playlist {_musicService.Playlists.Count + 1}"
                : playlistName.Trim();

            var newPlaylist = _musicService.CreatePlaylist(name);
            SelectedPlaylist = newPlaylist;
            NotifyPlaylistsChanged();
            StatusMessage = $"Created playlist \"{name}\".";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] CreatePlaylist error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void RenamePlaylist(string newName)
    {
        if (SelectedPlaylist is null || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            _musicService.RenamePlaylist(SelectedPlaylist.Id, newName.Trim());
            NotifyPlaylistsChanged();
            NotifySelectedPlaylistChanged();
            StatusMessage = $"Renamed playlist to \"{newName.Trim()}\".";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] RenamePlaylist error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DeletePlaylist(MusicPlaylist? playlist = null)
    {
        var target = playlist ?? SelectedPlaylist;
        if (target is null)
        {
            return;
        }

        try
        {
            string name = target.Name;
            _musicService.DeletePlaylist(target.Id);

            if (SelectedPlaylist?.Id == target.Id)
            {
                SelectedPlaylist = _musicService.CurrentPlaylist ?? _musicService.Playlists.FirstOrDefault();
            }

            NotifyPlaylistsChanged();
            NotifySelectedPlaylistChanged();
            StatusMessage = $"Deleted playlist \"{name}\".";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] DeletePlaylist error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SetActivePlaylist(MusicPlaylist? playlist = null)
    {
        var target = playlist ?? SelectedPlaylist;
        if (target is null)
        {
            return;
        }

        try
        {
            _musicService.SetActivePlaylist(target.Id);
            OnPropertyChanged(nameof(IsSelectedPlaylistActive));
            OnPropertyChanged(nameof(ActivePlaylistBadgeText));
            StatusMessage = $"Set \"{target.Name}\" as active playback playlist.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] SetActivePlaylist error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task PlayTrackAsync(MusicTrack? track)
    {
        if (track is null || SelectedPlaylist is null)
        {
            return;
        }

        try
        {
            if (_musicService.CurrentPlaylist?.Id != SelectedPlaylist.Id)
            {
                _musicService.SetActivePlaylist(SelectedPlaylist.Id);
            }

            await _musicService.PlayTrackAsync(track);
            NotifyPlaybackProperties();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] PlayTrackAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task TogglePlayPauseAsync()
    {
        try
        {
            if (_musicService.IsPlaying)
            {
                _musicService.Pause();
            }
            else
            {
                if (_musicService.CurrentPlaylist is null && SelectedPlaylist is not null)
                {
                    _musicService.SetActivePlaylist(SelectedPlaylist.Id);
                }

                await _musicService.PlayAsync();
            }

            NotifyPlaybackProperties();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] TogglePlayPauseAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task NextTrackAsync()
    {
        try
        {
            await _musicService.NextTrackAsync();
            NotifyPlaybackProperties();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] NextTrackAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task PreviousTrackAsync()
    {
        try
        {
            await _musicService.PreviousTrackAsync();
            NotifyPlaybackProperties();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] PreviousTrackAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Stop()
    {
        try
        {
            _musicService.Stop();
            NotifyPlaybackProperties();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] Stop error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        _musicService.IsShuffleEnabled = !_musicService.IsShuffleEnabled;
        OnPropertyChanged(nameof(IsShuffleEnabled));
        OnPropertyChanged(nameof(ShuffleOpacity));
        OnPropertyChanged(nameof(ShuffleTooltip));
    }

    [RelayCommand]
    public void CycleRepeatMode()
    {
        _musicService.RepeatMode = _musicService.RepeatMode switch
        {
            MusicRepeatMode.RepeatOff => MusicRepeatMode.RepeatAll,
            MusicRepeatMode.RepeatAll => MusicRepeatMode.RepeatOne,
            _ => MusicRepeatMode.RepeatOff
        };

        OnPropertyChanged(nameof(RepeatMode));
        OnPropertyChanged(nameof(RepeatGlyph));
        OnPropertyChanged(nameof(RepeatTooltip));
        OnPropertyChanged(nameof(RepeatOpacity));
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    public void RemoveTrack(MusicTrack? track)
    {
        var targetTrack = track ?? SelectedTrack;
        if (SelectedPlaylist is null || targetTrack is null)
        {
            return;
        }

        try
        {
            _musicService.RemoveTrack(SelectedPlaylist.Id, targetTrack.Id);
            NotifySelectedPlaylistChanged();
            StatusMessage = $"Removed \"{targetTrack.Title}\" from playlist.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibraryViewModel] RemoveTrack error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void MoveTrackUp(MusicTrack? track)
    {
        var targetTrack = track ?? SelectedTrack;
        if (SelectedPlaylist is null || targetTrack is null)
        {
            return;
        }

        int index = SelectedPlaylist.Tracks.IndexOf(targetTrack);
        if (index > 0)
        {
            _musicService.MoveTrack(SelectedPlaylist.Id, index, index - 1);
            NotifySelectedPlaylistChanged();
            SelectedTrack = targetTrack;
        }
    }

    [RelayCommand]
    public void MoveTrackDown(MusicTrack? track)
    {
        var targetTrack = track ?? SelectedTrack;
        if (SelectedPlaylist is null || targetTrack is null)
        {
            return;
        }

        int index = SelectedPlaylist.Tracks.IndexOf(targetTrack);
        if (index >= 0 && index < SelectedPlaylist.Tracks.Count - 1)
        {
            _musicService.MoveTrack(SelectedPlaylist.Id, index, index + 1);
            NotifySelectedPlaylistChanged();
            SelectedTrack = targetTrack;
        }
    }

    public void StartScrubbing()
    {
        _isScrubbing = true;
        _scrubbingPositionSeconds = _musicService.Position.TotalSeconds;
    }

    public void CompleteScrubbing(double targetSeconds)
    {
        _isScrubbing = false;
        _musicService.Seek(TimeSpan.FromSeconds(targetSeconds), "MusicLibraryUserScrubbing");
        NotifyPositionProperties();
    }

    #endregion

    #region Event Handlers & Notification Helpers

    partial void OnSelectedPlaylistChanged(MusicPlaylist? value)
    {
        NotifySelectedPlaylistChanged();
    }

    private void NotifyPlaylistsChanged()
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(Playlists));
            OnPropertyChanged(nameof(PlaylistsCountText));
            OnPropertyChanged(nameof(IsSelectedPlaylistActive));
            OnPropertyChanged(nameof(ActivePlaylistBadgeText));
            OnPropertyChanged(nameof(ActivePlaylistBadgeVisibility));
        });
    }

    public void NotifySelectedPlaylistChanged()
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(SelectedPlaylistTracks));
            OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
            OnPropertyChanged(nameof(HasSelectedPlaylist));
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(EmptyPlaylistVisibility));
            OnPropertyChanged(nameof(PlaylistTracksVisibility));
            OnPropertyChanged(nameof(IsSelectedPlaylistActive));
            OnPropertyChanged(nameof(ActivePlaylistBadgeText));
            OnPropertyChanged(nameof(ActivePlaylistBadgeVisibility));
        });
    }

    public void NotifyPlaybackProperties()
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(CurrentTrackArtist));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PlaybackStatusText));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(PlayPauseTooltip));
        OnPropertyChanged(nameof(DurationSeconds));
        NotifyPositionProperties();
    }

    public void NotifyPositionProperties()
    {
        if (!_isScrubbing)
        {
            OnPropertyChanged(nameof(PositionSeconds));
            OnPropertyChanged(nameof(PositionText));
        }
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => RunOnUIThread(NotifyPlaybackProperties);

    private void OnTrackChanged(object? sender, EventArgs e) => RunOnUIThread(NotifyPlaybackProperties);

    private void OnPlaylistChanged(object? sender, EventArgs e) => RunOnUIThread(() =>
    {
        NotifyPlaylistsChanged();
        NotifySelectedPlaylistChanged();
    });

    private void OnPositionChanged(object? sender, TimeSpan e) => RunOnUIThread(NotifyPositionProperties);

    private void RunOnUIThread(Action action)
    {
        if (_disposed) return;

        if (_dispatcher is not null)
        {
            _dispatcher.TryEnqueue(action);
            return;
        }

        if (_dispatcherQueue is not null)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                action();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() => action());
            }
        }
        else
        {
            action();
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _musicService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _musicService.TrackChanged -= OnTrackChanged;
        _musicService.PlaylistChanged -= OnPlaylistChanged;
        _musicService.PositionChanged -= OnPositionChanged;
    }
}
