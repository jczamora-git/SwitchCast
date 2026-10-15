using Windows.Graphics.Imaging;
using SwitchCast.Models;

namespace SwitchCast.Services.Camera;

/// <summary>
/// Service coordinating live camera video frame acquisition, device session lifecycle, and error handling.
/// </summary>
public interface ICameraCaptureService : IAsyncDisposable
{
    /// <summary>
    /// Whether an active camera session is currently acquiring frames.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// The camera source currently active, or null if stopped.
    /// </summary>
    CameraSource? ActiveCamera { get; }

    /// <summary>
    /// Last error encountered during camera initialization or capture.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Raised when a new camera frame arrives.
    /// </summary>
    event EventHandler<SoftwareBitmap>? FrameArrived;

    /// <summary>
    /// Raised when camera capture stops or is interrupted.
    /// </summary>
    event EventHandler? CaptureStopped;

    /// <summary>
    /// Starts live camera capture on the specified camera source.
    /// </summary>
    Task<bool> StartCaptureAsync(CameraSource camera, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops live camera capture and releases camera hardware resources immediately.
    /// </summary>
    Task StopCaptureAsync();
}
