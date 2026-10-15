using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using SwitchCast.Models;

namespace SwitchCast.Services.Camera;

/// <summary>
/// Service managing live camera video frame acquisition via Windows.Media.Capture and MediaFrameReader.
/// Releases hardware resources immediately upon capture stop or presentation switch.
/// </summary>
public sealed class CameraCaptureService : ICameraCaptureService
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public bool IsCapturing => _mediaCapture is not null && _frameReader is not null;

    public CameraSource? ActiveCamera { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public event EventHandler<SoftwareBitmap>? FrameArrived;
    public event EventHandler? CaptureStopped;

    public async Task<bool> StartCaptureAsync(CameraSource camera, CancellationToken cancellationToken = default)
    {
        if (camera is null)
        {
            throw new ArgumentNullException(nameof(camera));
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCaptureInternalAsync().ConfigureAwait(false);

            LastErrorMessage = null;
            ActiveCamera = camera;

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
            catch (UnauthorizedAccessException)
            {
                LastErrorMessage = "Camera access is blocked by Windows privacy settings. Please allow camera access in Windows Settings > Privacy & security > Camera.";
                Debug.WriteLine($"[CameraCaptureService] Privacy access denied: {LastErrorMessage}");
                mediaCapture.Dispose();
                ActiveCamera = null;
                return false;
            }
            catch (COMException comEx) when (comEx.ErrorCode == unchecked((int)0xC00D3704) || comEx.ErrorCode == unchecked((int)0x80070005))
            {
                LastErrorMessage = "Camera is currently in use by another application or unavailable.";
                Debug.WriteLine($"[CameraCaptureService] Device busy (0x{comEx.ErrorCode:X8}): {LastErrorMessage}");
                mediaCapture.Dispose();
                ActiveCamera = null;
                return false;
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to initialize camera: {ex.Message}";
                Debug.WriteLine($"[CameraCaptureService] Init error: {ex.Message}");
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
                await StopCaptureInternalAsync().ConfigureAwait(false);
                return false;
            }

            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource).AsTask(cancellationToken).ConfigureAwait(false);
            _frameReader.FrameArrived += OnFrameArrived;

            var status = await _frameReader.StartAsync().AsTask(cancellationToken).ConfigureAwait(false);
            if (status != MediaFrameReaderStartStatus.Success)
            {
                LastErrorMessage = $"Failed to start frame reader: {status}";
                await StopCaptureInternalAsync().ConfigureAwait(false);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            await StopCaptureInternalAsync().ConfigureAwait(false);
            return false;
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Unexpected error starting camera: {ex.Message}";
            Debug.WriteLine($"[CameraCaptureService] Start error: {ex.Message}");
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

    private async Task StopCaptureInternalAsync()
    {
        if (_frameReader is not null)
        {
            try
            {
                _frameReader.FrameArrived -= OnFrameArrived;
                await _frameReader.StopAsync().AsTask().ConfigureAwait(false);
            }
            catch { }
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

        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            if (frame?.VideoMediaFrame?.SoftwareBitmap is SoftwareBitmap bitmap)
            {
                FrameArrived?.Invoke(this, bitmap);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraCaptureService] OnFrameArrived error: {ex.Message}");
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
