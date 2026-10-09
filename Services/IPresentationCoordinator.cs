using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Coordinates presentation output window state, live source presentation, freeze/pause, blackout, and frame delivery.
/// </summary>
public interface IPresentationCoordinator : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Current operational status of the presentation.
    /// </summary>
    PresentationStatus Status { get; }

    /// <summary>
    /// Active source switching mode for presenter dock and global hotkeys.
    /// </summary>
    PresenterSwitchMode SwitchMode { get; }

    /// <summary>
    /// Logical selection cursor in the queued sources list.
    /// </summary>
    CaptureSource? SelectedSource { get; }

    /// <summary>
    /// Last confirmed foreground activated application window source.
    /// </summary>
    CaptureSource? ForegroundSource { get; }

    /// <summary>
    /// Currently presented capture source (on-air), or null if idle.
    /// </summary>
    CaptureSource? CurrentPresentationSource { get; }

    /// <summary>
    /// Gets whether the presentation output window is open.
    /// </summary>
    bool IsOutputWindowOpen { get; }

    /// <summary>
    /// Gets whether presentation is actively live and rendering frames.
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Gets whether presentation is temporarily paused (frozen frame).
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Gets whether presentation is in blackout mode (opaque black).
    /// </summary>
    bool IsBlackout { get; }

    /// <summary>
    /// ImageSource for the presentation output canvas (DirectX capture stream).
    /// </summary>
    ImageSource? PresentationImageSource { get; }

    /// <summary>
    /// Static image source for direct image presentation.
    /// </summary>
    ImageSource? DirectImageSource { get; }

    /// <summary>
    /// Native Windows MediaPlayer instance for direct video presentation.
    /// </summary>
    Windows.Media.Playback.MediaPlayer? MediaPlayer { get; }

    /// <summary>
    /// Direct media presentation service instance.
    /// </summary>
    Media.IMediaPresentationService MediaPresentationService { get; }

    /// <summary>
    /// Whether the active on-air source is a direct media file (Image or Video).
    /// </summary>
    bool IsActiveSourceMedia { get; }

    /// <summary>
    /// Whether the active on-air source is a video file.
    /// </summary>
    bool IsActiveSourceVideo { get; }

    /// <summary>
    /// Whether the active on-air source is a static image file.
    /// </summary>
    bool IsActiveSourceImage { get; }

    /// <summary>
    /// Gets the last error message encountered during presentation operations.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Opens or activates the presentation output window.
    /// </summary>
    Task OpenOutputWindowAsync();

    /// <summary>
    /// Closes the presentation output window.
    /// </summary>
    Task CloseOutputWindowAsync();

    /// <summary>
    /// Starts presenting the specified source (or currently selected source) to the presentation output window.
    /// </summary>
    Task StartPresentationAsync(CaptureSource? source = null);

    /// <summary>
    /// Stops presenting to the presentation output window.
    /// </summary>
    Task StopPresentationAsync();

    /// <summary>
    /// Switches the active presentation source without closing or recreating the output window.
    /// </summary>
    Task SwitchPresentationSourceAsync(CaptureSource newSource);

    /// <summary>
    /// Pauses presentation output, freezing the last active frame.
    /// </summary>
    Task PausePresentationAsync();

    /// <summary>
    /// Resumes live presentation output from pause or blackout.
    /// </summary>
    Task ResumePresentationAsync();

    /// <summary>
    /// Toggles presentation blackout mode.
    /// </summary>
    Task ToggleBlackoutAsync();

    /// <summary>
    /// Sets the source switching mode (LiveOnly, ActiveAndLive, ActiveOnly).
    /// </summary>
    Task SetSwitchModeAsync(PresenterSwitchMode mode);

    /// <summary>
    /// Switches to the specified source following the active PresenterSwitchMode rules.
    /// </summary>
    Task ExecuteSourceSwitchAsync(CaptureSource targetSource);

    /// <summary>
    /// Switches to the next available source in the queued sources list following active switch mode rules.
    /// </summary>
    Task SwitchToNextSourceAsync();

    /// <summary>
    /// Switches to the previous available source in the queued sources list following active switch mode rules.
    /// </summary>
    Task SwitchToPreviousSourceAsync();

    /// <summary>
    /// Switches to the source at the specified zero-based index in the queued sources list following active switch mode rules.
    /// </summary>
    Task SwitchToSourceIndexAsync(int index);
}
