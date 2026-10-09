using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using Windows.Media.Playback;

namespace SwitchCast.Services.Media;

/// <summary>
/// Authoritative service governing direct local image rendering and video media playback inside Presentation Output.
/// </summary>
public interface IMediaPresentationService : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Currently loaded static image source for Presentation Output, or null if inactive.
    /// </summary>
    ImageSource? DirectImageSource { get; }

    /// <summary>
    /// Authoritative native Windows MediaPlayer instance used for direct video playback.
    /// </summary>
    MediaPlayer? Player { get; }

    /// <summary>
    /// Currently active media file source (Image or Video), or null if none.
    /// </summary>
    MediaFileSource? ActiveMediaSource { get; }

    /// <summary>
    /// Gets whether a video is actively playing.
    /// </summary>
    bool IsVideoPlaying { get; }

    /// <summary>
    /// Gets whether the active video is paused.
    /// </summary>
    bool IsVideoPaused { get; }

    /// <summary>
    /// Gets whether the active video has reached the end of playback.
    /// </summary>
    bool IsVideoEnded { get; }

    /// <summary>
    /// Gets or sets whether the active video loops automatically.
    /// </summary>
    bool IsLooping { get; set; }

    /// <summary>
    /// Current playback position of the active video.
    /// </summary>
    TimeSpan Position { get; }

    /// <summary>
    /// Total duration of the active video.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Last error message encountered during media decoding or playback.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Event raised when playback state, position, or media source changes.
    /// </summary>
    event EventHandler? MediaStateChanged;

    /// <summary>
    /// Asynchronously decodes and displays a static image source.
    /// </summary>
    Task LoadImageAsync(ImageMediaSource imageSource);

    /// <summary>
    /// Clears and releases the currently displayed static image.
    /// </summary>
    void ClearImage();

    /// <summary>
    /// Asynchronously prepares and starts direct playback of a local video file.
    /// </summary>
    Task PlayVideoAsync(VideoMediaSource videoSource);

    /// <summary>
    /// Stops and unloads active video playback.
    /// </summary>
    Task StopVideoAsync();

    /// <summary>
    /// Pauses active video playback at current position.
    /// </summary>
    void PauseVideo();

    /// <summary>
    /// Resumes active video playback from current position.
    /// </summary>
    void ResumeVideo();

    /// <summary>
    /// Restarts active video playback from the beginning (00:00).
    /// </summary>
    void RestartVideo();

    /// <summary>
    /// Toggles automatic looping for the current video.
    /// </summary>
    void ToggleLoop();

    /// <summary>
    /// Seeks playback to the specified position.
    /// </summary>
    void Seek(TimeSpan position);
}
