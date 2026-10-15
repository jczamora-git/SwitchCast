namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Service managing interactive PowerPoint Live slideshow sessions via windowed PowerPoint COM automation.
/// Keeps the native slideshow windowed on the presenter desktop while SwitchCast captures and projects it.
/// </summary>
public interface IPowerPointLiveService
{
    /// <summary>
    /// Whether Microsoft PowerPoint is installed and registered on this system.
    /// </summary>
    bool IsPowerPointInstalled { get; }

    /// <summary>
    /// Whether a live windowed slideshow is currently active.
    /// </summary>
    bool IsLiveSlideshowRunning { get; }

    /// <summary>
    /// Native HWND of the windowed PowerPoint slideshow window, or IntPtr.Zero if not running.
    /// </summary>
    nint SlideshowWindowHandle { get; }

    /// <summary>
    /// Current 1-based slide index of the running slideshow.
    /// </summary>
    int CurrentSlideIndex { get; }

    /// <summary>
    /// Total slide count of the active presentation.
    /// </summary>
    int TotalSlideCount { get; }

    /// <summary>
    /// Last error encountered during PowerPoint Live operations.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Raised when a live slideshow is started.
    /// </summary>
    event EventHandler? SlideshowStarted;

    /// <summary>
    /// Raised when the live slideshow concludes or closes.
    /// </summary>
    event EventHandler? SlideshowEnded;

    /// <summary>
    /// Raised when the active slide or animation step changes in the live slideshow.
    /// </summary>
    event EventHandler? SlidePositionChanged;

    /// <summary>
    /// Launches a windowed PowerPoint slideshow for the specified presentation file.
    /// </summary>
    Task<bool> StartSlideshowAsync(string pptxPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances to the next animation step or slide.
    /// </summary>
    Task NextAsync();

    /// <summary>
    /// Recedes to the previous animation step or slide.
    /// </summary>
    Task PreviousAsync();

    /// <summary>
    /// Jumps directly to a specific slide index in the live slideshow.
    /// </summary>
    Task GoToSlideAsync(int slideIndex);

    /// <summary>
    /// Stops the live slideshow and terminates the PowerPoint slideshow session cleanly.
    /// </summary>
    Task StopSlideshowAsync();
}
