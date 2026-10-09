using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SwitchCast.Services.Media;

/// <summary>
/// Native WinUI 3 desktop file picker implementation for media files with HWND interop.
/// </summary>
public class Win32MediaPickerService : IMediaPickerService
{
    private readonly Func<IntPtr>? _windowHandleProvider;

    public Win32MediaPickerService(Func<IntPtr>? windowHandleProvider = null)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<IReadOnlyList<string>> PickMediaFilesAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };

            // Initialize with active window handle in desktop environment
            IntPtr hwnd = _windowHandleProvider?.Invoke() ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }

            if (hwnd != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            // Image file filters
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");

            // Video file filters
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".m4v");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mkv");

            var files = await picker.PickMultipleFilesAsync();
            if (files is not null && files.Count > 0)
            {
                return files.Select(f => f.Path).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Win32MediaPickerService] PickMediaFilesAsync failed: {ex.Message}");
        }

        return [];
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
}
