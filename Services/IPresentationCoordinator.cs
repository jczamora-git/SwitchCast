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
    /// Currently presented capture source, or null if idle.
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
    /// ImageSource for the presentation output canvas.
    /// </summary>
    ImageSource? PresentationImageSource { get; }

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
}
