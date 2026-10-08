using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// GPU-backed preview renderer converting Direct3D 11 capture surfaces to WinUI 3 SoftwareBitmapSource.
/// </summary>
public sealed class Direct3D11PreviewRenderer : ICapturePreviewRenderer
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SoftwareBitmapSource _softwareBitmapSource;
    private int _isProcessingFrame;
    private bool _isDisposed;

    public Direct3D11PreviewRenderer()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ??
                           throw new InvalidOperationException("Direct3D11PreviewRenderer must be created on a thread with an active DispatcherQueue.");
        _softwareBitmapSource = new SoftwareBitmapSource();
    }

    public ImageSource PreviewImageSource => _softwareBitmapSource;

    public async Task RenderFrameAsync(Direct3D11CaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_isDisposed)
        {
            return;
        }

        // Frame dropping / pacing: If the previous frame is still being copied or rendered, skip to stay in real-time
        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var surface = frame.Surface;
            if (surface is null)
            {
                return;
            }

            // Copy GPU Direct3D surface to SoftwareBitmap
            using var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);

            if (_isDisposed || softwareBitmap is null)
            {
                return;
            }

            // Ensure pixel format is BGRA8 with Premultiplied alpha for WinUI rendering
            SoftwareBitmap displayBitmap;
            var needsConversion = softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                                  softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight;

            if (needsConversion)
            {
                displayBitmap = SoftwareBitmap.Convert(
                    softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
            }
            else
            {
                displayBitmap = SoftwareBitmap.Copy(softwareBitmap);
            }

            // Dispatch frame presentation to UI thread
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isDisposed)
                    {
                        await _softwareBitmapSource.SetBitmapAsync(displayBitmap);
                    }
                }
                catch
                {
                    // Ignore transient render errors during window resize or shutdown
                }
                finally
                {
                    displayBitmap.Dispose();
                }
            });
        }
        catch
        {
            // Ignore capture frame acquisition errors during source closure
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessingFrame, 0);
        }
    }

    public void Clear()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            // Create a small empty 1x1 transparent bitmap to reset the image cleanly
            try
            {
                using var emptyBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 1, 1, BitmapAlphaMode.Premultiplied);
                _ = _softwareBitmapSource.SetBitmapAsync(emptyBitmap);
            }
            catch
            {
                // Ignored during cleanup
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Clear();
        _softwareBitmapSource.Dispose();
    }
}
