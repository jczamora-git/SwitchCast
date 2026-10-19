using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SwitchCast.Models.Music;
using SwitchCast.Services.Diagnostics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace SwitchCast.Services.Music;

/// <summary>
/// Authoritative implementation of independent background music playback and local playlist management.
/// Uses an isolated, dedicated audio-only MediaPlayer instance completely decoupled from video presentation.
/// </summary>
public sealed partial class BackgroundMusicService : ObservableObject, IBackgroundMusicService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly Random _random = new();

    private MediaPlayer? _player;
    private MediaSource? _currentMediaSource;
    private System.Threading.Timer? _positionTimer;
    private bool _disposed;

    private readonly List<MusicPlaylist> _playlists = [];
    private readonly List<int> _shuffleHistory = [];

    [ObservableProperty]
    private MusicPlaylist? _currentPlaylist;

    [ObservableProperty]
    private MusicTrack? _currentTrack;

    [ObservableProperty]
    private int _currentTrackIndex = -1;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private MusicRepeatMode _repeatMode = MusicRepeatMode.RepeatAll;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private double _volume = 0.5;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private TimeSpan _position = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? TrackChanged;
    public event EventHandler? PlaylistChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    public IReadOnlyList<MusicPlaylist> Playlists
    {
        get
        {
            lock (_stateLock)
            {
                return _playlists.ToList();
            }
        }
    }

    public BackgroundMusicService(string? customStoragePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customStoragePath))
        {
            _storageFilePath = customStoragePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "SwitchCast");
            _storageFilePath = Path.Combine(appDir, "playlists.json");
        }

        InitializePlayer();
    }

    private void InitializePlayer()
    {
        try
        {
            _player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.Media,
                Volume = Volume,
                IsMuted = IsMuted,
                AutoPlay = false
            };

            _player.CommandManager.IsEnabled = false;
            _player.MediaEnded += OnPlayerMediaEnded;
            _player.MediaFailed += OnPlayerMediaFailed;
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(InitializePlayer), ex);
            LastErrorMessage = "Failed to initialize background audio player.";
        }
    }

    private void OnPlayerMediaEnded(MediaPlayer sender, object args)
    {
        _ = HandleTrackEndedAsync();
    }

    private async Task HandleTrackEndedAsync()
    {
        try
        {
            if (RepeatMode == MusicRepeatMode.RepeatOne && CurrentTrack is not null)
            {
                Seek(TimeSpan.Zero);
                Resume();
                return;
            }

            await NextTrackAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(HandleTrackEndedAsync), ex);
        }
    }

    private void OnPlayerMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        string errorDetails = $"Playback failed: {args.Error} (HRESULT: 0x{args.ExtendedErrorCode?.HResult:X8}) - {args.ErrorMessage}";
        Debug.WriteLine($"[BackgroundMusicService] {errorDetails}");
        SwitchCastDiagnostics.LogWarning(nameof(BackgroundMusicService), errorDetails);

        LastErrorMessage = "Audio file playback failed.";
        Stop();
    }

    public async Task LoadPlaylistsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                CreateDefaultPlaylistIfEmpty();
                return;
            }

            var json = await File.ReadAllTextAsync(_storageFilePath).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<PersistedPlaylistsData>(json, JsonOptions);
            if (data is null || data.Playlists.Count == 0)
            {
                CreateDefaultPlaylistIfEmpty();
                return;
            }

            lock (_stateLock)
            {
                _playlists.Clear();
                foreach (var pData in data.Playlists)
                {
                    var playlist = new MusicPlaylist
                    {
                        Id = pData.Id ?? Guid.NewGuid().ToString("N"),
                        Name = pData.Name ?? "Playlist"
                    };

                    if (pData.Tracks is not null)
                    {
                        foreach (var tData in pData.Tracks)
                        {
                            bool exists = !string.IsNullOrWhiteSpace(tData.FilePath) && File.Exists(tData.FilePath);
                            playlist.Tracks.Add(new MusicTrack
                            {
                                Id = tData.Id ?? Guid.NewGuid().ToString("N"),
                                Title = tData.Title ?? (exists ? Path.GetFileNameWithoutExtension(tData.FilePath ?? string.Empty) : "Missing Track"),
                                Artist = tData.Artist,
                                FilePath = tData.FilePath ?? string.Empty,
                                Duration = tData.Duration,
                                IsAvailable = exists
                            });
                        }
                    }

                    _playlists.Add(playlist);
                }

                RepeatMode = data.RepeatMode;
                IsShuffleEnabled = data.IsShuffleEnabled;
                if (data.Volume >= 0.0 && data.Volume <= 1.0)
                {
                    Volume = data.Volume;
                }
                IsMuted = data.IsMuted;

                if (!string.IsNullOrEmpty(data.ActivePlaylistId))
                {
                    CurrentPlaylist = _playlists.FirstOrDefault(p => p.Id == data.ActivePlaylistId) ?? _playlists.FirstOrDefault();
                }
                else
                {
                    CurrentPlaylist = _playlists.FirstOrDefault();
                }
            }

            PlaylistChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(LoadPlaylistsAsync), ex);
            CreateDefaultPlaylistIfEmpty();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void CreateDefaultPlaylistIfEmpty()
    {
        lock (_stateLock)
        {
            if (_playlists.Count == 0)
            {
                var defaultPlaylist = new MusicPlaylist
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "Default Playlist"
                };
                _playlists.Add(defaultPlaylist);
                CurrentPlaylist = defaultPlaylist;
            }
        }
        PlaylistChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SavePlaylistsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            PersistedPlaylistsData data;
            lock (_stateLock)
            {
                data = new PersistedPlaylistsData
                {
                    ActivePlaylistId = CurrentPlaylist?.Id,
                    RepeatMode = RepeatMode,
                    IsShuffleEnabled = IsShuffleEnabled,
                    Volume = Volume,
                    IsMuted = IsMuted,
                    Playlists = _playlists.Select(p => new PersistedPlaylistData
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Tracks = p.Tracks.Select(t => new PersistedTrackData
                        {
                            Id = t.Id,
                            Title = t.Title,
                            Artist = t.Artist,
                            FilePath = t.FilePath,
                            Duration = t.Duration
                        }).ToList()
                    }).ToList()
                };
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_storageFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(SavePlaylistsAsync), ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public MusicPlaylist CreatePlaylist(string name)
    {
        var playlist = new MusicPlaylist
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? "New Playlist" : name.Trim()
        };

        lock (_stateLock)
        {
            _playlists.Add(playlist);
            if (CurrentPlaylist is null)
            {
                CurrentPlaylist = playlist;
            }
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePlaylistsAsync();
        return playlist;
    }

    public void DeletePlaylist(string playlistId)
    {
        lock (_stateLock)
        {
            var target = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (target is null)
            {
                return;
            }

            if (CurrentPlaylist?.Id == playlistId)
            {
                Stop();
                _playlists.Remove(target);
                CurrentPlaylist = _playlists.FirstOrDefault();
            }
            else
            {
                _playlists.Remove(target);
            }

            if (_playlists.Count == 0)
            {
                CreateDefaultPlaylistIfEmpty();
            }
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePlaylistsAsync();
    }

    public void RenamePlaylist(string playlistId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        lock (_stateLock)
        {
            var target = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (target is not null)
            {
                target.Name = newName.Trim();
            }
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePlaylistsAsync();
    }

    public void SetActivePlaylist(string playlistId)
    {
        lock (_stateLock)
        {
            var target = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (target is null || target == CurrentPlaylist)
            {
                return;
            }

            bool wasPlaying = IsPlaying;
            Stop();
            CurrentPlaylist = target;
            CurrentTrack = null;
            CurrentTrackIndex = -1;
            _shuffleHistory.Clear();
            PlaylistChanged?.Invoke(this, EventArgs.Empty);

            if (wasPlaying && target.Tracks.Count > 0)
            {
                _ = PlayAsync();
            }
        }

        _ = SavePlaylistsAsync();
    }

    public async Task AddTracksAsync(string playlistId, IEnumerable<string> filePaths)
    {
        MusicPlaylist? playlist;
        lock (_stateLock)
        {
            playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
        }

        if (playlist is null)
        {
            return;
        }

        var newTracks = new List<MusicTrack>();
        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            lock (_stateLock)
            {
                if (playlist.Tracks.Any(t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            var track = await CreateTrackFromFileAsync(path).ConfigureAwait(false);
            newTracks.Add(track);
        }

        if (newTracks.Count == 0)
        {
            return;
        }

        lock (_stateLock)
        {
            foreach (var t in newTracks)
            {
                playlist.Tracks.Add(t);
            }
            playlist.NotifyTracksChanged();
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        await SavePlaylistsAsync().ConfigureAwait(false);
    }

    private static async Task<MusicTrack> CreateTrackFromFileAsync(string filePath)
    {
        string title = Path.GetFileNameWithoutExtension(filePath);
        string? artist = null;
        TimeSpan duration = TimeSpan.Zero;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var musicProps = await file.Properties.GetMusicPropertiesAsync();
            if (!string.IsNullOrWhiteSpace(musicProps.Title))
            {
                title = musicProps.Title;
            }
            if (!string.IsNullOrWhiteSpace(musicProps.Artist))
            {
                artist = musicProps.Artist;
            }
            duration = musicProps.Duration;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BackgroundMusicService] Could not read audio properties: {ex.Message}");
        }

        return new MusicTrack
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Artist = artist,
            FilePath = filePath,
            Duration = duration,
            IsAvailable = true
        };
    }

    public void RemoveTrack(string playlistId, string trackId)
    {
        lock (_stateLock)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist is null)
            {
                return;
            }

            var track = playlist.Tracks.FirstOrDefault(t => t.Id == trackId);
            if (track is null)
            {
                return;
            }

            bool isCurrent = CurrentTrack?.Id == trackId;
            playlist.Tracks.Remove(track);
            playlist.NotifyTracksChanged();

            if (isCurrent)
            {
                if (playlist.Tracks.Count > 0)
                {
                    _ = NextTrackAsync();
                }
                else
                {
                    Stop();
                }
            }
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePlaylistsAsync();
    }

    public void MoveTrack(string playlistId, int oldIndex, int newIndex)
    {
        lock (_stateLock)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist is null || oldIndex < 0 || oldIndex >= playlist.Tracks.Count ||
                newIndex < 0 || newIndex >= playlist.Tracks.Count || oldIndex == newIndex)
            {
                return;
            }

            playlist.Tracks.Move(oldIndex, newIndex);
            if (CurrentTrack is not null)
            {
                CurrentTrackIndex = playlist.Tracks.IndexOf(CurrentTrack);
            }
        }

        PlaylistChanged?.Invoke(this, EventArgs.Empty);
        _ = SavePlaylistsAsync();
    }

    public async Task PlayAsync()
    {
        if (CurrentPlaylist is null || CurrentPlaylist.Tracks.Count == 0)
        {
            return;
        }

        if (IsPaused && _player is not null)
        {
            Resume();
            return;
        }

        int targetIndex = CurrentTrackIndex;
        if (targetIndex < 0 || targetIndex >= CurrentPlaylist.Tracks.Count)
        {
            targetIndex = 0;
        }

        var track = CurrentPlaylist.Tracks[targetIndex];
        await PlayTrackInternalAsync(track, targetIndex).ConfigureAwait(false);
    }

    public async Task PlayTrackAsync(MusicTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (CurrentPlaylist is null)
        {
            return;
        }

        int index = CurrentPlaylist.Tracks.IndexOf(track);
        if (index < 0)
        {
            return;
        }

        await PlayTrackInternalAsync(track, index).ConfigureAwait(false);
    }

    private Task PlayTrackInternalAsync(MusicTrack track, int index)
    {
        if (!File.Exists(track.FilePath))
        {
            track.IsAvailable = false;
            LastErrorMessage = $"Audio file '{track.Title}' not found.";
            SwitchCastDiagnostics.LogWarning(
                nameof(BackgroundMusicService),
                $"Audio file not found on disk: '{track.Title}' ({track.FilePath})");

            Stop();
            return Task.CompletedTask;
        }

        try
        {
            if (_player is null)
            {
                InitializePlayer();
            }

            if (_player is null)
            {
                return Task.CompletedTask;
            }

            _currentMediaSource?.Dispose();
            _currentMediaSource = MediaSource.CreateFromUri(new Uri(track.FilePath));
            _player.Source = _currentMediaSource;
            _player.Play();

            CurrentTrack = track;
            CurrentTrackIndex = index;
            Duration = track.Duration;
            Position = TimeSpan.Zero;
            IsPlaying = true;
            IsPaused = false;
            LastErrorMessage = null;

            StartThrottledPositionTimer();

            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(PlayTrackInternalAsync), ex);
            LastErrorMessage = $"Failed to play track: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (!IsPlaying || _player is null)
        {
            return;
        }

        try
        {
            _player.Pause();
            IsPlaying = false;
            IsPaused = true;
            StopPositionTimer();
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(Pause), ex);
        }
    }

    public void Resume()
    {
        if (!IsPaused || _player is null)
        {
            return;
        }

        try
        {
            _player.Play();
            IsPlaying = true;
            IsPaused = false;
            StartThrottledPositionTimer();
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(Resume), ex);
        }
    }

    public void Stop()
    {
        if (_player is null)
        {
            IsPlaying = false;
            IsPaused = false;
            Position = TimeSpan.Zero;
            return;
        }

        try
        {
            _player.Pause();
            if (_player.PlaybackSession is not null)
            {
                _player.PlaybackSession.Position = TimeSpan.Zero;
            }
        }
        catch
        {
            // Ignore transient shutdown errors
        }

        IsPlaying = false;
        IsPaused = false;
        Position = TimeSpan.Zero;
        StopPositionTimer();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task NextTrackAsync()
    {
        if (CurrentPlaylist is null || CurrentPlaylist.Tracks.Count == 0)
        {
            Stop();
            return;
        }

        int count = CurrentPlaylist.Tracks.Count;
        int nextIndex;

        if (IsShuffleEnabled && count > 1)
        {
            if (_shuffleHistory.Count >= count)
            {
                _shuffleHistory.Clear();
            }

            var unplayed = Enumerable.Range(0, count).Where(i => !_shuffleHistory.Contains(i) && i != CurrentTrackIndex).ToList();
            if (unplayed.Count > 0)
            {
                nextIndex = unplayed[_random.Next(unplayed.Count)];
            }
            else
            {
                nextIndex = _random.Next(count);
            }
            _shuffleHistory.Add(nextIndex);
        }
        else
        {
            nextIndex = CurrentTrackIndex + 1;
            if (nextIndex >= count)
            {
                if (RepeatMode == MusicRepeatMode.RepeatOff)
                {
                    Stop();
                    return;
                }
                // RepeatAll: wrap around
                nextIndex = 0;
            }
        }

        var track = CurrentPlaylist.Tracks[nextIndex];
        await PlayTrackInternalAsync(track, nextIndex).ConfigureAwait(false);
    }

    public async Task PreviousTrackAsync()
    {
        if (CurrentPlaylist is null || CurrentPlaylist.Tracks.Count == 0)
        {
            return;
        }

        // If played more than 3 seconds into current track, restart current track
        if (Position.TotalSeconds > 3.0)
        {
            Seek(TimeSpan.Zero);
            return;
        }

        int count = CurrentPlaylist.Tracks.Count;
        int prevIndex = CurrentTrackIndex - 1;
        if (prevIndex < 0)
        {
            prevIndex = count - 1;
        }

        var track = CurrentPlaylist.Tracks[prevIndex];
        await PlayTrackInternalAsync(track, prevIndex).ConfigureAwait(false);
    }

    public void Seek(TimeSpan position)
    {
        if (_player?.PlaybackSession is null)
        {
            return;
        }

        try
        {
            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }
            else if (position > Duration)
            {
                position = Duration;
            }

            _player.PlaybackSession.Position = position;
            Position = position;
            PositionChanged?.Invoke(this, position);
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(BackgroundMusicService), nameof(Seek), ex);
        }
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            _ = PlayAsync();
        }
    }

    partial void OnVolumeChanged(double value)
    {
        if (_player is not null)
        {
            _player.Volume = Math.Clamp(value, 0.0, 1.0);
        }
        _ = SavePlaylistsAsync();
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (_player is not null)
        {
            _player.IsMuted = value;
        }
        _ = SavePlaylistsAsync();
    }

    partial void OnRepeatModeChanged(MusicRepeatMode value)
    {
        _ = SavePlaylistsAsync();
    }

    partial void OnIsShuffleEnabledChanged(bool value)
    {
        _shuffleHistory.Clear();
        _ = SavePlaylistsAsync();
    }

    private void StartThrottledPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new System.Threading.Timer(OnPositionTimerTick, null, 250, 250);
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    private void OnPositionTimerTick(object? state)
    {
        if (_disposed || !IsPlaying || _player?.PlaybackSession is null)
        {
            return;
        }

        try
        {
            var pos = _player.PlaybackSession.Position;
            var dur = _player.PlaybackSession.NaturalDuration;

            if (dur != TimeSpan.Zero && dur != Duration)
            {
                Duration = dur;
                if (CurrentTrack is not null && CurrentTrack.Duration == TimeSpan.Zero)
                {
                    CurrentTrack.Duration = dur;
                }
            }

            Position = pos;
            PositionChanged?.Invoke(this, pos);
        }
        catch
        {
            // Ignore timer read during track transition
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopPositionTimer();

        if (_player is not null)
        {
            try
            {
                _player.MediaEnded -= OnPlayerMediaEnded;
                _player.MediaFailed -= OnPlayerMediaFailed;
                _player.Pause();
                _player.Source = null;
                _player.Dispose();
            }
            catch
            {
                // Silently ignore cleanup errors
            }
            _player = null;
        }

        _currentMediaSource?.Dispose();
        _currentMediaSource = null;
        _fileLock.Dispose();
    }

    private sealed class PersistedPlaylistsData
    {
        public string? ActivePlaylistId { get; set; }
        public MusicRepeatMode RepeatMode { get; set; }
        public bool IsShuffleEnabled { get; set; }
        public double Volume { get; set; } = 0.5;
        public bool IsMuted { get; set; }
        public List<PersistedPlaylistData> Playlists { get; set; } = [];
    }

    private sealed class PersistedPlaylistData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<PersistedTrackData> Tracks { get; set; } = [];
    }

    private sealed class PersistedTrackData
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? FilePath { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
