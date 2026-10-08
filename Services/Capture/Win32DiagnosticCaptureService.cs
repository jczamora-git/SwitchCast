using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using SwitchCast.Services.Capture.Interop;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Implements a fail-safe Win32 GDI capture pathway for diagnostic frame acquisition and verification.
/// </summary>
public class Win32DiagnosticCaptureService : IWin32DiagnosticCaptureService
{
    public Task<SoftwareBitmap?> CaptureWindowDiagnosticAsync(nint hWnd, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (hWnd == nint.Zero || !NativeCaptureMethods.IsWindow(hWnd))
        {
            return Task.FromResult<SoftwareBitmap?>(null);
        }

        if (NativeCaptureMethods.IsIconic(hWnd))
        {
            // Minimized windows cannot produce content via standard GDI PrintWindow
            return Task.FromResult<SoftwareBitmap?>(null);
        }

        if (!NativeCaptureMethods.GetWindowRect(hWnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return Task.FromResult<SoftwareBitmap?>(null);
        }

        int width = rect.Width;
        int height = rect.Height;

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hdcWindow = NativeCaptureMethods.GetWindowDC(hWnd);
            if (hdcWindow == nint.Zero)
            {
                return null;
            }

            var hdcMem = NativeCaptureMethods.CreateCompatibleDC(hdcWindow);
            if (hdcMem == nint.Zero)
            {
                NativeCaptureMethods.ReleaseDC(hWnd, hdcWindow);
                return null;
            }

            var bmi = new NativeCaptureMethods.BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<NativeCaptureMethods.BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (uint)NativeCaptureMethods.BI_RGB;

            var hDib = NativeCaptureMethods.CreateDIBSection(
                hdcWindow,
                ref bmi,
                NativeCaptureMethods.DIB_RGB_COLORS,
                out var ppvBits,
                nint.Zero,
                0);

            if (hDib == nint.Zero || ppvBits == nint.Zero)
            {
                NativeCaptureMethods.DeleteDC(hdcMem);
                NativeCaptureMethods.ReleaseDC(hWnd, hdcWindow);
                return null;
            }

            var hOldBitmap = NativeCaptureMethods.SelectObject(hdcMem, hDib);
            SoftwareBitmap? resultBitmap = null;

            try
            {
                // Attempt 1: PrintWindow with PW_RENDERFULLCONTENT (supports hardware acceleration in Win 8.1+)
                bool success = NativeCaptureMethods.PrintWindow(hWnd, hdcMem, NativeCaptureMethods.PW_RENDERFULLCONTENT);

                // Attempt 2: Standard PrintWindow
                if (!success)
                {
                    success = NativeCaptureMethods.PrintWindow(hWnd, hdcMem, 0);
                }

                // Attempt 3: BitBlt fallback directly from Window DC
                if (!success)
                {
                    success = NativeCaptureMethods.BitBlt(
                        hdcMem,
                        0,
                        0,
                        width,
                        height,
                        hdcWindow,
                        0,
                        0,
                        NativeCaptureMethods.SRCCOPY);
                }

                if (success)
                {
                    int byteLength = width * height * 4;
                    byte[] pixelBytes = new byte[byteLength];
                    Marshal.Copy(ppvBits, pixelBytes, 0, byteLength);

                    var softwareBitmap = new SoftwareBitmap(
                        BitmapPixelFormat.Bgra8,
                        width,
                        height,
                        BitmapAlphaMode.Premultiplied);

                    softwareBitmap.CopyFromBuffer(pixelBytes.AsBuffer());
                    resultBitmap = softwareBitmap;
                }
            }
            finally
            {
                if (hOldBitmap != nint.Zero)
                {
                    NativeCaptureMethods.SelectObject(hdcMem, hOldBitmap);
                }

                NativeCaptureMethods.DeleteObject(hDib);
                NativeCaptureMethods.DeleteDC(hdcMem);
                NativeCaptureMethods.ReleaseDC(hWnd, hdcWindow);
            }

            return resultBitmap;
        }, cancellationToken);
    }
}
