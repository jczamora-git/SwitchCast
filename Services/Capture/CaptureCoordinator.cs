using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Authoritative coordinator managing the capture item factory, DirectX device, session manager, and preview renderer.
/// </summary>
public sealed partial class CaptureCoordinator : ObservableObject, ICaptureCoordinator
{
    private readonly IGraphicsCaptureItemFactory _itemFactory;
    private readonly IDirect3D11DeviceProvider _deviceProvider;
    private readonly ICaptureSessionManager _sessionManager;
    private readonly ICapturePreviewRenderer _previewRenderer;
    private readonly IPresentationStateService _presentationStateService;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

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

    public async Task StartPreviewAsync(CaptureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!source.IsAvailable)
            {
                throw new InvalidOperationException($"Source '{source.Title}' is marked unavailable.");
            }

            if (State != CaptureState.Idle)
            {
                await StopPreviewInternalAsync().ConfigureAwait(false);
            }

            State = CaptureState.Starting;
            CurrentPreviewSource = source;
            LastErrorMessage = null;

            var captureItem = _itemFactory.CreateItemForSource(source);
            _sessionManager.StartCapture(captureItem, _deviceProvider);

            State = CaptureState.Capturing;
            _presentationStateService.SetActiveSource(source);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            State = CaptureState.Failed;
            await StopPreviewInternalAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task StopPreviewAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopPreviewInternalAsync().ConfigureAwait(false);
            State = CaptureState.Idle;
            LastErrorMessage = null;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task SwitchPreviewSourceAsync(CaptureSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentPreviewSource?.Id == newSource.Id && State == CaptureState.Capturing)
            {
                return;
            }

            await StopPreviewInternalAsync().ConfigureAwait(false);

            State = CaptureState.Starting;
            CurrentPreviewSource = newSource;
            LastErrorMessage = null;

            var captureItem = _itemFactory.CreateItemForSource(newSource);
            _sessionManager.StartCapture(captureItem, _deviceProvider);

            State = CaptureState.Capturing;
            _presentationStateService.SetActiveSource(newSource);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            State = CaptureState.Failed;
            await StopPreviewInternalAsync().ConfigureAwait(false);
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

    private async void OnFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (State == CaptureState.Capturing)
        {
            await _previewRenderer.RenderFrameAsync(e.Frame).ConfigureAwait(false);
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
