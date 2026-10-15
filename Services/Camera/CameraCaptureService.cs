using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using SwitchCast.Models;
using SwitchCast.Services.Diagnostics;

namespace SwitchCast.Services.Camera;

/// <summary>
/// Service managing live camera video frame acquisition via Windows.Media.Capture and MediaFrameReader.
/// Enforces bounded backpressure, off-UI-thread pixel format conversion, and strict resource disposal.
/// Releases hardware resources immediately upon capture stop or presentation switch.
/// </summary>
public sealed class CameraCaptureService : ICameraCaptureService
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _isProcessingFrame;
    private long _frameArrivalCount;
    private long _frameRenderSubmissionCount;
    private long _frameDropCount;
    private bool _disposed;

    public bool IsCapturing => _mediaCapture is not null && _frameReader is not null;

    public CameraSource? ActiveCamera { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public long FrameArrivalCount => Interlocked.Read(ref _frameArrivalCount);

    public long FrameRenderSubmissionCount => Interlocked.Read(ref _frameRenderSubmissionCount);

    public long FrameDropCount => Interlocked.Read(ref _frameDropCount);

    public event EventHandler<SoftwareBitmap>? FrameArrived;
    public event EventHandler? CaptureStopped;

    public async Task<bool> StartCaptureAsync(CameraSource camera, CancellationToken cancellationToken = default)
    {
        if (camera is null)
        {
            throw new ArgumentNullException(nameof(camera));
        }

        var sw = Stopwatch.StartNew();

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCaptureInternalAsync().ConfigureAwait(false);

            LastErrorMessage = null;
            ActiveCamera = camera;
            Interlocked.Exchange(ref _frameArrivalCount, 0);
            Interlocked.Exchange(ref _frameRenderSubmissionCount, 0);
            Interlocked.Exchange(ref _frameDropCount, 0);
            Interlocked.Exchange(ref _isProcessingFrame, 0);

            var initSettings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = camera.DeviceId,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            var mediaCapture = new MediaCapture();
            try
            {
                await mediaCapture.InitializeAsync(initSettings).AsTask(cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException uex)
            {
                LastErrorMessage = "Camera access is blocked by Windows privacy settings. Please allow camera access in Windows Settings > Privacy & security > Camera.";
                SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), "InitializeAsync (Privacy Access Denied)", uex, camera.Id, sw.ElapsedMilliseconds);
                mediaCapture.Dispose();
                ActiveCamera = null;
                return false;
            }
            catch (COMException comEx) when (comEx.ErrorCode == unchecked((int)0xC00D3704) || comEx.ErrorCode == unchecked((int)0x80070005))
            {
                LastErrorMessage = "Camera is currently in use by another application or unavailable.";
                SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), "InitializeAsync (Device Busy)", comEx, camera.Id, sw.ElapsedMilliseconds);
                mediaCapture.Dispose();
                ActiveCamera = null;
                return false;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to initialize camera (0x{ex.HResult:X8}): {ex.Message}";
                SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), "InitializeAsync", ex, camera.Id, sw.ElapsedMilliseconds);
                mediaCapture.Dispose();
                ActiveCamera = null;
                return false;
            }

            _mediaCapture = mediaCapture;
            _mediaCapture.Failed += OnMediaCaptureFailed;

            // Select color video frame source
            var frameSource = _mediaCapture.FrameSources.Values
                .FirstOrDefault(fs => fs.Info.SourceKind == MediaFrameSourceKind.Color)
                ?? _mediaCapture.FrameSources.Values.FirstOrDefault();

            if (frameSource is null)
            {
                LastErrorMessage = "No suitable color video stream found on this camera.";
                Debug.WriteLine($"[CameraCaptureService] No color frame source available for {camera.Title}");
                await StopCaptureInternalAsync().ConfigureAwait(false);
                return false;
            }

            // Inspect and optimize capture format for preview performance (prefer ~720p or 1080p @ 30 FPS)
            if (frameSource.SupportedFormats.Any())
            {
                var preferredFormat = frameSource.SupportedFormats
                    .OrderByDescending(f => f.VideoFormat.Width <= 1920 && f.VideoFormat.Height <= 1080)
                    .ThenBy(f => Math.Abs((int)f.VideoFormat.Width - 1280) + Math.Abs((int)f.VideoFormat.Height - 720))
                    .ThenByDescending(f => f.FrameRate.Numerator / Math.Max(1, f.FrameRate.Denominator) == 30)
                    .FirstOrDefault();

                if (preferredFormat is not null && preferredFormat != frameSource.CurrentFormat)
                {
                    try
                    {
                        await frameSource.SetFormatAsync(preferredFormat).AsTask(cancellationToken).ConfigureAwait(false);
                        Debug.WriteLine($"[CameraCaptureService] Selected format: {preferredFormat.VideoFormat.Width}x{preferredFormat.VideoFormat.Height} @ {preferredFormat.FrameRate.Numerator}/{preferredFormat.FrameRate.Denominator} ({preferredFormat.Subtype})");
                    }
                    catch (Exception ex)
                    {
                        SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), "SetFormatAsync", ex, camera.Id);
                    }
                }
            }

            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource).AsTask(cancellationToken).ConfigureAwait(false);
            _frameReader.FrameArrived += OnFrameArrived;

            var status = await _frameReader.StartAsync().AsTask(cancellationToken).ConfigureAwait(false);
            if (status != MediaFrameReaderStartStatus.Success)
            {
                LastErrorMessage = $"Failed to start frame reader: {status}";
                Debug.WriteLine($"[CameraCaptureService] MediaFrameReaderStartStatus={status}");
                await StopCaptureInternalAsync().ConfigureAwait(false);
                return false;
            }

            sw.Stop();
            Debug.WriteLine($"[CameraCaptureService] Started capture for '{camera.Title}' in {sw.ElapsedMilliseconds}ms");
            return true;
        }
        catch (OperationCanceledException)
        {
            await StopCaptureInternalAsync().ConfigureAwait(false);
            return false;
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Unexpected error starting camera (0x{ex.HResult:X8}): {ex.Message}";
            SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), nameof(StartCaptureAsync), ex, camera.Id, sw.ElapsedMilliseconds);
            await StopCaptureInternalAsync().ConfigureAwait(false);
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopCaptureAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCaptureInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void NotifyFrameCompleted()
    {
        Interlocked.Exchange(ref _isProcessingFrame, 0);
    }

    private async Task StopCaptureInternalAsync()
    {
        Interlocked.Exchange(ref _isProcessingFrame, 0);

        if (_frameReader is not null)
        {
            try
            {
                _frameReader.FrameArrived -= OnFrameArrived;
                await _frameReader.StopAsync().AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), "StopFrameReader", ex);
            }
            _frameReader.Dispose();
            _frameReader = null;
        }

        if (_mediaCapture is not null)
        {
            try
            {
                _mediaCapture.Failed -= OnMediaCaptureFailed;
            }
            catch { }
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }

        ActiveCamera = null;
        CaptureStopped?.Invoke(this, EventArgs.Empty);
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_disposed || !IsCapturing)
        {
            return;
        }

        Interlocked.Increment(ref _frameArrivalCount);

        // Bounded backpressure: drop frame if downstream renderer is currently processing a frame
        if (Interlocked.CompareExchange(ref _isProcessingFrame, 1, 0) != 0)
        {
            Interlocked.Increment(ref _frameDropCount);
            return;
        }

        SoftwareBitmap? converted = null;
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            if (frame?.VideoMediaFrame?.SoftwareBitmap is SoftwareBitmap rawBitmap)
            {
                // CRITICAL: SoftwareBitmapSource strictly requires Bgra8 + Premultiplied.
                // Convert on this background thread so the UI thread is never blocked doing pixel conversions.
                if (rawBitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                    rawBitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
                {
                    converted = SoftwareBitmap.Copy(rawBitmap);
                }
                else
                {
                    converted = SoftwareBitmap.Convert(rawBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(CameraCaptureService), nameof(OnFrameArrived), ex);
            Interlocked.Exchange(ref _isProcessingFrame, 0);
            return;
        }

        if (converted is not null)
        {
            Interlocked.Increment(ref _frameRenderSubmissionCount);
            FrameArrived?.Invoke(this, converted);
        }
        else
        {
            Interlocked.Exchange(ref _isProcessingFrame, 0);
        }
    }

    private void OnMediaCaptureFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
        Debug.WriteLine($"[CameraCaptureService] MediaCapture failed: {errorEventArgs.Message} (0x{errorEventArgs.Code:X8})");
        LastErrorMessage = errorEventArgs.Message;
        _ = StopCaptureAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopCaptureAsync().ConfigureAwait(false);
        _semaphore.Dispose();
    }
}
