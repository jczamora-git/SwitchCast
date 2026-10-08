using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Coordinates capture item creation, Direct3D 11 device lifecycle, session acquisition, and live preview rendering.
/// </summary>
public interface ICaptureCoordinator : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Current operational state of the capture pipeline.
    /// </summary>
    CaptureState State { get; }

    /// <summary>
    /// The source currently active in live preview, or null if stopped.
    /// </summary>
    CaptureSource? CurrentPreviewSource { get; }

    /// <summary>
    /// Gets the last error message encountered during capture operations.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Gets the ImageSource bound to the live preview UI.
    /// </summary>
    ImageSource? PreviewImageSource { get; }

    /// <summary>
    /// Starts live capture preview for the specified presentation source.
    /// </summary>
    Task StartPreviewAsync(CaptureSource source);

    /// <summary>
    /// Stops the active live capture preview and releases associated GPU resources.
    /// </summary>
    Task StopPreviewAsync();

    /// <summary>
    /// Safely switches live capture preview to another presentation source.
    /// </summary>
    Task SwitchPreviewSourceAsync(CaptureSource newSource);
}
