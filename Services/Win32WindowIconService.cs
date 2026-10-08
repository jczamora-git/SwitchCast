using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SwitchCast.Models;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services;

/// <summary>
/// High-performance Win32 and Shell icon extraction service with caching and safe native handle lifecycle management.
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
    private const uint SHGFI_SMALLICON = 0x000000001;

    private const uint DI_NORMAL = 0x0003;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();
    private bool _isDisposed;

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

        try
        {
            // Extract icon pixels on background thread to avoid blocking UI
            var pixelData = await Task.Run(() => ExtractIconPixelBytes(windowSource), cancellationToken).ConfigureAwait(false);

            if (pixelData is null || pixelData.Length == 0)
            {
                return null;
            }

            // Create SoftwareBitmap and SoftwareBitmapSource
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                ICON_WIDTH,
                ICON_HEIGHT,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());

            var sourceImage = new SoftwareBitmapSource();
            await sourceImage.SetBitmapAsync(softwareBitmap);

            _iconCache[cacheKey] = sourceImage;
            return sourceImage;
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
            // 1. Try WM_GETICON (ICON_SMALL2 -> ICON_SMALL -> ICON_BIG) with 200ms timeout
            hIcon = GetWindowIcon(hWnd);

            // 2. Fallback: Window class icon via GetClassLongPtr
            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetClassIcon(hWnd);
            }

            // 3. Fallback: Shell icon from process executable path
            if (hIcon == IntPtr.Zero && !string.IsNullOrWhiteSpace(windowSource.ProcessPath))
            {
                hIcon = GetShellProcessIcon(windowSource.ProcessPath);
                if (hIcon != IntPtr.Zero)
                {
                    isOwnedHandle = true; // Shell icons must be released with DestroyIcon
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
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_SMALL2, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 200, out var result) != IntPtr.Zero && result != IntPtr.Zero)
        {
            return result;
        }

        // Try ICON_SMALL
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_SMALL, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 200, out result) != IntPtr.Zero && result != IntPtr.Zero)
        {
            return result;
        }

        // Try ICON_BIG
        if (SendMessageTimeout(hWnd, WM_GETICON, ICON_BIG, IntPtr.Zero, SMTO_ABORTIFHUNG | SMTO_BLOCK, 200, out result) != IntPtr.Zero && result != IntPtr.Zero)
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

    private static IntPtr GetShellProcessIcon(string processPath)
    {
        try
        {
            if (!File.Exists(processPath))
            {
                return IntPtr.Zero;
            }

            var shinfo = new SHFILEINFO();
            var res = SHGetFileInfo(
                processPath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_SMALLICON);

            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                return shinfo.hIcon;
            }
        }
        catch
        {
            // Ignore shell retrieval errors
        }

        return IntPtr.Zero;
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
            // Draw icon onto memory DC with alpha channel support
            bool drawn = DrawIconEx(hdcMem, 0, 0, hIcon, width, height, 0, IntPtr.Zero, DI_NORMAL);
            if (!drawn)
            {
                return null;
            }

            int byteLength = width * height * 4;
            byte[] pixelBytes = new byte[byteLength];
            Marshal.Copy(ppvBits, pixelBytes, 0, byteLength);

            // Verify alpha channel for legacy 1-bit or 24-bit icons
            bool hasAlpha = false;
            for (int i = 3; i < pixelBytes.Length; i += 4)
            {
                if (pixelBytes[i] > 0)
                {
                    hasAlpha = true;
                    break;
                }
            }

            if (!hasAlpha)
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
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

    #endregion
}
