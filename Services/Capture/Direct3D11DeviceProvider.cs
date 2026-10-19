using System.Runtime.InteropServices;
using SwitchCast.Models.Performance;
using SwitchCast.Services.Capture.Interop;
using SwitchCast.Services.Diagnostics;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Manages native Direct3D 11 device creation and WinRT IDirect3DDevice projection for capture pipelines.
/// Supports adaptive backend selection: Auto (Hardware with WARP fallback), PreferHardware, and CompatibilityMode (WARP).
/// </summary>
public sealed class Direct3D11DeviceProvider : IDirect3D11DeviceProvider
{
    private readonly object _lock = new();
    private readonly Performance.IPerformanceMetricsService? _metricsService;
    private readonly IApplicationSettingsService? _settingsService;
    private RenderingMode _activeMode = RenderingMode.Auto;
    private bool _isHardwareAccelerated;
    private string _activeBackendDescription = "Direct3D 11 (Uninitialized)";

    private IntPtr _d3d11DevicePtr = IntPtr.Zero;
    private IntPtr _immediateContextPtr = IntPtr.Zero;
    private IDirect3DDevice? _winrtDevice;
    private bool _isDisposed;

    public Direct3D11DeviceProvider(
        IApplicationSettingsService? settingsService = null,
        Performance.IPerformanceMetricsService? metricsService = null)
    {
        _settingsService = settingsService;
        _metricsService = metricsService;

        if (_settingsService is not null)
        {
            _activeMode = _settingsService.CurrentSettings.RenderingMode;
        }
    }

    public RenderingMode ActiveMode
    {
        get
        {
            lock (_lock) { return _activeMode; }
        }
    }

    public bool IsHardwareAccelerated
    {
        get
        {
            lock (_lock) { return _isHardwareAccelerated; }
        }
    }

    public string ActiveBackendDescription
    {
        get
        {
            lock (_lock) { return _activeBackendDescription; }
        }
    }

    public void SetRenderingMode(RenderingMode mode)
    {
        lock (_lock)
        {
            if (_activeMode == mode)
            {
                return;
            }

            _activeMode = mode;
            if (_winrtDevice is not null)
            {
                // Recreate device for the new rendering mode without counting as an unexpected device loss/reset event
                ReleaseDeviceUnsafe();
                EnsureDevice();
            }
        }
    }

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

            if (_activeMode == RenderingMode.CompatibilityMode)
            {
                // Compatibility mode: directly request WARP software rasterizer
                var hr = NativeCaptureMethods.D3D11CreateDevice(
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
                    _activeBackendDescription = "Direct3D 11 WARP (Failed)";
                    Marshal.ThrowExceptionForHR(hr);
                }

                _isHardwareAccelerated = false;
                _activeBackendDescription = "Direct3D 11 WARP (Compatibility Mode)";
            }
            else if (_activeMode == RenderingMode.PreferHardware)
            {
                // Prefer Hardware: enforce hardware D3D11 device creation
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
                    _activeBackendDescription = "Direct3D 11 Hardware (Unavailable)";
                    Marshal.ThrowExceptionForHR(hr);
                }

                _isHardwareAccelerated = true;
                _activeBackendDescription = "Direct3D 11 Hardware (Prefer Hardware)";
            }
            else // Auto (Default)
            {
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

                if (hr == 0)
                {
                    _isHardwareAccelerated = true;
                    _activeBackendDescription = "Direct3D 11 Hardware (Auto)";
                }
                else
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
                        _activeBackendDescription = "Direct3D 11 Auto (Failed)";
                        Marshal.ThrowExceptionForHR(hr);
                    }

                    _isHardwareAccelerated = false;
                    _activeBackendDescription = "Direct3D 11 WARP Fallback (Auto)";
                }
            }

            var dxgiGuid = NativeCaptureMethods.IID_IDXGIDevice;
            var qiHr = Marshal.QueryInterface(_d3d11DevicePtr, ref dxgiGuid, out var dxgiDevicePtr);
            if (qiHr != 0)
            {
                Marshal.ThrowExceptionForHR(qiHr);
            }

            try
            {
                var interopHr = NativeCaptureMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out var graphicsDevicePtr);
                if (interopHr != 0)
                {
                    Marshal.ThrowExceptionForHR(interopHr);
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

    public void ResetDevice()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _metricsService?.RecordDeviceReset();
            ReleaseDeviceUnsafe();
            EnsureDevice();
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
