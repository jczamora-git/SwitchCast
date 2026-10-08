using SwitchCast.Models;
using Windows.Graphics.Capture;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Factory creating WinRT GraphicsCaptureItem instances from domain PresentationSource entities.
/// </summary>
public interface IGraphicsCaptureItemFactory
{
    /// <summary>
    /// Checks if Windows.Graphics.Capture is supported on the current operating system.
    /// </summary>
    bool IsCaptureSupported();

    /// <summary>
    /// Creates a GraphicsCaptureItem for the specified source, verifying native handle validity.
    /// </summary>
    /// <param name="source">The window or monitor source to capture.</param>
    /// <returns>A validated GraphicsCaptureItem.</returns>
    GraphicsCaptureItem CreateItemForSource(CaptureSource source);
}
