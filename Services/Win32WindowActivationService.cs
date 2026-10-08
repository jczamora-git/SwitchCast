using System.Diagnostics;
using System.Runtime.InteropServices;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Native Win32 implementation of application window activation and foreground management.
/// Respects Windows foreground lock timeouts, handles minimized window restoration, and provides safety bounds.
/// </summary>
public sealed class Win32WindowActivationService : IWindowActivationService
{
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    public bool ActivateSource(CaptureSource? source)
    {
        if (source is not WindowSource windowSource || windowSource.WindowHandle == IntPtr.Zero)
        {
            // Monitor sources and invalid handles cannot be activated as windows
            return false;
        }

        return ActivateWindow(windowSource.WindowHandle);
    }

    public bool ActivateWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return false;
        }

        try
        {
            // If the window is currently minimized, restore it first
            if (IsIconic(hWnd))
            {
                ShowWindowAsync(hWnd, SW_RESTORE);
            }
            else
            {
                ShowWindowAsync(hWnd, SW_SHOW);
            }

            // Request foreground window focus
            bool setResult = SetForegroundWindow(hWnd);

            // Cross-check resulting foreground window
            IntPtr currentForeground = GetForegroundWindow();
            bool isForeground = (currentForeground == hWnd);

            Debug.WriteLine($"[WindowActivationService] ActivateWindow hWnd=0x{hWnd:X} setResult={setResult} isForeground={isForeground}");
            return isForeground || setResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowActivationService] ActivateWindow failed hWnd=0x{hWnd:X} error={ex.Message}");
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
