using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// GPU-backed preview renderer presenting capture frames onto WinUI 3 SoftwareBitmapSource.
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

    public async Task RenderBitmapAsync(SoftwareBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (_isDisposed)
        {
            return;
        }

        // Frame dropping / pacing: If previous frame is still being presented, skip to keep real-time performance
        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var tcs = new TaskCompletionSource();
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isDisposed)
                    {
                        await _softwareBitmapSource.SetBitmapAsync(bitmap);
                    }
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            // Ignore transient render errors during window resize or shutdown
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessingFrame, 0);
        }
    }

    public async Task RenderFrameAsync(Direct3D11CaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_isDisposed)
        {
            return;
        }

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

            using var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);
            if (_isDisposed || softwareBitmap is null)
            {
                return;
            }

            SoftwareBitmap displayBitmap;
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
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

            var tcs = new TaskCompletionSource();
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isDisposed)
                    {
                        await _softwareBitmapSource.SetBitmapAsync(displayBitmap);
                    }
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    displayBitmap.Dispose();
                }
            });

            await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            // Ignore transient render errors during window resize or shutdown
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
