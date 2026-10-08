using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SwitchCast.Models;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services;

/// <summary>
/// High-performance Win32 and Shell icon extraction service with caching, UI thread dispatching, and safe native handle lifecycle management.
/// </summary>
public sealed class Win32WindowIconService : IWindowIconService
{
    private const int ICON_WIDTH = 32;
    private const int ICON_HEIGHT = 32;

    private const uint WM_GETICON = 0x007F;
    private static readonly IntPtr ICON_SMALL2 = new(2);
    private static readonly IntPtr ICON_SMALL = new(0);
    private static readonly IntPtr ICON_BIG = new(1);

    private const int GCLP_HICONSM = -34;
    private const int GCLP_HICON = -14;

    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private const uint SMTO_BLOCK = 0x0001;

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;

    private const uint DI_NORMAL = 0x0003;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _isDisposed;

    public Win32WindowIconService(DispatcherQueue? dispatcherQueue = null)
    {
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
    }

    public async Task<ImageSource?> GetIconForSourceAsync(CaptureSource source, CancellationToken cancellationToken = default)
    {
        if (source is not WindowSource windowSource || windowSource.WindowHandle == IntPtr.Zero)
        {
            return null;
        }

        string cacheKey = windowSource.Id;
        if (_iconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        string? exeCacheKey = !string.IsNullOrWhiteSpace(windowSource.ProcessPath)
            ? $"exe_{windowSource.ProcessPath.ToLowerInvariant()}"
            : null;

        if (exeCacheKey is not null && _iconCache.TryGetValue(exeCacheKey, out var exeCached))
        {
            _iconCache[cacheKey] = exeCached;
            return exeCached;
        }

        try
        {
            // 1. Extract icon pixels on background thread to avoid blocking UI
            var pixelData = await Task.Run(() => ExtractIconPixelBytes(windowSource), cancellationToken).ConfigureAwait(false);

            if (pixelData is null || pixelData.Length == 0)
            {
                return null;
            }

            // 2. Create SoftwareBitmap and SoftwareBitmapSource on the UI thread
            var dispatcher = _dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcher is not null && !dispatcher.HasThreadAccess)
            {
                var tcs = new TaskCompletionSource<ImageSource?>();
                bool enqueued = dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        var softwareBitmap = new SoftwareBitmap(
                            BitmapPixelFormat.Bgra8,
                            ICON_WIDTH,
                            ICON_HEIGHT,
                            BitmapAlphaMode.Premultiplied);

                        softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());

                        var sourceImage = new SoftwareBitmapSource();
                        await sourceImage.SetBitmapAsync(softwareBitmap);

                        _iconCache[cacheKey] = sourceImage;
                        if (exeCacheKey is not null)
                        {
                            _iconCache[exeCacheKey] = sourceImage;
                        }

                        tcs.TrySetResult(sourceImage);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WindowIconService] Error creating SoftwareBitmapSource on UI thread for {windowSource.Title}: {ex.Message}");
                        tcs.TrySetResult(null);
                    }
                });

                if (!enqueued)
                {
                    return null;
                }

                return await tcs.Task;
            }
            else
            {
                var softwareBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    ICON_WIDTH,
                    ICON_HEIGHT,
                    BitmapAlphaMode.Premultiplied);

                softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());

                var sourceImage = new SoftwareBitmapSource();
                await sourceImage.SetBitmapAsync(softwareBitmap);

                _iconCache[cacheKey] = sourceImage;
                if (exeCacheKey is not null)
                {
                    _iconCache[exeCacheKey] = sourceImage;
                }

                return sourceImage;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowIconService] Failed to resolve icon for {windowSource.Title}: {ex.Message}");
            return null;
        }
    }

    public void Invalidate(string sourceId)
    {
        if (!string.IsNullOrEmpty(sourceId))
        {
            _iconCache.TryRemove(sourceId, out _);
        }
    }

    public void ClearCache()
    {
        _iconCache.Clear();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _iconCache.Clear();
        }
    }

    private static byte[]? ExtractIconPixelBytes(WindowSource windowSource)
    {
        IntPtr hWnd = windowSource.WindowHandle;
        IntPtr hIcon = IntPtr.Zero;
        bool isOwnedHandle = false;

        try
        {
            // 1. Try WM_GETICON (ICON_SMALL2 -> ICON_SMALL -> ICON_BIG) with 100ms timeout
            hIcon = GetWindowIcon(hWnd);

            // 2. Fallback: Window class icon via GetClassLongPtr
            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetClassIcon(hWnd);
            }

            // 3. Fallback: Shell/Executable icon from process executable path
            if (hIcon == IntPtr.Zero)
            {
                string? exePath = windowSource.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath) && windowSource.ProcessId != 0)
                {
                    exePath = GetProcessPath(windowSource.ProcessId);
                }

                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    hIcon = GetExecutableIcon(exePath, out isOwnedHandle);
                }
            }

            if (hIcon == IntPtr.Zero)
            {
                return null;
            }

            // Convert HICON to 32x32 BGRA32 pixel buffer using GDI DIB section
            return RenderIconToBgra32(hIcon, ICON_WIDTH, ICON_HEIGHT);
        }
        finally
        {
            // Clean up owned handles only; never call DestroyIcon on borrowed window/class handles
            if (isOwnedHandle && hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }
        }
    }

    private static IntPtr GetWindowIcon(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return IntPtr.Zero;
        }

        // Try ICON_SMALL2
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_SMALL2, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out var result) != IntPtr.Zero && result != IntPtr.Zero)
        {
            return result;
        }

        // Try ICON_SMALL
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_SMALL, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out result) != IntPtr.Zero && result != IntPtr.Zero)
        {
            return result;
        }

        // Try ICON_BIG
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_BIG, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out result) != IntPtr.Zero && result != IntPtr.Zero)
        {
            return result;
        }

        return IntPtr.Zero;
    }

    private static IntPtr GetClassIcon(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return IntPtr.Zero;
        }

        var hIcon = GetClassLongPtr(hWnd, GCLP_HICONSM);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
        }
        return hIcon;
    }

    private static IntPtr GetExecutableIcon(string exePath, out bool isOwned)
    {
        isOwned = false;

        try
        {
            if (!File.Exists(exePath))
            {
                return IntPtr.Zero;
            }

            // Primary: ExtractIconEx for large (32x32) or small (16x16) icon
            int count = ExtractIconExW(exePath, 0, out IntPtr hLarge, out IntPtr hSmall, 1);
            if (count > 0)
            {
                if (hLarge != IntPtr.Zero)
                {
                    if (hSmall != IntPtr.Zero)
                    {
                        DestroyIcon(hSmall);
                    }
                    isOwned = true;
                    return hLarge;
                }
                if (hSmall != IntPtr.Zero)
                {
                    isOwned = true;
                    return hSmall;
                }
            }

            // Fallback: SHGetFileInfo with large icon
            var shinfo = new SHFILEINFO();
            var res = SHGetFileInfoW(
                exePath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_LARGEICON);

            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                isOwned = true;
                return shinfo.hIcon;
            }

            // Fallback: SHGetFileInfo with small icon
            res = SHGetFileInfoW(
                exePath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_SMALLICON);

            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                isOwned = true;
                return shinfo.hIcon;
            }
        }
        catch
        {
            // Ignore shell retrieval errors
        }

        return IntPtr.Zero;
    }

    private static string? GetProcessPath(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? RenderIconToBgra32(IntPtr hIcon, int width, int height)
    {
        var hdcScreen = GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            return null;
        }

        var hdcMem = CreateCompatibleDC(hdcScreen);
        if (hdcMem == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, hdcScreen);
            return null;
        }

        var bmi = new BITMAPINFO();
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // Top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = BI_RGB;

        var hDib = CreateDIBSection(hdcMem, ref bmi, DIB_RGB_COLORS, out var ppvBits, IntPtr.Zero, 0);
        if (hDib == IntPtr.Zero || ppvBits == IntPtr.Zero)
        {
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
            return null;
        }

        var hOldBitmap = SelectObject(hdcMem, hDib);

        try
        {
            // Check icon info for 32-bit color bitmap vs mask bitmap
            bool is32Bit = false;
            if (GetIconInfo(hIcon, out var iconInfo))
            {
                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out var bmp) > 0)
                    {
                        if (bmp.bmBitsPixel == 32)
                        {
                            is32Bit = true;
                        }
                    }
                    DeleteObject(iconInfo.hbmColor);
                }
                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmMask);
                }
            }

            // Draw icon onto memory DC with alpha channel support
            bool drawn = DrawIconEx(hdcMem, 0, 0, hIcon, width, height, 0, IntPtr.Zero, DI_NORMAL);
            if (!drawn)
            {
                return null;
            }

            int byteLength = width * height * 4;
            byte[] pixelBytes = new byte[byteLength];
            Marshal.Copy(ppvBits, pixelBytes, 0, byteLength);

            // Verify alpha channel
            bool hasAlpha = false;
            for (int i = 3; i < pixelBytes.Length; i += 4)
            {
                if (pixelBytes[i] > 0)
                {
                    hasAlpha = true;
                    break;
                }
            }

            if (!hasAlpha || !is32Bit)
            {
                // Fix opaque alpha for legacy icons
                for (int i = 0; i < pixelBytes.Length; i += 4)
                {
                    if (pixelBytes[i] > 0 || pixelBytes[i + 1] > 0 || pixelBytes[i + 2] > 0)
                    {
                        pixelBytes[i + 3] = 255;
                    }
                }
            }

            return pixelBytes;
        }
        finally
        {
            if (hOldBitmap != IntPtr.Zero)
            {
                SelectObject(hdcMem, hOldBitmap);
            }

            DeleteObject(hDib);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
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
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
    private static extern int GetClassLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetClassLongPtr64(hWnd, nIndex);
        return new IntPtr(GetClassLong32(hWnd, nIndex));
    }

    [DllImport("shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int ExtractIconExW(
        string lpszFile,
        int nIconIndex,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        int nIcons);

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(
        IntPtr hdc,
        int xLeft,
        int yTop,
        IntPtr hIcon,
        int cxWidth,
        int cyWidth,
        uint istepIfAniCur,
        IntPtr hbrFlickerFreeDraw,
        uint diFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(
        IntPtr hProcess,
        int dwFlags,
        [Out] StringBuilder lpExeName,
        ref int lpdwSize);

    #endregion
}
