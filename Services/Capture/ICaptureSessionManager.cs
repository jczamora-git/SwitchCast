using Windows.Graphics.Capture;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Lifecycle manager for a single Windows.Graphics.Capture session and associated frame pool.
/// </summary>
public interface ICaptureSessionManager : IDisposable
{
    /// <summary>
    /// Event raised when a new frame is acquired from the capture stream.
    /// </summary>
    event EventHandler<FrameArrivedEventArgs>? FrameArrived;

    /// <summary>
    /// Event raised when the captured window is closed or display is disconnected.
    /// </summary>
    event EventHandler? SourceClosed;

    /// <summary>
    /// Event raised when an unrecoverable error occurs in the capture stream.
    /// </summary>
    event EventHandler<Exception>? CaptureError;

    /// <summary>
    /// Gets whether a session is currently active and acquiring frames.
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// Starts capture for the specified item using the provided Direct3D 11 device.
    /// </summary>
    void StartCapture(GraphicsCaptureItem item, IDirect3D11DeviceProvider deviceProvider);

    /// <summary>
    /// Stops the active capture session and disposes frame pools deterministically.
    /// </summary>
    void StopCapture();
}
