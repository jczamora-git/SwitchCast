using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative coordinator managing the Presentation Output Window, presentation lifecycle, freeze/blackout, and frame distribution.
/// Implements serialized transition management, latest-request-wins coalescing, and non-blocking frame forwarding across all 4 source types.
/// </summary>
public sealed partial class PresentationCoordinator : ObservableObject, IPresentationCoordinator
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationOutputRenderer _outputRenderer;
    private readonly IWindowActivationService _windowActivationService;
    private readonly IMediaPresentationService _mediaPresentationService;
    private readonly INavigationService? _navigationService;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

    private CaptureSource? _targetRequestedPresentationSource;
    private long _presentationSequenceNumber;
    private PresentationStatus _previousStatusBeforeBlackout = PresentationStatus.Active;
    private volatile bool _isStoppingOrShuttingDown;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public PresentationCoordinator(
        IPresentationStateService presentationStateService,
        IPresentationWindowService presentationWindowService,
        ICaptureCoordinator captureCoordinator,
        IPresentationOutputRenderer outputRenderer,
        IWindowActivationService windowActivationService,
        IMediaPresentationService? mediaPresentationService = null,
        INavigationService? navigationService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _outputRenderer = outputRenderer ?? throw new ArgumentNullException(nameof(outputRenderer));
        _windowActivationService = windowActivationService ?? throw new ArgumentNullException(nameof(windowActivationService));
        _mediaPresentationService = mediaPresentationService ?? new MediaPresentationService();
        _navigationService = navigationService;

        _presentationStateService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IPresentationStateService.Status) ||
                e.PropertyName == nameof(IPresentationStateService.ActiveSource) ||
                e.PropertyName == nameof(IPresentationStateService.SwitchMode) ||
                e.PropertyName == nameof(IPresentationStateService.SelectedSource) ||
                e.PropertyName == nameof(IPresentationStateService.ForegroundSource))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(CurrentPresentationSource));
                OnPropertyChanged(nameof(SelectedSource));
                OnPropertyChanged(nameof(ForegroundSource));
                OnPropertyChanged(nameof(SwitchMode));
                OnPropertyChanged(nameof(IsLive));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsBlackout));
                OnPropertyChanged(nameof(IsActiveSourceMedia));
                OnPropertyChanged(nameof(IsActiveSourceVideo));
                OnPropertyChanged(nameof(IsActiveSourceImage));
            }
        };

        _mediaPresentationService.MediaStateChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(DirectImageSource));
            OnPropertyChanged(nameof(MediaPlayer));
            if (!string.IsNullOrEmpty(_mediaPresentationService.LastErrorMessage))
            {
                LastErrorMessage = _mediaPresentationService.LastErrorMessage;
            }
        };

        _captureCoordinator.FrameArrived += OnCaptureFrameArrived;
        _presentationWindowService.WindowOpened += (s, e) => OnPropertyChanged(nameof(IsOutputWindowOpen));
        _presentationWindowService.WindowClosed += OnWindowClosed;
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public PresenterSwitchMode SwitchMode => _presentationStateService.SwitchMode;

    public CaptureSource? SelectedSource => _presentationStateService.SelectedSource;

    public CaptureSource? ForegroundSource => _presentationStateService.ForegroundSource;

    public CaptureSource? CurrentPresentationSource => _presentationStateService.ActiveSource;

    public bool IsOutputWindowOpen => _presentationWindowService.IsWindowOpen;

    public bool IsLive => Status == PresentationStatus.Active;

    public bool IsPaused => Status == PresentationStatus.Paused;

    public bool IsBlackout => Status == PresentationStatus.Blackout;

    public bool IsActiveSourceMedia => CurrentPresentationSource?.Type == SourceType.Image || CurrentPresentationSource?.Type == SourceType.Video;

    public bool IsActiveSourceVideo => CurrentPresentationSource?.Type == SourceType.Video;

    public bool IsActiveSourceImage => CurrentPresentationSource?.Type == SourceType.Image;

    public IMediaPresentationService MediaPresentationService => _mediaPresentationService;

    public Task SetSwitchModeAsync(PresenterSwitchMode mode)
    {
        _presentationStateService.SetSwitchMode(mode);
        return Task.CompletedTask;
    }

    public ImageSource? PresentationImageSource => _outputRenderer.PresentationImageSource;

    public ImageSource? DirectImageSource => _mediaPresentationService.DirectImageSource;

    public Windows.Media.Playback.MediaPlayer? MediaPlayer => _mediaPresentationService.Player;

    public Task OpenOutputWindowAsync()
    {
        _presentationWindowService.ShowPresentationWindow();
        OnPropertyChanged(nameof(IsOutputWindowOpen));
        return Task.CompletedTask;
    }

    public async Task CloseOutputWindowAsync()
    {
        if (Status != PresentationStatus.Idle)
        {
            await StopPresentationAsync().ConfigureAwait(false);
        }

        _presentationWindowService.ClosePresentationWindow();
        OnPropertyChanged(nameof(IsOutputWindowOpen));
    }

    public async Task StartPresentationAsync(CaptureSource? source = null)
    {
        var targetSource = source ??
                           _presentationStateService.ActiveSource ??
                           _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);

        if (targetSource is null || !targetSource.IsAvailable)
        {
            throw new InvalidOperationException("No available presentation source selected.");
        }

        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, targetSource);

        Debug.WriteLine($"[PresentationCoordinator] StartPresentation requested seq={sequence} source={targetSource.Id} type={targetSource.Type}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] StartPresentation superseded seq={sequence}");
                return;
            }

            LastErrorMessage = null;
            _presentationStateService.SetStatus(PresentationStatus.Starting);

            // Ensure presentation output window is visible
            _presentationWindowService.ShowPresentationWindow();
            OnPropertyChanged(nameof(IsOutputWindowOpen));

            if (targetSource is ImageMediaSource imageSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                await _mediaPresentationService.LoadImageAsync(imageSource).ConfigureAwait(false);
            }
            else if (targetSource is VideoMediaSource videoSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.PlayVideoAsync(videoSource).ConfigureAwait(false);
            }
            else
            {
                // Window or Display source
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);

                if (_captureCoordinator.State != CaptureState.Capturing)
                {
                    await _captureCoordinator.StartPreviewAsync(targetSource).ConfigureAwait(false);
                }
                else if (_captureCoordinator.CurrentPreviewSource?.Id != targetSource.Id)
                {
                    await _captureCoordinator.SwitchPreviewSourceAsync(targetSource).ConfigureAwait(false);
                }

                _outputRenderer.Resume();
            }

            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] StartPresentation superseded after source start seq={sequence}");
                return;
            }

            _presentationStateService.SetActiveSource(targetSource);
            _presentationStateService.SetSelectedSource(targetSource);
            if (SwitchMode == PresenterSwitchMode.ActiveAndLive)
            {
                if (targetSource is WindowSource)
                {
                    bool activated = _windowActivationService.ActivateSource(targetSource);
                    if (activated)
                    {
                        _presentationStateService.SetForegroundSource(targetSource);
                    }
                }
                else if (targetSource is ImageMediaSource or VideoMediaSource)
                {
                    ActivatePresentationOutput();
                    _presentationStateService.SetForegroundSource(null);
                }
                else
                {
                    _presentationStateService.SetForegroundSource(null);
                }
            }
            _presentationStateService.SetStatus(PresentationStatus.Active);
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation completed seq={sequence} source={targetSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation failed seq={sequence} error={ex.Message}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task StopPresentationAsync(bool isShuttingDown = false)
    {
        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, null);

        Debug.WriteLine($"[PresentationCoordinator] StopPresentation requested seq={sequence} isShuttingDown={isShuttingDown}");

        _isStoppingOrShuttingDown = true;

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _outputRenderer.Clear();
            _mediaPresentationService.ClearImage();
            await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);

            _presentationStateService.SetActiveSource(null);
            _presentationStateService.SetForegroundSource(null);
            _presentationStateService.SetStatus(PresentationStatus.Idle);
            LastErrorMessage = null;
            await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);

            // Close the presentation output window on stop
            _presentationWindowService.ClosePresentationWindow();

            if (!isShuttingDown)
            {
                // Bring Main Control Dashboard to front and navigate to Dashboard page
                _windowActivationService.ActivateMainWindow();
                _navigationService?.NavigateToDashboard();
            }

            Debug.WriteLine($"[PresentationCoordinator] StopPresentation completed seq={sequence}");
        }
        finally
        {
            if (!isShuttingDown)
            {
                _isStoppingOrShuttingDown = false;
            }
            _transitionSemaphore.Release();
        }
    }

    public async Task SwitchPresentationSourceAsync(CaptureSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, newSource);

        Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation requested seq={sequence} target={newSource.Id} type={newSource.Type}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation superseded seq={sequence}");
                return;
            }

            if (!newSource.IsAvailable)
            {
                throw new InvalidOperationException($"Source '{newSource.Title}' is marked unavailable.");
            }

            if (_presentationStateService.ActiveSource?.Id == newSource.Id && Status == PresentationStatus.Active)
            {
                return;
            }

            LastErrorMessage = null;

            if (newSource is ImageMediaSource imageSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                await _mediaPresentationService.LoadImageAsync(imageSource).ConfigureAwait(false);
            }
            else if (newSource is VideoMediaSource videoSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.PlayVideoAsync(videoSource).ConfigureAwait(false);
            }
            else
            {
                // Window or Display source
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);

                if (_captureCoordinator.State != CaptureState.Capturing)
                {
                    await _captureCoordinator.StartPreviewAsync(newSource).ConfigureAwait(false);
                }
                else
                {
                    await _captureCoordinator.SwitchPreviewSourceAsync(newSource).ConfigureAwait(false);
                }

                _outputRenderer.Resume();
            }

            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation superseded after source switch seq={sequence}");
                return;
            }

            _presentationStateService.SetActiveSource(newSource);
            if (Status == PresentationStatus.Paused || Status == PresentationStatus.Blackout)
            {
                if (newSource is VideoMediaSource)
                {
                    _mediaPresentationService.PauseVideo();
                }
            }
            else
            {
                _presentationStateService.SetStatus(PresentationStatus.Active);
            }

            if (SwitchMode == PresenterSwitchMode.ActiveAndLive && (newSource is ImageMediaSource or VideoMediaSource))
            {
                ActivatePresentationOutput();
            }

            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation completed seq={sequence} target={newSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation failed seq={sequence} error={ex.Message}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task PausePresentationAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Active)
            {
                if (CurrentPresentationSource?.Type == SourceType.Video)
                {
                    _mediaPresentationService.PauseVideo();
                }
                else
                {
                    _outputRenderer.Freeze();
                }

                _presentationStateService.SetStatus(PresentationStatus.Paused);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task ResumePresentationAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Paused || Status == PresentationStatus.Blackout)
            {
                if (CurrentPresentationSource?.Type == SourceType.Video)
                {
                    _mediaPresentationService.ResumeVideo();
                }
                else
                {
                    _outputRenderer.Resume();
                }

                _presentationStateService.SetStatus(PresentationStatus.Active);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task ToggleBlackoutAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Blackout)
            {
                if (_previousStatusBeforeBlackout == PresentationStatus.Active)
                {
                    if (CurrentPresentationSource?.Type == SourceType.Video)
                    {
                        _mediaPresentationService.ResumeVideo();
                    }
                    else
                    {
                        _outputRenderer.Resume();
                    }
                    _presentationStateService.SetStatus(PresentationStatus.Active);
                }
                else
                {
                    if (CurrentPresentationSource?.Type != SourceType.Video)
                    {
                        _outputRenderer.Freeze();
                    }
                    _presentationStateService.SetStatus(PresentationStatus.Paused);
                }
            }
            else if (Status == PresentationStatus.Active || Status == PresentationStatus.Paused)
            {
                _previousStatusBeforeBlackout = Status;
                if (CurrentPresentationSource?.Type == SourceType.Video)
                {
                    _mediaPresentationService.PauseVideo();
                }
                _presentationStateService.SetStatus(PresentationStatus.Blackout);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task ExecuteSourceSwitchAsync(CaptureSource targetSource)
    {
        ArgumentNullException.ThrowIfNull(targetSource);

        _presentationStateService.SetSelectedSource(targetSource);

        var mode = SwitchMode;
        Debug.WriteLine($"[PresentationCoordinator] ExecuteSourceSwitch mode={mode} target={targetSource.Id} ({targetSource.Title})");

        switch (mode)
        {
            case PresenterSwitchMode.ActiveAndLive:
                if (targetSource is WindowSource)
                {
                    bool activated = _windowActivationService.ActivateSource(targetSource);
                    if (activated)
                    {
                        _presentationStateService.SetForegroundSource(targetSource);
                    }
                    else
                    {
                        _presentationStateService.SetForegroundSource(null);
                    }
                }
                else
                {
                    _presentationStateService.SetForegroundSource(null);
                }

                if (Status == PresentationStatus.Idle)
                {
                    await StartPresentationAsync(targetSource).ConfigureAwait(false);
                }
                else
                {
                    await SwitchPresentationSourceAsync(targetSource).ConfigureAwait(false);
                }
                break;

            case PresenterSwitchMode.ActiveOnly:
                if (targetSource is WindowSource)
                {
                    bool actOnly = _windowActivationService.ActivateSource(targetSource);
                    if (actOnly)
                    {
                        _presentationStateService.SetForegroundSource(targetSource);
                    }
                    else
                    {
                        _presentationStateService.SetForegroundSource(null);
                    }
                }
                else if (targetSource is ImageMediaSource or VideoMediaSource)
                {
                    _presentationStateService.SetForegroundSource(null);
                    ActivatePresentationOutput();
                }
                else
                {
                    _presentationStateService.SetForegroundSource(null);
                }
                break;

            case PresenterSwitchMode.LiveOnly:
            default:
                if (Status == PresentationStatus.Idle)
                {
                    await StartPresentationAsync(targetSource).ConfigureAwait(false);
                }
                else
                {
                    await SwitchPresentationSourceAsync(targetSource).ConfigureAwait(false);
                }
                break;
        }
    }

    public async Task SwitchToNextSourceAsync()
    {
        var queue = _presentationStateService.SelectedSources;
        if (queue.Count == 0)
        {
            return;
        }

        var current = (Status != PresentationStatus.Idle && SwitchMode != PresenterSwitchMode.ActiveOnly)
            ? (CurrentPresentationSource ?? SelectedSource)
            : (SelectedSource ?? CurrentPresentationSource);

        int currentIndex = -1;
        if (current is not null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Id == current.Id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // If current was not found in the queue (e.g. unqueued while On-Air), check if SelectedSource is in queue
        if (currentIndex < 0 && SelectedSource is not null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Id == SelectedSource.Id)
                {
                    currentIndex = i - 1;
                    if (currentIndex < 0)
                    {
                        currentIndex = queue.Count - 1;
                    }
                    break;
                }
            }
        }

        int nextIndex = (currentIndex + 1) % queue.Count;
        var nextSource = queue[nextIndex];

        await ExecuteSourceSwitchAsync(nextSource).ConfigureAwait(false);
    }

    public async Task SwitchToPreviousSourceAsync()
    {
        var queue = _presentationStateService.SelectedSources;
        if (queue.Count == 0)
        {
            return;
        }

        var current = (Status != PresentationStatus.Idle && SwitchMode != PresenterSwitchMode.ActiveOnly)
            ? (CurrentPresentationSource ?? SelectedSource)
            : (SelectedSource ?? CurrentPresentationSource);

        int currentIndex = -1;
        if (current is not null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Id == current.Id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // If current was not found in the queue (e.g. unqueued while On-Air), check if SelectedSource is in queue
        if (currentIndex < 0 && SelectedSource is not null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Id == SelectedSource.Id)
                {
                    currentIndex = (i + 1) % queue.Count;
                    break;
                }
            }
        }

        int prevIndex = (currentIndex - 1 + queue.Count) % queue.Count;
        var prevSource = queue[prevIndex];

        await ExecuteSourceSwitchAsync(prevSource).ConfigureAwait(false);
    }

    public async Task SwitchToSourceIndexAsync(int index)
    {
        var queue = _presentationStateService.SelectedSources;
        if (index < 0 || index >= queue.Count)
        {
            return;
        }

        var targetSource = queue[index];
        await ExecuteSourceSwitchAsync(targetSource).ConfigureAwait(false);
    }

    private void OnCaptureFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (Status != PresentationStatus.Active || IsActiveSourceMedia)
        {
            return;
        }

        try
        {
            if (e.SharedBitmap is not null)
            {
                _ = _outputRenderer.RenderSharedBitmapAsync(e.SharedBitmap, e.Generation);
            }
            else if (e.SoftwareBitmap is not null)
            {
                _ = _outputRenderer.RenderBitmapAsync(e.SoftwareBitmap);
            }
            else if (e.Frame is not null)
            {
                _ = _outputRenderer.RenderFrameAsync(e.Frame);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationCoordinator] OnCaptureFrameArrived handled error: {ex.Message}");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsOutputWindowOpen));

        if (Status != PresentationStatus.Idle)
        {
            _ = StopPresentationAsync();
        }
    }

    private void ActivatePresentationOutput()
    {
        if (_isStoppingOrShuttingDown)
        {
            return;
        }

        if (!_presentationWindowService.IsWindowOpen)
        {
            _presentationWindowService.ShowPresentationWindow();
            OnPropertyChanged(nameof(IsOutputWindowOpen));
        }

        var hwnd = _presentationWindowService.WindowHandle;
        if (hwnd != IntPtr.Zero)
        {
            _windowActivationService.ActivateWindow(hwnd);
        }
        else if (_presentationWindowService.IsWindowOpen)
        {
            _presentationWindowService.ShowPresentationWindow();
        }
    }

    public void Dispose()
    {
        _captureCoordinator.FrameArrived -= OnCaptureFrameArrived;
        _presentationWindowService.WindowClosed -= OnWindowClosed;

        _mediaPresentationService.Dispose();
        _outputRenderer.Dispose();
        _transitionSemaphore.Dispose();
    }
}
