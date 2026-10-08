using Windows.Graphics.DirectX.Direct3D11;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Service providing hardware-accelerated Direct3D 11 graphics devices for capture pipelines.
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
    /// Ensures the graphics device is active and recreates it if lost.
    /// </summary>
    void EnsureDevice();

    /// <summary>
    /// Forces the release and recreation of the Direct3D 11 device (e.g. after DXGI device removal or reset).
    /// </summary>
    void ResetDevice();
}
