using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Manages active GraphicsCaptureSession and Direct3D11CaptureFramePool lifecycles.
/// Implements session generation tracking, backpressure frame draining, and zero-leak ref-counted bitmap delivery.
/// </summary>
public sealed class CaptureSessionManager : ICaptureSessionManager
{
    private readonly object _syncLock = new();
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private IDirect3D11DeviceProvider? _deviceProvider;
    private SizeInt32 _lastSize;
    private bool _isCapturing;
    private bool _isDisposed;
    private int _isProcessingFrame;
    private long _sessionGeneration;

    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;
    public event EventHandler? SourceClosed;
    public event EventHandler<Exception>? CaptureError;

    public bool IsCapturing
    {
        get
        {
            lock (_syncLock)
            {
                return _isCapturing;
            }
        }
    }

    /// <summary>
    /// Current session generation counter.
    /// </summary>
    public long SessionGeneration => Volatile.Read(ref _sessionGeneration);

    public void StartCapture(GraphicsCaptureItem item, IDirect3D11DeviceProvider deviceProvider)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(deviceProvider);

        lock (_syncLock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CaptureSessionManager));
            }

            if (_isCapturing)
            {
                StopCaptureInternal();
            }

            try
            {
                Interlocked.Increment(ref _sessionGeneration);
                _item = item;
                _deviceProvider = deviceProvider;
                _lastSize = item.Size;
                Interlocked.Exchange(ref _isProcessingFrame, 0);

                // Ensure Direct3D 11 device is active
                _deviceProvider.EnsureDevice();

                // Create frame pool with standard 32-bit BGRA format and 2 backbuffers
                _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _deviceProvider.Device,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _lastSize);

                _framePool.FrameArrived += OnFrameArrived;
                _item.Closed += OnItemClosed;

                _session = _framePool.CreateCaptureSession(_item);

                if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
                {
                    try
                    {
                        _session.IsCursorCaptureEnabled = true;
                    }
                    catch
                    {
                        // Ignored on platforms without cursor toggle support
                    }
                }

                _session.StartCapture();
                _isCapturing = true;
            }
            catch (Exception ex)
            {
                StopCaptureInternal();
                CaptureError?.Invoke(this, ex);
                throw;
            }
        }
    }

    public void StopCapture()
    {
        lock (_syncLock)
        {
            StopCaptureInternal();
        }
    }

    private void StopCaptureInternal()
    {
        _isCapturing = false;
        Interlocked.Increment(ref _sessionGeneration);
        Interlocked.Exchange(ref _isProcessingFrame, 0);

        if (_item is not null)
        {
            _item.Closed -= OnItemClosed;
            _item = null;
        }

        if (_session is not null)
        {
            try
            {
                _session.Dispose();
            }
            catch
            {
                // Ignore transient disposal exceptions
            }
            _session = null;
        }

        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
            try
            {
                _framePool.Dispose();
            }
            catch
            {
                // Ignore transient disposal exceptions
            }
            _framePool = null;
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        long currentGen = Volatile.Read(ref _sessionGeneration);

        // Pacing & Backpressure: If previous frame conversion is still running, drain and drop this frame
        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            try
            {
                using var skipped = sender.TryGetNextFrame();
            }
            catch
            {
                // Ignored
            }
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Direct3D11CaptureFrame? frame = null;
                lock (_syncLock)
                {
                    if (!_isCapturing || _framePool is null || Volatile.Read(ref _sessionGeneration) != currentGen)
                    {
                        return;
                    }
                    frame = sender.TryGetNextFrame();
                }

                if (frame is null)
                {
                    return;
                }

                // Check if captured surface dimensions changed (window resize, display rotation)
                if (frame.ContentSize.Width != _lastSize.Width || frame.ContentSize.Height != _lastSize.Height)
                {
                    lock (_syncLock)
                    {
                        if (_isCapturing && _framePool is not null && _deviceProvider is not null && Volatile.Read(ref _sessionGeneration) == currentGen)
                        {
                            _lastSize = frame.ContentSize;
                            _framePool.Recreate(
                                _deviceProvider.Device,
                                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                2,
                                _lastSize);
                        }
                    }
                }

                // Keep Direct3D frame alive throughout surface copy to prevent premature WinRT COM destruction
                SoftwareBitmap? softwareBitmap = null;
                try
                {
                    var surface = frame.Surface;
                    if (surface is not null && Volatile.Read(ref _sessionGeneration) == currentGen)
                    {
                        softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);
                    }
                }
                finally
                {
                    frame.Dispose();
                    frame = null;
                }

                if (softwareBitmap is null || Volatile.Read(ref _sessionGeneration) != currentGen)
                {
                    softwareBitmap?.Dispose();
                    return;
                }

                // Ensure pixel format is BGRA8 with Premultiplied alpha for WinUI rendering
                SoftwareBitmap displayBitmap;
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
                {
                    displayBitmap = SoftwareBitmap.Convert(
                        softwareBitmap,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                    softwareBitmap.Dispose();
                }
                else
                {
                    displayBitmap = softwareBitmap;
                }

                if (Volatile.Read(ref _sessionGeneration) != currentGen)
                {
                    displayBitmap.Dispose();
                    return;
                }

                // Wrap in RefCountedSoftwareBitmap: initial count is 1 for this scope.
                // Renderers that want to present call TryAddRef() to reserve the bitmap for UI dispatch.
                // When this using block finishes, Dispose() releases the initial ref.
                // If no renderer took a ref, displayBitmap is disposed immediately.
                using var sharedBitmap = new RefCountedSoftwareBitmap(displayBitmap, initialRefCount: 1);
                FrameArrived?.Invoke(this, new FrameArrivedEventArgs(sharedBitmap, currentGen));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException or System.Runtime.InteropServices.COMException)
            {
                // Expected during session stop, window closure, or resolution switch
            }
            catch (Exception ex)
            {
                CaptureError?.Invoke(this, ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessingFrame, 0);
            }
        });
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        StopCapture();
        SourceClosed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            StopCaptureInternal();
        }
    }
}
