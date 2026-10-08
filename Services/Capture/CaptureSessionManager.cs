using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Manages active GraphicsCaptureSession and Direct3D11CaptureFramePool lifecycles.
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
                _item = item;
                _deviceProvider = deviceProvider;
                _lastSize = item.Size;

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

        if (_item is not null)
        {
            _item.Closed -= OnItemClosed;
            _item = null;
        }

        if (_session is not null)
        {
            _session.Dispose();
            _session = null;
        }

        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
            _framePool.Dispose();
            _framePool = null;
        }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }

            // Check if captured surface dimensions changed (window resize, display rotation)
            if (frame.ContentSize.Width != _lastSize.Width || frame.ContentSize.Height != _lastSize.Height)
            {
                lock (_syncLock)
                {
                    if (_isCapturing && _framePool is not null && _deviceProvider is not null)
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

            FrameArrived?.Invoke(this, new FrameArrivedEventArgs(frame));
        }
        catch (Exception ex)
        {
            CaptureError?.Invoke(this, ex);
        }
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
