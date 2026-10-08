using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Event arguments containing acquired frame data from a capture session.
/// </summary>
public sealed class FrameArrivedEventArgs : EventArgs
{
    public FrameArrivedEventArgs(RefCountedSoftwareBitmap sharedBitmap, long generation = 0)
    {
        SharedBitmap = sharedBitmap ?? throw new ArgumentNullException(nameof(sharedBitmap));
        SoftwareBitmap = sharedBitmap.Bitmap;
        Generation = generation;
    }

    public FrameArrivedEventArgs(Direct3D11CaptureFrame? frame, SoftwareBitmap? softwareBitmap = null, long generation = 0)
    {
        Frame = frame;
        SoftwareBitmap = softwareBitmap;
        Generation = generation;
    }

    public FrameArrivedEventArgs(SoftwareBitmap softwareBitmap, long generation = 0)
    {
        SoftwareBitmap = softwareBitmap ?? throw new ArgumentNullException(nameof(softwareBitmap));
        Generation = generation;
    }

    /// <summary>
    /// The acquired Direct3D 11 capture frame, if available.
    /// </summary>
    public Direct3D11CaptureFrame? Frame { get; }

    /// <summary>
    /// The independently owned SoftwareBitmap representation.
    /// </summary>
    public SoftwareBitmap? SoftwareBitmap { get; }

    /// <summary>
    /// Thread-safe ref-counted bitmap wrapper ensuring deterministic multi-renderer disposal without memory leaks.
    /// </summary>
    public RefCountedSoftwareBitmap? SharedBitmap { get; }

    /// <summary>
    /// Capture session generation identifier for dropping stale frames across source switches.
    /// </summary>
    public long Generation { get; }
}

