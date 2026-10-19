using System.ComponentModel;
using SwitchCast.Models.Music;

namespace SwitchCast.Services.Music;

/// <summary>
/// Authoritative service governing independent background music playback, playlist management, and audio transport.
/// Uses an isolated, dedicated audio-only MediaPlayer instance completely separate from video presentation.
/// </summary>
public interface IBackgroundMusicService : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Event raised when playback state (Playing, Paused, Stopped) changes.
    /// </summary>
    event EventHandler? PlaybackStateChanged;

    /// <summary>
    /// Event raised when the current track changes.
    /// </summary>
    event EventHandler? TrackChanged;

    /// <summary>
    /// Event raised when the playlist collection or active playlist structure changes.
    /// </summary>
    event EventHandler? PlaylistChanged;

    /// <summary>
    /// Event raised when playback position updates (throttled to avoid UI flooding).
    /// </summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>
    /// The currently active playlist, or null if no playlist is selected.
    /// </summary>
    MusicPlaylist? CurrentPlaylist { get; }

    /// <summary>
    /// The currently loaded or playing track, or null if none.
    /// </summary>
    MusicTrack? CurrentTrack { get; }

    /// <summary>
    /// Zero-based index of the currently playing track within CurrentPlaylist.
    /// </summary>
    int CurrentTrackIndex { get; }

    /// <summary>
    /// Whether background music is actively playing.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// Whether background music is currently paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Gets or sets the repeat mode (RepeatOff, RepeatOne, RepeatAll).
    /// </summary>
    MusicRepeatMode RepeatMode { get; set; }

    /// <summary>
    /// Gets or sets whether shuffle mode is enabled.
    /// </summary>
    bool IsShuffleEnabled { get; set; }

    /// <summary>
    /// Gets or sets the normalized background music volume (0.0 to 1.0).
    /// </summary>
    double Volume { get; set; }

    /// <summary>
    /// Gets or sets whether background music is currently muted.
    /// </summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Current playback position of the active track.
    /// </summary>
    TimeSpan Position { get; }

    /// <summary>
    /// Total duration of the active track.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// All saved music playlists.
    /// </summary>
    IReadOnlyList<MusicPlaylist> Playlists { get; }

    /// <summary>
    /// Last error message encountered during audio playback or loading.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Asynchronously loads persisted playlists from local storage.
    /// </summary>
    Task LoadPlaylistsAsync();

    /// <summary>
    /// Asynchronously saves playlists to local storage.
    /// </summary>
    Task SavePlaylistsAsync();

    /// <summary>
    /// Creates a new playlist with the specified name.
    /// </summary>
    MusicPlaylist CreatePlaylist(string name);

    /// <summary>
    /// Deletes the playlist with the specified ID.
    /// </summary>
    void DeletePlaylist(string playlistId);

    /// <summary>
    /// Renames the playlist with the specified ID.
    /// </summary>
    void RenamePlaylist(string playlistId, string newName);

    /// <summary>
    /// Sets the specified playlist as active.
    /// </summary>
    void SetActivePlaylist(string playlistId);

    /// <summary>
    /// Adds tracks from the specified file paths to the given playlist.
    /// </summary>
    Task AddTracksAsync(string playlistId, IEnumerable<string> filePaths);

    /// <summary>
    /// Removes a track from the playlist.
    /// </summary>
    void RemoveTrack(string playlistId, string trackId);

    /// <summary>
    /// Reorders a track within a playlist.
    /// </summary>
    void MoveTrack(string playlistId, int oldIndex, int newIndex);

    /// <summary>
    /// Starts or resumes playback of the current playlist/track.
    /// </summary>
    Task PlayAsync();

    /// <summary>
    /// Starts playback of a specific track within the active playlist.
    /// </summary>
    Task PlayTrackAsync(MusicTrack track);

    /// <summary>
    /// Pauses background music playback.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes paused background music playback.
    /// </summary>
    void Resume();

    /// <summary>
    /// Stops playback and resets position to beginning.
    /// </summary>
    void Stop();

    /// <summary>
    /// Advances to the next track according to repeat and shuffle settings.
    /// </summary>
    Task NextTrackAsync();

    /// <summary>
    /// Retreats to the previous track or restarts the current track.
    /// </summary>
    Task PreviousTrackAsync();

    /// <summary>
    /// Seeks playback to the specified position with an optional diagnostic request source.
    /// </summary>
    void Seek(TimeSpan position, string? source = null);

    /// <summary>
    /// Toggles between Play and Pause.
    /// </summary>
    void TogglePlayPause();
}
