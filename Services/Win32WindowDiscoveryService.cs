using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Native Win32 window discovery service that safely enumerates and filters top-level application windows.
/// </summary>
public class Win32WindowDiscoveryService : IWindowDiscoveryService
{
    private const int DWMWA_CLOAKED = 14;
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_VISIBLE = 0x10000000L;

    // Filter out common desktop shell window class names
    private static readonly HashSet<string> ExcludedClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
        "ApplicationFrameWindow",
        "EdgeUiInputTopWndClass",
        "SideBar_HTMLHostWindow",
        "SnappedDesktop"
    };

    public Task<IReadOnlyList<WindowSource>> EnumerateWindowsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<WindowSource>>(() =>
        {
            var windows = new List<WindowSource>();
            var currentProcessId = (uint)Environment.ProcessId;

            EnumWindows((hwnd, _) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                if (!IsWindowEligible(hwnd, currentProcessId, out var windowSource))
                {
                    return true;
                }

                if (windowSource is not null)
                {
                    windows.Add(windowSource);
                }

                return true;
            }, nint.Zero);

            return windows.OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
        }, cancellationToken);
    }

    private static bool IsWindowEligible(nint hwnd, uint currentProcessId, out WindowSource? source)
    {
        source = null;

        // 1. Must be a valid and visible window
        if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
        {
            return false;
        }

        // 2. Filter out windows without meaningful titles
        var titleLength = GetWindowTextLengthW(hwnd);
        if (titleLength == 0)
        {
            return false;
        }

        var sbTitle = new StringBuilder(titleLength + 1);
        if (GetWindowTextW(hwnd, sbTitle, sbTitle.Capacity) == 0)
        {
            return false;
        }

        var title = sbTitle.ToString().Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        // 3. Exclude SwitchCast itself and extract Process ID
        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0 || processId == currentProcessId)
        {
            return false;
        }

        // 4. Check DWM Cloaked state (virtual desktop switch or background UWP suspension)
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        // 5. Check Extended Style for Tool Windows
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        // 6. Check Class Name for known Shell surfaces
        var sbClass = new StringBuilder(256);
        if (GetClassNameW(hwnd, sbClass, sbClass.Capacity) > 0)
        {
            var className = sbClass.ToString();
            if (ExcludedClassNames.Contains(className))
            {
                // CoreWindow/ApplicationFrameWindow are allowed only if they have substantial user titles
                if (className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) &&
                    (title.Equals("Search", StringComparison.OrdinalIgnoreCase) ||
                     title.Equals("Start", StringComparison.OrdinalIgnoreCase) ||
                     title.Equals("Cortana", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
        }

        // 7. Check Window Bounds (must have non-zero dimensions)
        if (!GetWindowRect(hwnd, out var rect) || (rect.Right - rect.Left <= 0) || (rect.Bottom - rect.Top <= 0))
        {
            return false;
        }

        // 8. Extract Process metadata safely
        var processName = "Unknown";
        string? processPath = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;
            try
            {
                processPath = process.MainModule?.FileName;
            }
            catch
            {
                // Process path might be restricted for elevated system processes
            }
        }
        catch
        {
            // Process might have terminated or access denied
        }

        var isMinimized = IsIconic(hwnd);

        source = new WindowSource
        {
            Id = $"win_{hwnd}_{processId}",
            Title = title,
            WindowHandle = hwnd,
            ProcessId = processId,
            ProcessName = processName,
            ProcessPath = processPath,
            IsAvailable = true
        };

        return true;
    }

    #region Win32 P/Invoke Declarations

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hwnd, [Out] StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLengthW(nint hwnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint hwnd, [Out] StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    #endregion
}
