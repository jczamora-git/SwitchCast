using SwitchCast.Models;

namespace SwitchCast.Services.Camera;

/// <summary>
/// Service managing discovery and enumeration of connected physical and virtual camera video capture devices.
/// </summary>
public interface ICameraDiscoveryService
{
    /// <summary>
    /// Raised when camera hardware devices are plugged in, unplugged, or changed.
    /// </summary>
    event EventHandler? CamerasChanged;

    /// <summary>
    /// Enumerates all connected video capture camera devices.
    /// </summary>
    Task<IReadOnlyList<CameraSource>> EnumerateCamerasAsync(CancellationToken cancellationToken = default);
}
