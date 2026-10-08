using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Authoritative coordinator managing the capture item factory, DirectX device, session manager, and preview renderer.
/// Implements serialized transition management, latest-request-wins coalescing, and non-blocking frame forwarding.
/// </summary>
public sealed partial class CaptureCoordinator : ObservableObject, ICaptureCoordinator
{
    private readonly IGraphicsCaptureItemFactory _itemFactory;
    private readonly IDirect3D11DeviceProvider _deviceProvider;
    private readonly ICaptureSessionManager _sessionManager;
    private readonly ICapturePreviewRenderer _previewRenderer;
    private readonly IPresentationStateService _presentationStateService;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

    private CaptureSource? _targetRequestedSource;
    private long _transitionSequenceNumber;

    [ObservableProperty]
    private CaptureState _state = CaptureState.Idle;

    [ObservableProperty]
    private CaptureSource? _currentPreviewSource;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public CaptureCoordinator(
        IGraphicsCaptureItemFactory itemFactory,
        IDirect3D11DeviceProvider deviceProvider,
        ICaptureSessionManager sessionManager,
        ICapturePreviewRenderer previewRenderer,
        IPresentationStateService presentationStateService)
    {
        _itemFactory = itemFactory ?? throw new ArgumentNullException(nameof(itemFactory));
        _deviceProvider = deviceProvider ?? throw new ArgumentNullException(nameof(deviceProvider));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _previewRenderer = previewRenderer ?? throw new ArgumentNullException(nameof(previewRenderer));
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));

        _sessionManager.FrameArrived += OnFrameArrived;
        _sessionManager.SourceClosed += OnSourceClosed;
        _sessionManager.CaptureError += OnCaptureError;
    }

    public ImageSource? PreviewImageSource => _previewRenderer.PreviewImageSource;

    public event EventHandler<FrameArrivedEventArgs>? FrameArrived;

    public async Task StartPreviewAsync(CaptureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        long sequence = Interlocked.Increment(ref _transitionSequenceNumber);
        Volatile.Write(ref _targetRequestedSource, source);

        Debug.WriteLine($"[CaptureCoordinator] StartPreview requested seq={sequence} source={source.Id}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _transitionSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[CaptureCoordinator] StartPreview superseded seq={sequence}");
                return;
            }

            if (!source.IsAvailable)
            {
                throw new InvalidOperationException($"Source '{source.Title}' is marked unavailable.");
            }

            if (State != CaptureState.Idle)
            {
                await StopPreviewInternalAsync().ConfigureAwait(false);
            }

            if (Volatile.Read(ref _transitionSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[CaptureCoordinator] StartPreview superseded after stop seq={sequence}");
                return;
            }

            State = CaptureState.Starting;
            CurrentPreviewSource = source;
            LastErrorMessage = null;

            var captureItem = _itemFactory.CreateItemForSource(source);
            _sessionManager.StartCapture(captureItem, _deviceProvider);

            State = CaptureState.Capturing;
            _presentationStateService.SetActiveSource(source);
            Debug.WriteLine($"[CaptureCoordinator] StartPreview completed seq={sequence} source={source.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            State = CaptureState.Failed;
            await StopPreviewInternalAsync().ConfigureAwait(false);
            Debug.WriteLine($"[CaptureCoordinator] StartPreview failed seq={sequence} error={ex.Message}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task StopPreviewAsync()
    {
        long sequence = Interlocked.Increment(ref _transitionSequenceNumber);
        Volatile.Write(ref _targetRequestedSource, null);

        Debug.WriteLine($"[CaptureCoordinator] StopPreview requested seq={sequence}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopPreviewInternalAsync().ConfigureAwait(false);
            State = CaptureState.Idle;
            LastErrorMessage = null;
            Debug.WriteLine($"[CaptureCoordinator] StopPreview completed seq={sequence}");
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task SwitchPreviewSourceAsync(CaptureSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        long sequence = Interlocked.Increment(ref _transitionSequenceNumber);
        Volatile.Write(ref _targetRequestedSource, newSource);

        Debug.WriteLine($"[CaptureCoordinator] SwitchPreview requested seq={sequence} target={newSource.Id}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Check if superseded while waiting for semaphore
            if (Volatile.Read(ref _transitionSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[CaptureCoordinator] SwitchPreview superseded seq={sequence}");
                return;
            }

            var targetSource = Volatile.Read(ref _targetRequestedSource);
            if (targetSource is null)
            {
                return;
            }

            if (CurrentPreviewSource?.Id == targetSource.Id && State == CaptureState.Capturing)
            {
                return;
            }

            await StopPreviewInternalAsync().ConfigureAwait(false);

            // Double check if superseded during stop
            if (Volatile.Read(ref _transitionSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[CaptureCoordinator] SwitchPreview superseded after stop seq={sequence}");
                return;
            }

            State = CaptureState.Starting;
            CurrentPreviewSource = targetSource;
            LastErrorMessage = null;

            var captureItem = _itemFactory.CreateItemForSource(targetSource);
            _sessionManager.StartCapture(captureItem, _deviceProvider);

            State = CaptureState.Capturing;
            _presentationStateService.SetActiveSource(targetSource);
            Debug.WriteLine($"[CaptureCoordinator] SwitchPreview completed seq={sequence} target={targetSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            State = CaptureState.Failed;
            await StopPreviewInternalAsync().ConfigureAwait(false);
            Debug.WriteLine($"[CaptureCoordinator] SwitchPreview failed seq={sequence} error={ex.Message}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    private async Task StopPreviewInternalAsync()
    {
        _sessionManager.StopCapture();
        _previewRenderer.Clear();
        CurrentPreviewSource = null;
        _presentationStateService.SetActiveSource(null);
        await Task.CompletedTask;
    }

    private void OnFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (State != CaptureState.Capturing)
        {
            return;
        }

        try
        {
            if (e.SharedBitmap is not null)
            {
                _ = _previewRenderer.RenderSharedBitmapAsync(e.SharedBitmap, e.Generation);
            }
            else if (e.SoftwareBitmap is not null)
            {
                _ = _previewRenderer.RenderBitmapAsync(e.SoftwareBitmap);
            }
            else if (e.Frame is not null)
            {
                _ = _previewRenderer.RenderFrameAsync(e.Frame);
            }

            FrameArrived?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CaptureCoordinator] OnFrameArrived handled error: {ex.Message}");
        }
    }

    private async void OnSourceClosed(object? sender, EventArgs e)
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var sourceName = CurrentPreviewSource?.Title ?? "Selected source";
            LastErrorMessage = $"Capture source '{sourceName}' was closed or removed.";
            await StopPreviewInternalAsync().ConfigureAwait(false);
            State = CaptureState.Failed;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    private async void OnCaptureError(object? sender, Exception ex)
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            LastErrorMessage = $"Capture stream error: {ex.Message}";
            await StopPreviewInternalAsync().ConfigureAwait(false);
            State = CaptureState.Failed;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _sessionManager.FrameArrived -= OnFrameArrived;
        _sessionManager.SourceClosed -= OnSourceClosed;
        _sessionManager.CaptureError -= OnCaptureError;

        _sessionManager.Dispose();
        _previewRenderer.Dispose();
        _deviceProvider.Dispose();
        _transitionSemaphore.Dispose();
    }
}
