using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Event arguments containing acquired frame data from a capture session.
/// </summary>
public sealed class FrameArrivedEventArgs : EventArgs
{
    public FrameArrivedEventArgs(Direct3D11CaptureFrame? frame, SoftwareBitmap? softwareBitmap = null)
    {
        Frame = frame;
        SoftwareBitmap = softwareBitmap;
    }

    public FrameArrivedEventArgs(SoftwareBitmap softwareBitmap)
    {
        SoftwareBitmap = softwareBitmap ?? throw new ArgumentNullException(nameof(softwareBitmap));
    }

    /// <summary>
    /// The acquired Direct3D 11 capture frame, if available.
    /// </summary>
    public Direct3D11CaptureFrame? Frame { get; }

    /// <summary>
    /// The independently owned SoftwareBitmap representation.
    /// </summary>
    public SoftwareBitmap? SoftwareBitmap { get; }
}

