using SwitchCast.Models.Performance;
using Windows.Graphics.DirectX.Direct3D11;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Service providing Direct3D 11 graphics devices for capture and presentation rendering pipelines.
/// Supports adaptive hardware, software (WARP), and compatibility rasterization modes.
/// </summary>
public interface IDirect3D11DeviceProvider : IDisposable
{
    /// <summary>
    /// Gets the WinRT Direct3D 11 device representation.
    /// </summary>
    IDirect3DDevice Device { get; }

    /// <summary>
    /// Gets the native Direct3D 11 device pointer.
    /// </summary>
    nint NativeDevicePointer { get; }

    /// <summary>
    /// Gets the currently active rendering mode.
    /// </summary>
    RenderingMode ActiveMode { get; }

    /// <summary>
    /// Gets whether the active device is hardware-accelerated.
    /// </summary>
    bool IsHardwareAccelerated { get; }

    /// <summary>
    /// Gets a human-readable description of the active rendering backend.
    /// </summary>
    string ActiveBackendDescription { get; }

    /// <summary>
    /// Updates the target rendering mode and reinitializes device state if changed.
    /// </summary>
    void SetRenderingMode(RenderingMode mode);

    /// <summary>
    /// Ensures the graphics device is active and recreates it if lost.
    /// </summary>
    void EnsureDevice();

    /// <summary>
    /// Forces the release and recreation of the Direct3D 11 device (e.g. after DXGI device removal or reset).
    /// </summary>
    void ResetDevice();
}
