using Windows.Graphics.Capture;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Event arguments containing acquired frame data from a capture session.
/// </summary>
public sealed class FrameArrivedEventArgs : EventArgs
{
    public FrameArrivedEventArgs(Direct3D11CaptureFrame frame)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    /// <summary>
    /// The acquired Direct3D 11 capture frame. Caller is responsible for lifetime or processing before disposal.
    /// </summary>
    public Direct3D11CaptureFrame Frame { get; }
}
