namespace SwitchCast.Models.Performance;

/// <summary>
/// User-configurable rendering mode controlling Direct3D 11 hardware and compatibility fallbacks.
/// </summary>
public enum RenderingMode
{
    /// <summary>
    /// Automatically prefers the most efficient compatible rendering path based on detected GPU capabilities.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Enforces Direct3D 11 hardware acceleration across all capture and presentation pipelines.
    /// </summary>
    PreferHardware = 1,

    /// <summary>
    /// Forces tested software/GDI fallback paths for maximum compatibility with unstable drivers.
    /// </summary>
    CompatibilityMode = 2
}
