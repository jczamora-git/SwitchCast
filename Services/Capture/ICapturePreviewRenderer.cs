using Microsoft.UI.Xaml.Media;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Renderer presenting live capture frames onto a WinUI 3 preview surface.
/// </summary>
public interface ICapturePreviewRenderer : IDisposable
{
    /// <summary>
    /// The ImageSource bound to the XAML preview control.
    /// </summary>
    ImageSource PreviewImageSource { get; }

    /// <summary>
    /// Gets or sets whether the preview renderer is enabled to process incoming frames.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Processes and renders an acquired Direct3D 11 capture frame.
    /// </summary>
    Task RenderFrameAsync(Direct3D11CaptureFrame frame);

    /// <summary>
    /// Renders an independently owned SoftwareBitmap directly to the preview surface.
    /// </summary>
    Task RenderBitmapAsync(SoftwareBitmap bitmap);

    /// <summary>
    /// Renders a thread-safe ref-counted bitmap with preview rate-limiting (~15 FPS) and session generation validation.
    /// </summary>
    Task RenderSharedBitmapAsync(RefCountedSoftwareBitmap sharedBitmap, long generation = 0);

    /// <summary>
    /// Clears the active preview image and releases frame buffers.
    /// </summary>
    void Clear();
}
