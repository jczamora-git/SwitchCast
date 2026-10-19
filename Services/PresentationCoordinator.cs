using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.PowerPoint;
using SwitchCast.Services.Camera;
using Windows.Graphics.Imaging;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative coordinator managing the Presentation Output Window, presentation lifecycle, freeze/blackout, and frame distribution.
/// Implements serialized transition management, latest-request-wins coalescing, and non-blocking frame forwarding across all source types.
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
    private readonly IExternalDisplayService? _externalDisplayService;
    private readonly IPowerPointPresentationService _powerPointService;
    private readonly IPowerPointLiveService? _powerPointLiveService;
    private readonly ICameraCaptureService? _cameraCaptureService;
    private readonly IPresenterDockService? _presenterDockService;
    private readonly IApplicationSettingsService? _settingsService;
    private readonly Services.Music.IBackgroundMusicService? _backgroundMusicService;
    private readonly Services.Performance.IPerformanceMetricsService? _performanceMetricsService;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

    private CaptureSource? _targetRequestedPresentationSource;
    private long _presentationSequenceNumber;
    private PresentationStatus _previousStatusBeforeBlackout = PresentationStatus.Active;
    private volatile bool _isStoppingOrShuttingDown;
    private int _firstCameraFrameRendered;
    private CancellationTokenSource? _cameraStartupCts;
    private readonly object _cameraFrameLock = new();
    private SoftwareBitmap? _pendingCameraFrame;
    private bool _isRenderingCameraFrame;
    private Task? _activeStopTask;
    private readonly object _stopLock = new();
    private bool _showPresentationCursor = true;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public PresentationCoordinator(
        IPresentationStateService presentationStateService,
        IPresentationWindowService presentationWindowService,
        ICaptureCoordinator captureCoordinator,
        IPresentationOutputRenderer outputRenderer,
        IWindowActivationService windowActivationService,
        IMediaPresentationService? mediaPresentationService = null,
        INavigationService? navigationService = null,
        IExternalDisplayService? externalDisplayService = null,
        IPowerPointPresentationService? powerPointService = null,
        IPowerPointLiveService? powerPointLiveService = null,
        ICameraCaptureService? cameraCaptureService = null,
        IPresenterDockService? presenterDockService = null,
        IApplicationSettingsService? settingsService = null,
        Services.Music.IBackgroundMusicService? backgroundMusicService = null,
        Services.Performance.IPerformanceMetricsService? performanceMetricsService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _outputRenderer = outputRenderer ?? throw new ArgumentNullException(nameof(outputRenderer));
        _windowActivationService = windowActivationService ?? throw new ArgumentNullException(nameof(windowActivationService));
        _mediaPresentationService = mediaPresentationService ?? new MediaPresentationService();
        _navigationService = navigationService;
        _externalDisplayService = externalDisplayService;
        _powerPointService = powerPointService ?? new PowerPointPresentationService();
        _powerPointLiveService = powerPointLiveService;
        _cameraCaptureService = cameraCaptureService;
        _presenterDockService = presenterDockService;
        _settingsService = settingsService;
        _backgroundMusicService = backgroundMusicService;
        _performanceMetricsService = performanceMetricsService;

        if (_settingsService is not null)
        {
            _showPresentationCursor = _settingsService.CurrentSettings.ShowPresentationCursor;
        }
        _captureCoordinator.SetCursorCaptureEnabled(_showPresentationCursor);

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
                OnPropertyChanged(nameof(IsActiveSourcePowerPoint));
                OnPropertyChanged(nameof(IsActiveSourceCamera));
                OnPropertyChanged(nameof(PowerPointProgressText));
                OnPropertyChanged(nameof(DirectImageSource));
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

        _powerPointService.SlideChanged += (s, e) =>
        {
            if (_powerPointService.ActivePowerPointSource is not null &&
                _presentationStateService.ActiveSource is PowerPointSource)
            {
                _presentationStateService.SetActiveSource(_powerPointService.ActivePowerPointSource);
            }
            OnPropertyChanged(nameof(PowerPointSlideImageSource));
            OnPropertyChanged(nameof(PowerPointProgressText));
            OnPropertyChanged(nameof(DirectImageSource));
        };

        if (_powerPointLiveService is not null)
        {
            _powerPointLiveService.SlidePositionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(PowerPointProgressText));
            };
        }

        if (_cameraCaptureService is not null)
        {
            _cameraCaptureService.FrameArrived += OnCameraFrameArrived;
            _outputRenderer.FrameRenderCompleted += OnOutputRendererFrameRenderCompleted;
        }

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

    public bool IsActiveSourcePowerPoint => CurrentPresentationSource?.Type == SourceType.PowerPoint;

    public bool IsActiveSourceCamera => CurrentPresentationSource?.Type == SourceType.Camera;

    public string? PowerPointProgressText
    {
        get
        {
            if (CurrentPresentationSource is PowerPointSource ppt)
            {
                if (ppt.IsLiveSlideshow && _powerPointLiveService is not null)
                {
                    return _powerPointLiveService.TotalSlideCount > 0
                        ? $"Slide {_powerPointLiveService.CurrentSlideIndex} of {_powerPointLiveService.TotalSlideCount}"
                        : "PowerPoint Live";
                }

                int current = _powerPointService.CurrentSlideIndex;
                int total = _powerPointService.TotalSlideCount > 0 ? _powerPointService.TotalSlideCount : ppt.SlideCount;
                return total > 0 ? $"Slide {current} of {total}" : null;
            }
            return null;
        }
    }

    public ImageSource? PowerPointSlideImageSource => _powerPointService.CurrentSlideImageSource;

    public IMediaPresentationService MediaPresentationService => _mediaPresentationService;

    public Task SetSwitchModeAsync(PresenterSwitchMode mode)
    {
        _presentationStateService.SetSwitchMode(mode);
        return Task.CompletedTask;
    }

    public ImageSource? PresentationImageSource => _outputRenderer.PresentationImageSource;

    public ImageSource? DirectImageSource =>
        IsActiveSourcePowerPoint
            ? _powerPointService.CurrentSlideImageSource
            : _mediaPresentationService.DirectImageSource;

    public Windows.Media.Playback.MediaPlayer? MediaPlayer => _mediaPresentationService.Player;

    public Services.Music.IBackgroundMusicService? BackgroundMusicService => _backgroundMusicService;

    public bool ShowPresentationCursor => _showPresentationCursor;

    public async Task SetShowPresentationCursorAsync(bool show)
    {
        if (_showPresentationCursor == show)
        {
            return;
        }

        _showPresentationCursor = show;
        if (_settingsService is not null)
        {
            await _settingsService.SetShowPresentationCursorAsync(show).ConfigureAwait(false);
        }

        _captureCoordinator.SetCursorCaptureEnabled(show);
        OnPropertyChanged(nameof(ShowPresentationCursor));
    }

    public async Task OpenOutputWindowAsync()
    {
        _presentationWindowService.ShowPresentationWindow();
        OnPropertyChanged(nameof(IsOutputWindowOpen));

        if (_externalDisplayService is not null)
        {
            await _externalDisplayService.ApplyDisplayAutomationAsync(isStartingPresentation: false).ConfigureAwait(false);
        }
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
        var targetSource = source;
        if (targetSource is null)
        {
            if (_presentationStateService.SelectedSource is not null &&
                _presentationStateService.SelectedSource.IsAvailable &&
                _presentationStateService.SelectedSources.Any(s => s.Id == _presentationStateService.SelectedSource.Id))
            {
                targetSource = _presentationStateService.SelectedSource;
            }
            else
            {
                targetSource = _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable) ??
                               _presentationStateService.ActiveSource;
            }
        }

        if (targetSource is null || !targetSource.IsAvailable)
        {
            throw new InvalidOperationException("No available presentation source selected.");
        }

        if (_presentationStateService.ActiveSource?.Id == targetSource.Id &&
            (Status == PresentationStatus.Active || Status == PresentationStatus.Starting))
        {
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation redundant for {targetSource.Id} (Status={Status}); ensuring output window visible.");
            _presentationWindowService.ShowPresentationWindow();
            return;
        }

        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, targetSource);

        Debug.WriteLine($"[PresentationCoordinator] StartPresentation requested seq={sequence} source={targetSource.Id} type={targetSource.Type}");

        var startupSw = Stopwatch.StartNew();

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

            if (_externalDisplayService is not null)
            {
                await _externalDisplayService.ApplyDisplayAutomationAsync(isStartingPresentation: true).ConfigureAwait(false);
            }

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
            else if (targetSource is PowerPointSource pptSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _mediaPresentationService.ClearImage();
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

                if (pptSource.IsLiveSlideshow && _powerPointLiveService is not null)
                {
                    await _powerPointLiveService.StartSlideshowAsync(pptSource.FilePath).ConfigureAwait(false);
                    if (_powerPointLiveService.SlideshowWindowHandle != nint.Zero)
                    {
                        var windowSource = new WindowSource
                        {
                            Id = $"win_{_powerPointLiveService.SlideshowWindowHandle}",
                            Title = "PowerPoint Slide Show",
                            WindowHandle = _powerPointLiveService.SlideshowWindowHandle,
                            ProcessName = "POWERPNT",
                            IsAvailable = true
                        };
                        await _captureCoordinator.StartPreviewAsync(windowSource).ConfigureAwait(false);
                    }
                }
                else
                {
                    await _powerPointService.LoadPresentationAsync(pptSource).ConfigureAwait(false);
                }
            }
            else if (targetSource is CameraSource cameraSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _mediaPresentationService.ClearImage();
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }

                _presentationStateService.SetActiveSource(cameraSource);
                _presentationStateService.SetSelectedSource(cameraSource);
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);

                if (_cameraCaptureService is not null)
                {
                    bool started = await _cameraCaptureService.StartCaptureAsync(cameraSource).ConfigureAwait(false);
                    if (!started)
                    {
                        LastErrorMessage = _cameraCaptureService.LastErrorMessage ?? "Failed to start camera capture.";
                        _presentationStateService.SetStatus(PresentationStatus.Error);
                        return;
                    }
                }

                ArmCameraStartupTimeout();
            }
            else
            {
                // Window or Display source
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

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
                else if (targetSource is ImageMediaSource or VideoMediaSource or CameraSource or PowerPointSource)
                {
                    ActivatePresentationOutput();
                    _presentationStateService.SetForegroundSource(null);
                }
                else
                {
                    _presentationStateService.SetForegroundSource(null);
                }
            }
            if (targetSource is not CameraSource)
            {
                _presentationStateService.SetStatus(PresentationStatus.Active);
            }

            if (_settingsService?.CurrentSettings.AutoOpenPresenterDock != false)
            {
                _presenterDockService?.ShowDock();
            }

            _performanceMetricsService?.RecordPresentationStartup(startupSw.Elapsed.TotalMilliseconds);

            Debug.WriteLine($"[PresentationCoordinator] StartPresentation completed seq={sequence} source={targetSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException(
                nameof(PresentationCoordinator),
                nameof(StartPresentationAsync),
                ex,
                $"Seq={sequence}, SourceId={targetSource?.Id}, SourceType={targetSource?.Type}, HResult=0x{ex.HResult:X8}, ThreadId={Environment.CurrentManagedThreadId}");
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation failed seq={sequence} source={targetSource?.Id} HResult=0x{ex.HResult:X8} thread={Environment.CurrentManagedThreadId} error={ex}\n{ex.StackTrace}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public Task StopPresentationAsync(bool isShuttingDown = false)
    {
        lock (_stopLock)
        {
            if (_activeStopTask is not null && !_activeStopTask.IsCompleted)
            {
                Debug.WriteLine("[PresentationCoordinator] StopPresentation already in progress; returning active task.");
                return _activeStopTask;
            }

            _activeStopTask = ExecuteStopPresentationAsync(isShuttingDown);
            return _activeStopTask;
        }
    }

    private async Task ExecuteStopPresentationAsync(bool isShuttingDown)
    {
        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, null);

        Debug.WriteLine($"[PresentationCoordinator] StopPresentation requested seq={sequence} isShuttingDown={isShuttingDown}");
        Diagnostics.SwitchCastDiagnostics.LogInfo(
            nameof(PresentationCoordinator),
            $"StopPresentation requested seq={sequence} | isShuttingDown={isShuttingDown} | Status={_presentationStateService.Status} | PID={Environment.ProcessId}");

        _isStoppingOrShuttingDown = true;

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // 1. Invalidate camera startup & drain camera frames
            _cameraStartupCts?.Cancel();
            Interlocked.Exchange(ref _firstCameraFrameRendered, 0);

            lock (_cameraFrameLock)
            {
                _pendingCameraFrame?.Dispose();
                _pendingCameraFrame = null;
                _isRenderingCameraFrame = false;
            }

            if (_cameraCaptureService is not null)
            {
                await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
            }

            // 2. Stop WGC capture preview
            await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);

            // 3. Stop PowerPoint live slideshow & presentation
            if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
            {
                await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
            }
            _powerPointService.ClearPresentation();

            // 4. Stop Video and clear Image
            _mediaPresentationService.ClearImage();
            await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);

            // 5. Stop background music by default on presentation stop
            _backgroundMusicService?.Stop();

            // 6. Invalidate render generations & clear output renderer
            _outputRenderer.Clear();

            // 6. Update presentation state to Idle
            _presentationStateService.SetActiveSource(null);
            _presentationStateService.SetForegroundSource(null);
            _presentationStateService.SetStatus(PresentationStatus.Idle);
            LastErrorMessage = null;

            // 7. Close the presentation output window on stop (triggers PresentationWindow.Cleanup)
            _presentationWindowService.ClosePresentationWindow();

            // 8. Restore MainWindow and navigate to Dashboard if not shutting down
            if (!isShuttingDown)
            {
                _windowActivationService.ActivateMainWindow();
                _navigationService?.NavigateToDashboard();
            }

            Debug.WriteLine($"[PresentationCoordinator] StopPresentation completed seq={sequence}");
            Diagnostics.SwitchCastDiagnostics.LogInfo(
                nameof(PresentationCoordinator),
                $"StopPresentation completed seq={sequence} | PID={Environment.ProcessId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationCoordinator] StopPresentation error seq={sequence}: {ex.Message}");
            Diagnostics.SwitchCastDiagnostics.LogException(nameof(PresentationCoordinator), nameof(StopPresentationAsync), ex);
            throw;
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

        var switchSw = Stopwatch.StartNew();

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

            if (_presentationStateService.ActiveSource?.Id == newSource.Id &&
                (Status == PresentationStatus.Active || Status == PresentationStatus.Starting))
            {
                Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation redundant for {newSource.Id} (Status={Status}); ignoring duplicate switch.");
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
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }
                _cameraStartupCts?.Cancel();
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

                await _mediaPresentationService.LoadImageAsync(imageSource).ConfigureAwait(false);
            }
            else if (newSource is VideoMediaSource videoSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                _mediaPresentationService.ClearImage();
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }
                _cameraStartupCts?.Cancel();
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

                await _mediaPresentationService.PlayVideoAsync(videoSource).ConfigureAwait(false);
            }
            else if (newSource is PowerPointSource pptSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _mediaPresentationService.ClearImage();
                _cameraStartupCts?.Cancel();
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);
                lock (_cameraFrameLock)
                {
                    _pendingCameraFrame?.Dispose();
                    _pendingCameraFrame = null;
                    _isRenderingCameraFrame = false;
                }
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

                if (pptSource.IsLiveSlideshow && _powerPointLiveService is not null)
                {
                    await _powerPointLiveService.StartSlideshowAsync(pptSource.FilePath).ConfigureAwait(false);
                    if (_powerPointLiveService.SlideshowWindowHandle != nint.Zero)
                    {
                        var windowSource = new WindowSource
                        {
                            Id = $"win_{_powerPointLiveService.SlideshowWindowHandle}",
                            Title = "PowerPoint Slide Show",
                            WindowHandle = _powerPointLiveService.SlideshowWindowHandle,
                            ProcessName = "POWERPNT",
                            IsAvailable = true
                        };
                        await _captureCoordinator.StartPreviewAsync(windowSource).ConfigureAwait(false);
                    }
                }
                else
                {
                    await _powerPointService.LoadPresentationAsync(pptSource).ConfigureAwait(false);
                }
            }
            else if (newSource is CameraSource cameraSource)
            {
                if (_captureCoordinator.State == CaptureState.Capturing)
                {
                    await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
                }
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _mediaPresentationService.ClearImage();
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }

                _presentationStateService.SetStatus(PresentationStatus.Starting);
                _presentationStateService.SetActiveSource(cameraSource);
                _presentationStateService.SetSelectedSource(cameraSource);
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);

                if (_cameraCaptureService is not null)
                {
                    bool started = await _cameraCaptureService.StartCaptureAsync(cameraSource).ConfigureAwait(false);
                    if (!started)
                    {
                        LastErrorMessage = _cameraCaptureService.LastErrorMessage ?? "Failed to start camera capture.";
                        _presentationStateService.SetStatus(PresentationStatus.Error);
                        return;
                    }
                }

                ArmCameraStartupTimeout();
            }
            else
            {
                // Window or Display source
                _mediaPresentationService.ClearImage();
                await _mediaPresentationService.StopVideoAsync().ConfigureAwait(false);
                _powerPointService.ClearPresentation();
                if (_powerPointLiveService is not null && _powerPointLiveService.IsLiveSlideshowRunning)
                {
                    await _powerPointLiveService.StopSlideshowAsync().ConfigureAwait(false);
                }
                _cameraStartupCts?.Cancel();
                Interlocked.Exchange(ref _firstCameraFrameRendered, 0);
                if (_cameraCaptureService is not null)
                {
                    await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                }

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
                if (newSource is not CameraSource)
                {
                    _presentationStateService.SetStatus(PresentationStatus.Active);
                }
            }

            if (SwitchMode == PresenterSwitchMode.ActiveAndLive && (newSource is ImageMediaSource or VideoMediaSource or CameraSource or PowerPointSource))
            {
                ActivatePresentationOutput();
            }

            _performanceMetricsService?.RecordSourceSwitch(switchSw.Elapsed.TotalMilliseconds);

            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation completed seq={sequence} target={newSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            SwitchCast.Services.Diagnostics.SwitchCastDiagnostics.LogException(
                nameof(PresentationCoordinator),
                nameof(SwitchPresentationSourceAsync),
                ex,
                $"Seq={sequence}, SourceId={newSource.Id}, SourceType={newSource.Type}, HResult=0x{ex.HResult:X8}, ThreadId={Environment.CurrentManagedThreadId}");
            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation failed seq={sequence} source={newSource.Id} type={newSource.Type} HResult=0x{ex.HResult:X8} thread={Environment.CurrentManagedThreadId} error={ex}\n{ex.StackTrace}");
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

        if (_presentationStateService.ActiveSource?.Id == targetSource.Id &&
            (Status == PresentationStatus.Active || Status == PresentationStatus.Starting))
        {
            Debug.WriteLine($"[PresentationCoordinator] ExecuteSourceSwitch redundant for {targetSource.Id} (Status={Status}); ensuring output visible.");
            _presentationWindowService.ShowPresentationWindow();
            return;
        }

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
                else if (targetSource is ImageMediaSource or VideoMediaSource or PowerPointSource or CameraSource)
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

    public async Task<bool> PowerPointNextSlideAsync()
    {
        if (CurrentPresentationSource is not PowerPointSource ppt)
        {
            return false;
        }

        if (ppt.IsLiveSlideshow && _powerPointLiveService is not null)
        {
            await _powerPointLiveService.NextAsync().ConfigureAwait(false);
            OnPropertyChanged(nameof(PowerPointProgressText));
            return true;
        }

        bool result = await _powerPointService.NextSlideAsync().ConfigureAwait(false);
        if (_powerPointService.ActivePowerPointSource is not null)
        {
            _presentationStateService.SetActiveSource(_powerPointService.ActivePowerPointSource);
            _presentationStateService.UpdateSelectedSource(_powerPointService.ActivePowerPointSource);
        }
        OnPropertyChanged(nameof(PowerPointProgressText));
        OnPropertyChanged(nameof(PowerPointSlideImageSource));
        return result;
    }

    public async Task<bool> PowerPointPreviousSlideAsync()
    {
        if (CurrentPresentationSource is not PowerPointSource ppt)
        {
            return false;
        }

        if (ppt.IsLiveSlideshow && _powerPointLiveService is not null)
        {
            await _powerPointLiveService.PreviousAsync().ConfigureAwait(false);
            OnPropertyChanged(nameof(PowerPointProgressText));
            return true;
        }

        bool result = await _powerPointService.PreviousSlideAsync().ConfigureAwait(false);
        if (_powerPointService.ActivePowerPointSource is not null)
        {
            _presentationStateService.SetActiveSource(_powerPointService.ActivePowerPointSource);
            _presentationStateService.UpdateSelectedSource(_powerPointService.ActivePowerPointSource);
        }
        OnPropertyChanged(nameof(PowerPointProgressText));
        OnPropertyChanged(nameof(PowerPointSlideImageSource));
        return result;
    }

    public async Task<bool> PowerPointGoToSlideAsync(int slideIndex)
    {
        if (CurrentPresentationSource is not PowerPointSource ppt)
        {
            return false;
        }

        if (ppt.IsLiveSlideshow && _powerPointLiveService is not null)
        {
            await _powerPointLiveService.GoToSlideAsync(slideIndex).ConfigureAwait(false);
            OnPropertyChanged(nameof(PowerPointProgressText));
            return true;
        }

        bool result = await _powerPointService.GoToSlideAsync(slideIndex).ConfigureAwait(false);
        if (_powerPointService.ActivePowerPointSource is not null)
        {
            _presentationStateService.SetActiveSource(_powerPointService.ActivePowerPointSource);
            _presentationStateService.UpdateSelectedSource(_powerPointService.ActivePowerPointSource);
        }
        OnPropertyChanged(nameof(PowerPointProgressText));
        OnPropertyChanged(nameof(PowerPointSlideImageSource));
        return result;
    }

    private void OnCameraFrameArrived(object? sender, SoftwareBitmap bitmap)
    {
        if ((Status != PresentationStatus.Active && Status != PresentationStatus.Starting) ||
            !IsActiveSourceCamera ||
            _isStoppingOrShuttingDown ||
            _targetRequestedPresentationSource?.Type != SourceType.Camera)
        {
            bitmap.Dispose();
            _cameraCaptureService?.NotifyFrameCompleted();
            return;
        }

        SoftwareBitmap? droppedFrame = null;
        bool startRenderLoop = false;

        lock (_cameraFrameLock)
        {
            if (_isRenderingCameraFrame)
            {
                // Bounded latest-frame-wins: drop older pending frame in favor of the latest incoming frame
                droppedFrame = _pendingCameraFrame;
                _pendingCameraFrame = bitmap;
            }
            else
            {
                _isRenderingCameraFrame = true;
                _pendingCameraFrame = bitmap;
                startRenderLoop = true;
            }
        }

        if (droppedFrame is not null)
        {
            droppedFrame.Dispose();
            _cameraCaptureService?.NotifyFrameCompleted();
        }

        if (startRenderLoop)
        {
            _ = ProcessCameraFramesLoopAsync();
        }
    }

    private async Task ProcessCameraFramesLoopAsync()
    {
        while (true)
        {
            SoftwareBitmap? frameToRender = null;
            lock (_cameraFrameLock)
            {
                frameToRender = _pendingCameraFrame;
                _pendingCameraFrame = null;
                if (frameToRender is null)
                {
                    _isRenderingCameraFrame = false;
                    break;
                }
            }

            if ((Status != PresentationStatus.Active && Status != PresentationStatus.Starting) ||
                !IsActiveSourceCamera ||
                _isStoppingOrShuttingDown ||
                _targetRequestedPresentationSource?.Type != SourceType.Camera)
            {
                frameToRender.Dispose();
                _cameraCaptureService?.NotifyFrameCompleted();
                lock (_cameraFrameLock)
                {
                    _pendingCameraFrame?.Dispose();
                    _pendingCameraFrame = null;
                    _isRenderingCameraFrame = false;
                }
                break;
            }

            try
            {
                await _outputRenderer.RenderBitmapAsync(frameToRender).ConfigureAwait(false);

                // Transition state from Starting to Active only after successfully completing the first frame render milestone
                if (Status == PresentationStatus.Starting && IsActiveSourceCamera)
                {
                    if (Interlocked.CompareExchange(ref _firstCameraFrameRendered, 1, 0) == 0)
                    {
                        _cameraStartupCts?.Cancel();
                        Debug.WriteLine("[PresentationCoordinator] First camera frame successfully rendered. Transitioning to Active (LIVE).");
                        _presentationStateService.SetStatus(PresentationStatus.Active);
                    }
                }
            }
            catch (Exception ex)
            {
                Diagnostics.SwitchCastDiagnostics.LogException(nameof(PresentationCoordinator), nameof(ProcessCameraFramesLoopAsync), ex);
            }
        }
    }

    private void ArmCameraStartupTimeout()
    {
        _cameraStartupCts?.Cancel();
        _cameraStartupCts?.Dispose();
        _cameraStartupCts = new CancellationTokenSource();
        var token = _cameraStartupCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(8000, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested &&
                    Status == PresentationStatus.Starting &&
                    IsActiveSourceCamera)
                {
                    Debug.WriteLine("[PresentationCoordinator] Camera presentation startup timed out after 8s without receiving frames.");
                    LastErrorMessage = "Camera presentation timed out waiting for video frames. Please check camera connection and privacy settings.";
                    _presentationStateService.SetStatus(PresentationStatus.Error);
                    if (_cameraCaptureService is not null)
                    {
                        await _cameraCaptureService.StopCaptureAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation when frame arrives or presentation stops
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PresentationCoordinator] Camera startup timeout watcher exception: {ex.Message}");
            }
        }, token);
    }

    private void OnOutputRendererFrameRenderCompleted(object? sender, EventArgs e)
    {
        _cameraCaptureService?.NotifyFrameCompleted();
    }

    public void Dispose()
    {
        _captureCoordinator.FrameArrived -= OnCaptureFrameArrived;
        _presentationWindowService.WindowClosed -= OnWindowClosed;

        if (_cameraCaptureService is not null)
        {
            _cameraCaptureService.FrameArrived -= OnCameraFrameArrived;
            _outputRenderer.FrameRenderCompleted -= OnOutputRendererFrameRenderCompleted;
        }

        _cameraStartupCts?.Cancel();
        _cameraStartupCts?.Dispose();

        _mediaPresentationService.Dispose();
        _outputRenderer.Dispose();
        _transitionSemaphore.Dispose();
    }
}
