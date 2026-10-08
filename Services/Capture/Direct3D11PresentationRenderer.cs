using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// GPU-backed presentation renderer converting Direct3D 11 capture surfaces to WinUI 3 SoftwareBitmapSource for the Presentation Output Window.
/// </summary>
public sealed class Direct3D11PresentationRenderer : IPresentationOutputRenderer
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SoftwareBitmapSource _softwareBitmapSource;
    private int _isProcessingFrame;
    private bool _isFrozen;
    private bool _isDisposed;

    public Direct3D11PresentationRenderer()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ??
                           throw new InvalidOperationException("Direct3D11PresentationRenderer must be created on a thread with an active DispatcherQueue.");
        _softwareBitmapSource = new SoftwareBitmapSource();
    }

    public ImageSource? PresentationImageSource => _softwareBitmapSource;

    public bool IsFrozen => _isFrozen;

    public async Task RenderFrameAsync(Direct3D11CaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_isDisposed || _isFrozen)
        {
            return;
        }

        // Frame dropping / pacing: Skip if the previous frame is still being copied/rendered to preserve real-time playback
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

            if (_isDisposed || _isFrozen || softwareBitmap is null)
            {
                return;
            }

            // Ensure pixel format is BGRA8 with Premultiplied alpha for WinUI rendering
            SoftwareBitmap displayBitmap;
            var needsConversion = softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                                  softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied;

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
                    if (!_isDisposed && !_isFrozen)
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

    public void Freeze()
    {
        _isFrozen = true;
    }

    public void Resume()
    {
        _isFrozen = false;
    }

    public void Clear()
    {
        _isFrozen = false;
        _dispatcherQueue.TryEnqueue(() =>
        {
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
        _isFrozen = false;
        Clear();
        _softwareBitmapSource.Dispose();
    }
}
