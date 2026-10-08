namespace SwitchCast.Services.Capture;

/// <summary>
/// Operational states of the screen capture engine.
/// </summary>
public enum CaptureState
{
    /// <summary>
    /// No capture session active.
    /// </summary>
    Idle,

    /// <summary>
    /// Capture item and frame pool are being initialized.
    /// </summary>
    Starting,

    /// <summary>
    /// Frames are actively being acquired and rendered.
    /// </summary>
    Capturing,

    /// <summary>
    /// Capture session is safely stopping and releasing GPU resources.
    /// </summary>
    Stopping,

    /// <summary>
    /// Capture failed or source closed unexpectedly.
    /// </summary>
    Failed
}
