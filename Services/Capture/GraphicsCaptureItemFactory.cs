using System.Runtime.InteropServices;
using SwitchCast.Models;
using SwitchCast.Services.Capture.Interop;
using Windows.Graphics.Capture;
using WinRT;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Default implementation of IGraphicsCaptureItemFactory using native COM interop.
/// </summary>
public sealed class GraphicsCaptureItemFactory : IGraphicsCaptureItemFactory
{
    private const string CaptureItemClassId = "Windows.Graphics.Capture.GraphicsCaptureItem";

    public bool IsCaptureSupported()
    {
        return GraphicsCaptureSession.IsSupported();
    }

    public GraphicsCaptureItem CreateItemForSource(CaptureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsCaptureSupported())
        {
            throw new PlatformNotSupportedException("Windows Graphics Capture is not supported on this operating system.");
        }

        return source switch
        {
            WindowSource windowSource => CreateForWindow(windowSource),
            MonitorSource monitorSource => CreateForMonitor(monitorSource),
            _ => throw new NotSupportedException($"Source type '{source.GetType().Name}' is not supported for capture.")
        };
    }

    private static GraphicsCaptureItem CreateForWindow(WindowSource windowSource)
    {
        var hwnd = (IntPtr)windowSource.WindowHandle;

        if (hwnd == IntPtr.Zero || !NativeCaptureMethods.IsWindow(hwnd))
        {
            throw new InvalidOperationException($"Target window handle (0x{windowSource.WindowHandle:X}) is no longer valid or has been closed.");
        }

        // Re-validate owning process to guard against HWND reuse
        if (windowSource.ProcessId != 0)
        {
            NativeCaptureMethods.GetWindowThreadProcessId(hwnd, out var currentPid);
            if (currentPid != windowSource.ProcessId)
            {
                throw new InvalidOperationException($"Window handle was reassigned to another process (PID: {currentPid}). Capture aborted for security.");
            }
        }

        var interopGuid = NativeCaptureMethods.IID_IGraphicsCaptureItemInterop;
        var hr = NativeCaptureMethods.RoGetActivationFactory(CaptureItemClassId, ref interopGuid, out var factoryPtr);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            var inspectableGuid = NativeCaptureMethods.IID_IInspectable;

            hr = interop.CreateForWindow(hwnd, ref inspectableGuid, out var rawItemPtr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawItemPtr);
            }
            finally
            {
                Marshal.Release(rawItemPtr);
            }
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }
    }

    private static GraphicsCaptureItem CreateForMonitor(MonitorSource monitorSource)
    {
        var hmon = (IntPtr)monitorSource.MonitorHandle;

        if (hmon == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Monitor handle for display '{monitorSource.DeviceName}' is invalid or disconnected.");
        }

        var interopGuid = NativeCaptureMethods.IID_IGraphicsCaptureItemInterop;
        var hr = NativeCaptureMethods.RoGetActivationFactory(CaptureItemClassId, ref interopGuid, out var factoryPtr);
        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
            var inspectableGuid = NativeCaptureMethods.IID_IInspectable;

            hr = interop.CreateForMonitor(hmon, ref inspectableGuid, out var rawItemPtr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawItemPtr);
            }
            finally
            {
                Marshal.Release(rawItemPtr);
            }
        }
        finally
        {
            Marshal.Release(factoryPtr);
        }
    }
}
