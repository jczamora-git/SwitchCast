using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Renderer presenting live capture frames onto the dedicated Presentation Output Window.
/// </summary>
public interface IPresentationOutputRenderer : IDisposable
{
    /// <summary>
    /// The ImageSource bound to the presentation output XAML canvas.
    /// </summary>
    ImageSource? PresentationImageSource { get; }

    /// <summary>
    /// Gets whether frame updates are currently frozen (paused).
    /// </summary>
    bool IsFrozen { get; }

    /// <summary>
    /// Processes and renders an acquired Direct3D 11 capture frame to the presentation output surface.
    /// </summary>
    Task RenderFrameAsync(Direct3D11CaptureFrame frame);

    /// <summary>
    /// Renders an independently owned SoftwareBitmap directly to the presentation output surface.
    /// </summary>
    Task RenderBitmapAsync(SoftwareBitmap bitmap);

    /// <summary>
    /// Renders a thread-safe ref-counted bitmap directly to the presentation output surface.
    /// </summary>
    Task RenderSharedBitmapAsync(RefCountedSoftwareBitmap sharedBitmap);

    /// <summary>
    /// Freezes the current frame on screen (for pause).
    /// </summary>
    void Freeze();

    /// <summary>
    /// Resumes live frame updates after pause.
    /// </summary>
    void Resume();

    /// <summary>
    /// Clears the presentation output image.
    /// </summary>
    void Clear();
}
