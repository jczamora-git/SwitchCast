using System.Runtime.InteropServices;

namespace SwitchCast.Services.Capture.Interop;

/// <summary>
/// Native Win32, Direct3D 11, and WinRT activation P/Invoke interop declarations.
/// </summary>
public static class NativeCaptureMethods
{
    public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    public const uint D3D11_SDK_VERSION = 7;
    public const int D3D_DRIVER_TYPE_HARDWARE = 1;
    public const int D3D_DRIVER_TYPE_WARP = 2;

    public static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    public static readonly Guid IID_IInspectable = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");
    public static readonly Guid IID_IDXGIDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    public static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    public static readonly Guid IID_IDXGIAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");

    public const int DXGI_ERROR_NOT_FOUND = unchecked((int)0x887A0002);
    public const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;

    [DllImport("dxgi.dll", ExactSpelling = true)]
    public static extern int CreateDXGIFactory1(
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 ppFactory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory1
    {
        int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        int GetParent(ref Guid riid, out IntPtr ppParent);
        int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
        int MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
        int GetWindowAssociation(out IntPtr pWindowHandle);
        int CreateSwapChain(IntPtr pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
        int CreateSoftwareAdapter(IntPtr Module, out IntPtr ppAdapter);
        [PreserveSig]
        int EnumAdapters1(uint Adapter, [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1 ppAdapter);
        [PreserveSig]
        int IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIAdapter1
    {
        int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        int GetParent(ref Guid riid, out IntPtr ppParent);
        int GetDesc(IntPtr pDesc);
        int CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
        [PreserveSig]
        int GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    public static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int driverType,
        IntPtr software,
        uint flags,
        [In] int[]? pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    public static extern int CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern int RoGetActivationFactory(
        IntPtr activatableClassId,
        [In] ref Guid iid,
        [Out] out IntPtr factory);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public const int PW_CLIENTONLY = 0x00000001;
    public const int PW_RENDERFULLCONTENT = 0x00000002;
    public const int SRCCOPY = 0x00CC0020;
    public const uint DIB_RGB_COLORS = 0;
    public const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    public static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBmp, uint nFlags);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(
        IntPtr hdcDest,
        int nXDest,
        int nYDest,
        int nWidth,
        int nHeight,
        IntPtr hdcSrc,
        int nXSrc,
        int nYSrc,
        int dwRop);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
