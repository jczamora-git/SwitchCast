using System.Runtime.InteropServices;

namespace SwitchCast.Services.Capture.Interop;

/// <summary>
/// COM interop interface for creating Windows.Graphics.Capture.GraphicsCaptureItem from native HWND or HMONITOR.
/// </summary>
[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IGraphicsCaptureItemInterop
{
    [PreserveSig]
    int CreateForWindow(
        [In] IntPtr hWnd,
        [In] ref Guid riid,
        [Out] out IntPtr result);

    [PreserveSig]
    int CreateForMonitor(
        [In] IntPtr hMonitor,
        [In] ref Guid riid,
        [Out] out IntPtr result);
}
