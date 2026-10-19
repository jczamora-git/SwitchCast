namespace SwitchCast.Models.Music;

/// <summary>
/// Repeat playback modes for background music playlists.
/// </summary>
public enum MusicRepeatMode
{
    /// <summary>
    /// Stop playback when the playlist finishes.
    /// </summary>
    RepeatOff = 0,

    /// <summary>
    /// Repeat the current track indefinitely.
    /// </summary>
    RepeatOne = 1,

    /// <summary>
    /// Loop through all tracks in the playlist and start over.
    /// </summary>
    RepeatAll = 2
}
