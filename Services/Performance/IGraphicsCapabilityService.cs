using SwitchCast.Models.Performance;

namespace SwitchCast.Services.Performance;

/// <summary>
/// Probes and reports hardware DirectX capabilities, adapter topology, and capture compatibility.
/// </summary>
public interface IGraphicsCapabilityService
{
    /// <summary>
    /// Gets the cached or evaluated graphics capabilities.
    /// </summary>
    GraphicsCapabilities GetCapabilities(bool forceRefresh = false);

    /// <summary>
    /// Asynchronously retrieves graphics capabilities.
    /// </summary>
    Task<GraphicsCapabilities> GetCapabilitiesAsync(bool forceRefresh = false);

    /// <summary>
    /// Gets a value indicating whether Direct3D 11 hardware-accelerated capture is supported.
    /// </summary>
    bool CanSupportHardwareCapture { get; }

    /// <summary>
    /// Gets a value indicating whether WARP software rasterization fallback is available.
    /// </summary>
    bool CanSupportWarpFallback { get; }

    /// <summary>
    /// Refreshes cached capabilities when display state or GPU device changes.
    /// </summary>
    void RefreshCapabilities();
}
