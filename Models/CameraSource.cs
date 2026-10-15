namespace SwitchCast.Models;

/// <summary>
/// Domain model representing a connected USB or integrated camera video capture device.
/// </summary>
public record CameraSource : CaptureSource
{
    public CameraSource()
    {
        Type = SourceType.Camera;
    }

    /// <summary>
    /// Stable Windows device interface identifier (from DeviceInformation.Id).
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Whether this camera device is an integrated built-in laptop camera.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// Camera capture resolution width in pixels, if known.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Camera capture resolution height in pixels, if known.
    /// </summary>
    public int Height { get; init; }
}
