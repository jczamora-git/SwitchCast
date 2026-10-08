using System.Runtime.InteropServices;
using SwitchCast.Services.Capture.Interop;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Manages native Direct3D 11 device creation and WinRT IDirect3DDevice projection for capture pipelines.
/// </summary>
public sealed class Direct3D11DeviceProvider : IDirect3D11DeviceProvider
{
    private readonly object _lock = new();
    private IntPtr _d3d11DevicePtr = IntPtr.Zero;
    private IntPtr _immediateContextPtr = IntPtr.Zero;
    private IDirect3DDevice? _winrtDevice;
    private bool _isDisposed;

    public IDirect3DDevice Device
    {
        get
        {
            EnsureDevice();
            return _winrtDevice ?? throw new InvalidOperationException("Direct3D 11 device failed to initialize.");
        }
    }

    public nint NativeDevicePointer
    {
        get
        {
            EnsureDevice();
            return _d3d11DevicePtr;
        }
    }

    public void EnsureDevice()
    {
        lock (_lock)
        {
            if (_winrtDevice is not null && _d3d11DevicePtr != IntPtr.Zero)
            {
                return;
            }

            ReleaseDeviceUnsafe();

            // Try hardware acceleration first
            var hr = NativeCaptureMethods.D3D11CreateDevice(
                IntPtr.Zero,
                NativeCaptureMethods.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                NativeCaptureMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                null,
                0,
                NativeCaptureMethods.D3D11_SDK_VERSION,
                out _d3d11DevicePtr,
                out _,
                out _immediateContextPtr);

            if (hr != 0)
            {
                // Fallback to WARP software rasterizer
                hr = NativeCaptureMethods.D3D11CreateDevice(
                    IntPtr.Zero,
                    NativeCaptureMethods.D3D_DRIVER_TYPE_WARP,
                    IntPtr.Zero,
                    NativeCaptureMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    null,
                    0,
                    NativeCaptureMethods.D3D11_SDK_VERSION,
                    out _d3d11DevicePtr,
                    out _,
                    out _immediateContextPtr);

                if (hr != 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }
            }

            var dxgiGuid = NativeCaptureMethods.IID_IDXGIDevice;
            hr = Marshal.QueryInterface(_d3d11DevicePtr, ref dxgiGuid, out var dxgiDevicePtr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                hr = NativeCaptureMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out var graphicsDevicePtr);
                if (hr != 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                try
                {
                    _winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevicePtr);
                }
                finally
                {
                    Marshal.Release(graphicsDevicePtr);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevicePtr);
            }
        }
    }

    private void ReleaseDeviceUnsafe()
    {
        _winrtDevice?.Dispose();
        _winrtDevice = null;

        if (_immediateContextPtr != IntPtr.Zero)
        {
            Marshal.Release(_immediateContextPtr);
            _immediateContextPtr = IntPtr.Zero;
        }

        if (_d3d11DevicePtr != IntPtr.Zero)
        {
            Marshal.Release(_d3d11DevicePtr);
            _d3d11DevicePtr = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            ReleaseDeviceUnsafe();
        }
    }
}
