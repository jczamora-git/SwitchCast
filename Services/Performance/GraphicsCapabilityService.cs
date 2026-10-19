using System.Diagnostics;
using System.Runtime.InteropServices;
using SwitchCast.Models.Performance;
using SwitchCast.Services.Capture.Interop;
using SwitchCast.Services.Diagnostics;
using Windows.Graphics.Capture;

namespace SwitchCast.Services.Performance;

/// <summary>
/// Authoritative implementation of graphics capability and GPU adapter probing.
/// Queries DXGI 1.1 adapters, Direct3D 11 feature levels, and Windows.Graphics.Capture support with caching.
/// </summary>
public sealed class GraphicsCapabilityService : IGraphicsCapabilityService
{
    private readonly object _syncLock = new();
    private GraphicsCapabilities? _cachedCapabilities;

    public bool CanSupportHardwareCapture => GetCapabilities().IsD3D11HardwareSupported && GetCapabilities().IsWindowsGraphicsCaptureSupported;

    public bool CanSupportWarpFallback => GetCapabilities().IsWarpSupported;

    public GraphicsCapabilities GetCapabilities(bool forceRefresh = false)
    {
        lock (_syncLock)
        {
            if (_cachedCapabilities is not null && !forceRefresh)
            {
                return _cachedCapabilities;
            }

            _cachedCapabilities = ProbeCapabilities();
            return _cachedCapabilities;
        }
    }

    public Task<GraphicsCapabilities> GetCapabilitiesAsync(bool forceRefresh = false)
    {
        return Task.Run(() => GetCapabilities(forceRefresh));
    }

    public void RefreshCapabilities()
    {
        lock (_syncLock)
        {
            _cachedCapabilities = ProbeCapabilities();
        }
    }

    private static GraphicsCapabilities ProbeCapabilities()
    {
        var sw = Stopwatch.StartNew();
        var adapters = new List<GraphicsAdapterInfo>();
        bool d3d11Hardware = false;
        bool warpSupported = false;
        bool wgcSupported = false;
        string featureLevelStr = "Unknown";
        int featureLevelVal = 0;
        GraphicsAdapterInfo? primaryAdapter = null;

        // 1. Enumerate DXGI Adapters
        try
        {
            var factoryGuid = NativeCaptureMethods.IID_IDXGIFactory1;
            int hr = NativeCaptureMethods.CreateDXGIFactory1(ref factoryGuid, out var factory);
            if (hr == 0 && factory is not null)
            {
                uint index = 0;
                while (true)
                {
                    hr = factory.EnumAdapters1(index, out var adapter);
                    if (hr != 0 || adapter is null)
                    {
                        break;
                    }

                    try
                    {
                        hr = adapter.GetDesc1(out var desc);
                        if (hr == 0)
                        {
                            bool isSoftware = (desc.Flags & NativeCaptureMethods.DXGI_ADAPTER_FLAG_SOFTWARE) != 0;
                            var adapterInfo = new GraphicsAdapterInfo
                            {
                                Name = string.IsNullOrWhiteSpace(desc.Description) ? $"Adapter {index}" : desc.Description.Trim(),
                                VendorId = desc.VendorId,
                                DeviceId = desc.DeviceId,
                                DedicatedVideoMemoryBytes = desc.DedicatedVideoMemory.ToUInt64(),
                                DedicatedSystemMemoryBytes = desc.DedicatedSystemMemory.ToUInt64(),
                                SharedSystemMemoryBytes = desc.SharedSystemMemory.ToUInt64(),
                                IsSoftware = isSoftware
                            };

                            adapters.Add(adapterInfo);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(adapter);
                    }

                    index++;
                }

                Marshal.ReleaseComObject(factory);
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogWarning(nameof(GraphicsCapabilityService), $"DXGI adapter enumeration encountered exception: {ex.Message}");
        }

        // 2. Probe Direct3D 11 Hardware device creation and feature level
        try
        {
            int hr = NativeCaptureMethods.D3D11CreateDevice(
                IntPtr.Zero,
                NativeCaptureMethods.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                NativeCaptureMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                null,
                0,
                NativeCaptureMethods.D3D11_SDK_VERSION,
                out var d3d11Device,
                out featureLevelVal,
                out var context);

            if (hr == 0 && d3d11Device != IntPtr.Zero)
            {
                d3d11Hardware = true;
                featureLevelStr = FormatFeatureLevel(featureLevelVal);

                if (context != IntPtr.Zero)
                {
                    Marshal.Release(context);
                }
                Marshal.Release(d3d11Device);
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogWarning(nameof(GraphicsCapabilityService), $"D3D11 hardware probe failed: {ex.Message}");
        }

        // 3. Probe WARP software rasterizer device creation
        try
        {
            int hr = NativeCaptureMethods.D3D11CreateDevice(
                IntPtr.Zero,
                NativeCaptureMethods.D3D_DRIVER_TYPE_WARP,
                IntPtr.Zero,
                NativeCaptureMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                null,
                0,
                NativeCaptureMethods.D3D11_SDK_VERSION,
                out var warpDevice,
                out int warpLevel,
                out var warpContext);

            if (hr == 0 && warpDevice != IntPtr.Zero)
            {
                warpSupported = true;

                if (!d3d11Hardware)
                {
                    featureLevelVal = warpLevel;
                    featureLevelStr = $"{FormatFeatureLevel(warpLevel)} (WARP)";
                }

                if (warpContext != IntPtr.Zero)
                {
                    Marshal.Release(warpContext);
                }
                Marshal.Release(warpDevice);
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogWarning(nameof(GraphicsCapabilityService), $"WARP probe failed: {ex.Message}");
        }

        // 4. Probe Windows.Graphics.Capture platform compatibility
        try
        {
            wgcSupported = GraphicsCaptureSession.IsSupported();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogWarning(nameof(GraphicsCapabilityService), $"WGC support check failed: {ex.Message}");
            wgcSupported = false;
        }

        // Determine primary adapter (prefer first non-software adapter, otherwise first available)
        primaryAdapter = adapters.FirstOrDefault(a => !a.IsSoftware) ?? adapters.FirstOrDefault();

        sw.Stop();
        SwitchCastDiagnostics.LogInfo(
            nameof(GraphicsCapabilityService),
            $"Probed Graphics Capabilities in {sw.ElapsedMilliseconds}ms | Hardware={d3d11Hardware} | WARP={warpSupported} | WGC={wgcSupported} | Primary={primaryAdapter?.Name} | FeatureLevel={featureLevelStr}");

        return new GraphicsCapabilities
        {
            IsD3D11HardwareSupported = d3d11Hardware,
            IsWarpSupported = warpSupported,
            IsWindowsGraphicsCaptureSupported = wgcSupported,
            FeatureLevel = featureLevelStr,
            FeatureLevelValue = featureLevelVal,
            PrimaryAdapter = primaryAdapter,
            AvailableAdapters = adapters
        };
    }

    private static string FormatFeatureLevel(int level) => level switch
    {
        0xb100 => "Direct3D 11.1",
        0xb000 => "Direct3D 11.0",
        0xa100 => "Direct3D 10.1",
        0xa000 => "Direct3D 10.0",
        0x9300 => "Direct3D 9.3",
        0x9100 => "Direct3D 9.1",
        _ => $"Direct3D 0x{level:X4}"
    };
}
