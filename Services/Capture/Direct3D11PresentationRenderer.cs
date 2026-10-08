using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// GPU-backed presentation renderer presenting capture frames onto WinUI 3 SoftwareBitmapSource for the Presentation Output Window.
/// Implements high-priority non-blocking UI delivery, generation validation, and frame-pacing to ensure smooth presentation output.
/// </summary>
public sealed class Direct3D11PresentationRenderer : IPresentationOutputRenderer
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SoftwareBitmapSource _softwareBitmapSource;
    private long _currentGeneration;
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

    /// <summary>
    /// Renders a thread-safe ref-counted bitmap to the presentation output surface with non-blocking UI dispatch and generation validation.
    /// </summary>
    public Task RenderSharedBitmapAsync(RefCountedSoftwareBitmap sharedBitmap, long generation = 0)
    {
        ArgumentNullException.ThrowIfNull(sharedBitmap);

        if (_isDisposed || _isFrozen)
        {
            return Task.CompletedTask;
        }

        // Generation check: drop stale frames from superseded capture sessions
        if (generation != 0 && generation < Volatile.Read(ref _currentGeneration))
        {
            return Task.CompletedTask;
        }

        // Frame Dropping Gate: If previous frame is still being presented on UI thread, drop to maintain real-time playback
        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        if (!sharedBitmap.TryAddRef())
        {
            Interlocked.Exchange(ref _isProcessingFrame, 0);
            return Task.CompletedTask;
        }

        long targetGen = generation != 0 ? generation : Volatile.Read(ref _currentGeneration);

        // Decoupled non-blocking UI dispatch (worker thread never blocks on UI thread)
        var enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (!_isDisposed && !_isFrozen && (targetGen == 0 || targetGen >= Volatile.Read(ref _currentGeneration)))
                {
                    await _softwareBitmapSource.SetBitmapAsync(sharedBitmap.Bitmap);
                }
            }
            catch (Exception ex)
            {
                // Transient render errors during window resize or shutdown safely observed
                Debug.WriteLine($"[Direct3D11PresentationRenderer] Render exception observed: {ex.Message}");
            }
            finally
            {
                sharedBitmap.Release();
                Interlocked.Exchange(ref _isProcessingFrame, 0);
            }
        });

        if (!enqueued)
        {
            sharedBitmap.Release();
            Interlocked.Exchange(ref _isProcessingFrame, 0);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Standalone fallback bitmap renderer.
    /// </summary>
    public Task RenderBitmapAsync(SoftwareBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (_isDisposed || _isFrozen)
        {
            return Task.CompletedTask;
        }

        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        var enqueued = _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (!_isDisposed && !_isFrozen)
                {
                    await _softwareBitmapSource.SetBitmapAsync(bitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Direct3D11PresentationRenderer] Fallback render exception: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingFrame, 0);
            }
        });

        if (!enqueued)
        {
            Interlocked.Exchange(ref _isProcessingFrame, 0);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Direct frame rendering fallback.
    /// </summary>
    public async Task RenderFrameAsync(Direct3D11CaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_isDisposed || _isFrozen)
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
                Interlocked.Exchange(ref _isProcessingFrame, 0);
                return;
            }

            using var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);
            if (_isDisposed || _isFrozen || softwareBitmap is null)
            {
                Interlocked.Exchange(ref _isProcessingFrame, 0);
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

            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isDisposed && !_isFrozen)
                    {
                        await _softwareBitmapSource.SetBitmapAsync(displayBitmap);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Direct3D11PresentationRenderer] Frame fallback render exception: {ex.Message}");
                }
                finally
                {
                    displayBitmap.Dispose();
                    Interlocked.Exchange(ref _isProcessingFrame, 0);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Direct3D11PresentationRenderer] Surface copy exception: {ex.Message}");
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
        Interlocked.Increment(ref _currentGeneration);
        _dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (!_isDisposed)
                {
                    using var emptyBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 1, 1, BitmapAlphaMode.Premultiplied);
                    await _softwareBitmapSource.SetBitmapAsync(emptyBitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Direct3D11PresentationRenderer] Clear exception: {ex.Message}");
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
        Interlocked.Increment(ref _currentGeneration);

        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _softwareBitmapSource.Dispose();
            }
            catch
            {
                // Ignored during cleanup
            }
        });
    }
}
