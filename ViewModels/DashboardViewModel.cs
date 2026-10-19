using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Models.Music;
using SwitchCast.Services;
using SwitchCast.Services.Capture;
using SwitchCast.Services.Media;
using SwitchCast.Services.Music;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the presenter dashboard, live preview capture, and dedicated presentation output orchestration.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly INavigationService _navigationService;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationCoordinator _presentationCoordinator;
    private readonly IPresenterDockService? _dockService;
    private readonly IApplicationSettingsService? _settingsService;
    private readonly IBackgroundMusicService? _backgroundMusicService;
    private readonly IMediaPickerService? _mediaPickerService;
    private IUIThreadDispatcher? _dispatcher;
    private bool _disposed;

    [ObservableProperty]
    private CaptureSource? _selectedPreviewSource;

    [ObservableProperty]
    private CaptureSource? _selectedPresentationSource;

    private bool _isSyncingPresentationSource;
    private readonly Services.Diagnostics.IErrorReportingService? _reportingService;

    [ObservableProperty]
    private bool _hasPreviousCrashReport;

    [ObservableProperty]
    private Models.Diagnostics.DiagnosticIncident? _latestPreviousCrash;

    public DashboardViewModel(
        IPresentationStateService presentationStateService,
        INavigationService navigationService,
        ICaptureCoordinator captureCoordinator,
        IPresentationCoordinator presentationCoordinator,
        IPresenterDockService? dockService = null,
        IApplicationSettingsService? settingsService = null,
        Services.Diagnostics.IErrorReportingService? reportingService = null,
        IUIThreadDispatcher? uiDispatcher = null,
        IBackgroundMusicService? backgroundMusicService = null,
        IMediaPickerService? mediaPickerService = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));
        _dockService = dockService;
        _settingsService = settingsService;
        _reportingService = reportingService;
        _backgroundMusicService = backgroundMusicService ?? _presentationCoordinator.BackgroundMusicService;
        _mediaPickerService = mediaPickerService;

        _dispatcher = UIThreadDispatcherProvider.Resolve(uiDispatcher);

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        _captureCoordinator.PropertyChanged += OnCaptureCoordinatorPropertyChanged;
        _presentationCoordinator.PropertyChanged += OnPresentationCoordinatorPropertyChanged;
        if (_presentationCoordinator.MediaPresentationService is not null)
        {
            _presentationCoordinator.MediaPresentationService.MediaStateChanged += (s, e) => NotifyMediaPlaybackProperties();
        }

        if (_dockService is not null)
        {
            _dockService.DockOpened += (s, e) => RunOnUIThread(() => OnPropertyChanged(nameof(IsPresenterDockOpen)));
            _dockService.DockClosed += (s, e) => RunOnUIThread(() => OnPropertyChanged(nameof(IsPresenterDockOpen)));
        }

        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.PlaybackStateChanged += OnBackgroundMusicStateChanged;
            _backgroundMusicService.TrackChanged += OnBackgroundMusicTrackChanged;
            _backgroundMusicService.PlaylistChanged += OnBackgroundMusicPlaylistChanged;
            _backgroundMusicService.PositionChanged += OnBackgroundMusicPositionChanged;
        }

        // Default selected sources if sources are already queued
        var defaultSource = _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);
        _selectedPreviewSource = defaultSource;
        _selectedPresentationSource = defaultSource;
    }

    private void CheckForPreviousCrash()
    {
        if (_reportingService is null)
        {
            return;
        }

        try
        {
            var crashes = _reportingService.GetPreviousCrashReports();
            if (crashes.Count > 0)
            {
                LatestPreviousCrash = crashes[0];
                HasPreviousCrashReport = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DashboardViewModel] CheckForPreviousCrash error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DismissPreviousCrashBanner()
    {
        HasPreviousCrashReport = false;
    }

    /// <summary>
    /// Configures the authoritative UI thread DispatcherQueue for this ViewModel.
    /// </summary>
    public void SetDispatcherQueue(Microsoft.UI.Dispatching.DispatcherQueue? queue)
    {
        _dispatcher = queue is not null ? new DispatcherQueueAdapter(queue) : null;
    }

    /// <summary>
    /// Configures a testable UI thread dispatcher for this ViewModel.
    /// </summary>
    public void SetUIThreadDispatcher(IUIThreadDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private void RunOnUIThread(Action action)
    {
        if (_disposed || _dispatcher is null)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(action);
        }
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public string StatusDisplayText => Status switch
    {
        PresentationStatus.Idle => IsCapturing ? "Live Preview Active" : "Not Started",
        PresentationStatus.Starting => "Initializing...",
        PresentationStatus.Active => "Presenting Live",
        PresentationStatus.Paused => "Paused (Frozen)",
        PresentationStatus.Blackout => "Blackout Active",
        PresentationStatus.Error => "Error",
        _ => "Unknown"
    };

    public string ActiveSourceTitle => _presentationCoordinator.CurrentPresentationSource?.Title ??
                                       _captureCoordinator.CurrentPreviewSource?.Title ??
                                       _presentationStateService.ActiveSource?.Title ??
                                       "None";

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public bool HasNoSelectedSources => !HasSelectedSources;

    public string SourceSelectorPlaceholderText => HasSelectedSources ? "Select target source..." : "No queued sources";

    public bool IsStarting => Status == PresentationStatus.Starting;

    public bool CanStartPresentation => !IsStarting && (HasActivePresentation || HasSelectedSources);

    public string StartPresentingTooltip => HasActivePresentation
        ? "Stop Live Presentation"
        : (IsStarting
            ? "Presentation starting..."
            : (HasSelectedSources ? "Start Live Presentation" : "Add a presentation source first."));

    public bool HasActiveSource => _presentationCoordinator.CurrentPresentationSource is not null ||
                                  _captureCoordinator.CurrentPreviewSource is not null ||
                                  _presentationStateService.ActiveSource is not null;

    public IReadOnlyList<CaptureSource> SelectedSources => _presentationStateService.SelectedSources;

    public CaptureState CaptureState => _captureCoordinator.State;

    public bool IsCapturing => CaptureState == CaptureState.Capturing;

    public bool IsStartingCapture => CaptureState == CaptureState.Starting;

    public bool IsCaptureIdle => CaptureState == CaptureState.Idle;

    public bool HasCaptureError => !string.IsNullOrWhiteSpace(_captureCoordinator.LastErrorMessage) ||
                                  !string.IsNullOrWhiteSpace(_presentationCoordinator.LastErrorMessage);

    public string CaptureErrorMessage => _presentationCoordinator.LastErrorMessage ??
                                         _captureCoordinator.LastErrorMessage ??
                                         string.Empty;

    public ImageSource? PreviewImageSource => _captureCoordinator.PreviewImageSource;

    public bool IsOutputWindowOpen => _presentationCoordinator.IsOutputWindowOpen;

    public bool IsPresenterDockOpen => _dockService?.IsDockOpen ?? false;

    public bool IsPresenting => _presentationCoordinator.IsLive;

    public bool IsPresentationPaused => _presentationCoordinator.IsPaused;

    public bool IsPresentationBlackout => _presentationCoordinator.IsBlackout;

    public bool HasActivePresentation => IsPresenting || IsPresentationPaused || IsPresentationBlackout;

    public bool IsActiveSourceVideo => _presentationCoordinator.IsActiveSourceVideo;

    public bool IsActiveSourceImage => _presentationCoordinator.IsActiveSourceImage;

    public bool IsVideoPlaying => _presentationCoordinator.MediaPresentationService.IsVideoPlaying;

    public bool IsVideoPaused => _presentationCoordinator.MediaPresentationService.IsVideoPaused;

    public bool IsVideoEnded => _presentationCoordinator.MediaPresentationService.IsVideoEnded;

    public bool IsVideoLooping => _presentationCoordinator.MediaPresentationService.IsLooping;

    public string VideoPlaybackButtonGlyph => IsVideoPlaying ? "\uE769" : "\uE768";

    public string VideoPlaybackButtonText => IsVideoPlaying ? "Pause Video" : (IsVideoEnded ? "Replay Video" : "Play Video");

    public string VideoLoopButtonText => IsVideoLooping ? "Loop: On" : "Loop: Off";

    public Visibility VideoControlsVisibility => (HasActivePresentation && IsActiveSourceVideo) ? Visibility.Visible : Visibility.Collapsed;

    public double MediaVolume
    {
        get => _presentationCoordinator.MediaPresentationService.Volume * 100.0;
        set
        {
            var normalized = Math.Clamp(value / 100.0, 0.0, 1.0);
            _presentationCoordinator.MediaPresentationService.SetVolume(normalized);
            OnPropertyChanged(nameof(MediaVolume));
            OnPropertyChanged(nameof(MediaVolumePercentText));
            OnPropertyChanged(nameof(MediaMuteButtonGlyph));
            OnPropertyChanged(nameof(MediaMuteButtonTooltip));
        }
    }

    public bool IsMediaMuted => _presentationCoordinator.MediaPresentationService.IsMuted;

    public string MediaVolumePercentText => $"{Math.Round(MediaVolume)}%";

    public string MediaMuteButtonGlyph => (IsMediaMuted || MediaVolume == 0) ? "\uE74F" : "\uE767";

    public string MediaMuteButtonTooltip => IsMediaMuted ? "Unmute media audio" : "Mute media audio";

    [RelayCommand]
    public void ToggleMediaMute()
    {
        _presentationCoordinator.MediaPresentationService.ToggleMute();
        OnPropertyChanged(nameof(IsMediaMuted));
        OnPropertyChanged(nameof(MediaMuteButtonGlyph));
        OnPropertyChanged(nameof(MediaMuteButtonTooltip));
    }

    public string VideoPositionText
    {
        get
        {
            var pos = _presentationCoordinator.MediaPresentationService.Position;
            var dur = _presentationCoordinator.MediaPresentationService.Duration;
            if (dur == TimeSpan.Zero && _presentationCoordinator.CurrentPresentationSource is VideoMediaSource v && v.Duration.HasValue)
            {
                dur = v.Duration.Value;
            }

            return $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
    }

    public string PresentationOutputStatusText => IsOutputWindowOpen ? "Window Open" : "Window Closed";

    public string PresentationButtonText => HasActivePresentation ? "Stop Presenting" : "Start Presenting";

    public string PresentationButtonGlyph => HasActivePresentation ? "\uE71A" : "\uE768";

    public string PauseButtonText => IsPresentationPaused ? "Resume Stream" : "Pause Stream";

    public string BlackoutButtonText => IsPresentationBlackout ? "End Blackout" : "Blackout";

    public Visibility LivePreviewVisibility => IsCapturing ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StartingCaptureVisibility => IsStartingCapture ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyWorkspaceVisibility => (IsCaptureIdle || CaptureState == CaptureState.Failed) && !HasSelectedSources
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility ReadyToPreviewVisibility => (IsCaptureIdle || CaptureState == CaptureState.Failed) && HasSelectedSources
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility HasSelectedSourcesVisibility => HasSelectedSources ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoSelectedSourcesVisibility => HasSelectedSources ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Opens or activates the floating presenter dock window.
    /// </summary>
    [RelayCommand]
    public void ShowPresenterDock()
    {
        _dockService?.ShowDock();
    }

    /// <summary>
    /// Toggles the floating presenter dock window open or closed.
    /// </summary>
    [RelayCommand]
    public void TogglePresenterDock()
    {
        _dockService?.ToggleDock();
    }

    /// <summary>
    /// Starts live capture preview on the selected source.
    /// </summary>
    [RelayCommand]
    public async Task StartPreviewAsync()
    {
        var targetSource = SelectedPreviewSource ?? SelectedSources.FirstOrDefault(s => s.IsAvailable);
        if (targetSource is null)
        {
            return;
        }

        if (targetSource.Type == SourceType.Image || targetSource.Type == SourceType.Video)
        {
            // Direct media preview is ready to present
            return;
        }

        try
        {
            await _captureCoordinator.StartPreviewAsync(targetSource).ConfigureAwait(false);
        }
        catch
        {
            // Error state bound via CaptureErrorMessage
        }
    }

    /// <summary>
    /// Stops the live capture preview.
    /// </summary>
    [RelayCommand]
    public async Task StopPreviewAsync()
    {
        await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Switches live preview to a specific queued presentation source.
    /// </summary>
    [RelayCommand]
    public async Task SwitchPreviewSourceAsync(CaptureSource? newSource)
    {
        if (newSource is null || !newSource.IsAvailable)
        {
            return;
        }

        if (SelectedPreviewSource?.Id != newSource.Id)
        {
            RunOnUIThread(() => SelectedPreviewSource = newSource);
            return;
        }

        if (IsCapturing && (newSource.Type == SourceType.Window || newSource.Type == SourceType.Display))
        {
            try
            {
                await _captureCoordinator.SwitchPreviewSourceAsync(newSource).ConfigureAwait(false);
            }
            catch
            {
                // Error state bound via CaptureErrorMessage
            }
        }
    }

    /// <summary>
    /// Opens or focuses the dedicated presentation output window.
    /// </summary>
    [RelayCommand]
    public async Task OpenPresentationWindowAsync()
    {
        await _presentationCoordinator.OpenOutputWindowAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the dedicated presentation output window.
    /// </summary>
    [RelayCommand]
    public async Task ClosePresentationWindowAsync()
    {
        await _presentationCoordinator.CloseOutputWindowAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the authoritative target source for starting presentation according to policy:
    /// 1. An explicitly selected, valid queued target (in SelectedSources and available).
    /// 2. The first valid queued source (SelectedSources.FirstOrDefault(s => s.IsAvailable)).
    /// </summary>
    public CaptureSource? ResolveAuthoritativeStartSource()
    {
        if (SelectedPresentationSource is not null &&
            SelectedPresentationSource.IsAvailable &&
            SelectedSources.Any(s => s.Id == SelectedPresentationSource.Id))
        {
            return SelectedSources.First(s => s.Id == SelectedPresentationSource.Id);
        }

        return SelectedSources.FirstOrDefault(s => s.IsAvailable);
    }

    /// <summary>
    /// Starts presenting to the dedicated Presentation Output Window.
    /// </summary>
    [RelayCommand]
    public async Task StartPresentationAsync()
    {
        if (IsStarting || HasActivePresentation)
        {
            return;
        }

        var targetSource = ResolveAuthoritativeStartSource();
        if (targetSource is null)
        {
            return;
        }

        // Align selected source with the authoritative target being started
        RunOnUIThread(() =>
        {
            _isSyncingPresentationSource = true;
            try
            {
                SelectedPresentationSource = targetSource;
            }
            finally
            {
                _isSyncingPresentationSource = false;
            }
        });

        try
        {
            await _presentationCoordinator.StartPresentationAsync(targetSource).ConfigureAwait(false);

            if (_settingsService?.CurrentSettings.AutoOpenPresenterDock == true)
            {
                _dockService?.ShowDock();
            }
        }
        catch
        {
            // Error state bound via CaptureErrorMessage
        }
    }

    /// <summary>
    /// Stops presenting to the dedicated Presentation Output Window.
    /// </summary>
    [RelayCommand]
    public async Task StopPresentationAsync()
    {
        try
        {
            await _presentationCoordinator.StopPresentationAsync().ConfigureAwait(false);
        }
        catch
        {
            // Error state bound via CaptureErrorMessage
        }
    }

    /// <summary>
    /// Toggles presenting on/off.
    /// </summary>
    [RelayCommand]
    public async Task TogglePresentationAsync()
    {
        if (HasActivePresentation)
        {
            await StopPresentationAsync().ConfigureAwait(false);
        }
        else
        {
            await StartPresentationAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pauses presentation output, freezing the current frame or pausing video.
    /// </summary>
    [RelayCommand]
    public async Task PausePresentationAsync()
    {
        await _presentationCoordinator.PausePresentationAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes live presentation output.
    /// </summary>
    [RelayCommand]
    public async Task ResumePresentationAsync()
    {
        await _presentationCoordinator.ResumePresentationAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles between Paused and Live presentation states.
    /// </summary>
    [RelayCommand]
    public async Task TogglePauseAsync()
    {
        if (IsPresentationPaused)
        {
            await ResumePresentationAsync().ConfigureAwait(false);
        }
        else if (IsPresenting)
        {
            await PausePresentationAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Toggles presentation blackout mode.
    /// </summary>
    [RelayCommand]
    public async Task ToggleBlackoutAsync()
    {
        await _presentationCoordinator.ToggleBlackoutAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Restarts active video playback from the beginning.
    /// </summary>
    [RelayCommand]
    public void RestartVideo()
    {
        _presentationCoordinator.MediaPresentationService.RestartVideo();
    }

    /// <summary>
    /// Toggles video looping state.
    /// </summary>
    [RelayCommand]
    public void ToggleVideoLoop()
    {
        _presentationCoordinator.MediaPresentationService.ToggleLoop();
    }

    /// <summary>
    /// Toggles video playback (play / pause).
    /// </summary>
    [RelayCommand]
    public void ToggleVideoPlayback()
    {
        if (IsVideoPlaying)
        {
            _presentationCoordinator.MediaPresentationService.PauseVideo();
        }
        else
        {
            _presentationCoordinator.MediaPresentationService.ResumeVideo();
        }
    }

    /// <summary>
    /// Switches live presentation source on the fly without closing the output window.
    /// </summary>
    [RelayCommand]
    public async Task SwitchPresentationSourceAsync(CaptureSource? newSource)
    {
        if (newSource is null || !newSource.IsAvailable)
        {
            return;
        }

        if (SelectedPresentationSource?.Id != newSource.Id)
        {
            RunOnUIThread(() =>
            {
                _isSyncingPresentationSource = true;
                try
                {
                    SelectedPresentationSource = newSource;
                }
                finally
                {
                    _isSyncingPresentationSource = false;
                }
            });
        }

        if (HasActivePresentation)
        {
            try
            {
                await _presentationCoordinator.SwitchPresentationSourceAsync(newSource).ConfigureAwait(false);
            }
            catch
            {
                // Error state bound via CaptureErrorMessage
            }
        }
    }

    /// <summary>
    /// Navigates the shell to the Sources management page.
    /// </summary>
    [RelayCommand]
    private void NavigateToSources()
    {
        _navigationService.NavigateTo(typeof(Views.SourcesPage));
    }

    partial void OnSelectedPreviewSourceChanged(CaptureSource? value)
    {
        if (IsCapturing && value is not null && value.IsAvailable && (value.Type == SourceType.Window || value.Type == SourceType.Display) && value.Id != _captureCoordinator.CurrentPreviewSource?.Id)
        {
            _ = SwitchPreviewSourceAsync(value);
        }
    }

    partial void OnSelectedPresentationSourceChanged(CaptureSource? value)
    {
        if (_isSyncingPresentationSource)
        {
            return;
        }

        // Guard against spurious null push from ComboBox when items change/toggle visibility
        if (value is null && HasSelectedSources && !HasActivePresentation)
        {
            var fallback = SelectedSources.FirstOrDefault(s => s.IsAvailable);
            if (fallback is not null && SelectedPresentationSource != fallback)
            {
                _isSyncingPresentationSource = true;
                try
                {
                    SelectedPresentationSource = fallback;
                }
                finally
                {
                    _isSyncingPresentationSource = false;
                }
                return;
            }
        }

        if (HasActivePresentation && value is not null && value.IsAvailable && value.Id != _presentationCoordinator.CurrentPresentationSource?.Id)
        {
            _ = SwitchPresentationSourceAsync(value);
        }
    }

    private void NotifyMediaPlaybackProperties()
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IsActiveSourceVideo));
            OnPropertyChanged(nameof(IsActiveSourceImage));
            OnPropertyChanged(nameof(IsVideoPlaying));
            OnPropertyChanged(nameof(IsVideoPaused));
            OnPropertyChanged(nameof(IsVideoEnded));
            OnPropertyChanged(nameof(IsVideoLooping));
            OnPropertyChanged(nameof(VideoPlaybackButtonGlyph));
            OnPropertyChanged(nameof(VideoPlaybackButtonText));
            OnPropertyChanged(nameof(VideoLoopButtonText));
            OnPropertyChanged(nameof(VideoPositionText));
            OnPropertyChanged(nameof(VideoControlsVisibility));
            OnPropertyChanged(nameof(MediaVolume));
            OnPropertyChanged(nameof(IsMediaMuted));
            OnPropertyChanged(nameof(MediaVolumePercentText));
            OnPropertyChanged(nameof(MediaMuteButtonGlyph));
            OnPropertyChanged(nameof(MediaMuteButtonTooltip));
        });
    }

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status))
        {
            RunOnUIThread(() =>
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusDisplayText));
                OnPropertyChanged(nameof(IsStarting));
                OnPropertyChanged(nameof(IsPresenting));
                OnPropertyChanged(nameof(IsPresentationPaused));
                OnPropertyChanged(nameof(IsPresentationBlackout));
                OnPropertyChanged(nameof(HasActivePresentation));
                OnPropertyChanged(nameof(PresentationButtonText));
                OnPropertyChanged(nameof(PresentationButtonGlyph));
                OnPropertyChanged(nameof(PauseButtonText));
                OnPropertyChanged(nameof(BlackoutButtonText));
                OnPropertyChanged(nameof(CanStartPresentation));
                OnPropertyChanged(nameof(StartPresentingTooltip));
                NotifyMediaPlaybackProperties();
            });
        }
        else if (e.PropertyName == nameof(IPresentationStateService.ActiveSource))
        {
            RunOnUIThread(() =>
            {
                if (_presentationStateService.ActiveSource is not null &&
                    SelectedPresentationSource?.Id != _presentationStateService.ActiveSource.Id)
                {
                    _isSyncingPresentationSource = true;
                    try
                    {
                        SelectedPresentationSource = _presentationStateService.ActiveSource;
                    }
                    finally
                    {
                        _isSyncingPresentationSource = false;
                    }
                }

                OnPropertyChanged(nameof(ActiveSourceTitle));
                OnPropertyChanged(nameof(HasActiveSource));
                NotifyMediaPlaybackProperties();
            });
        }
        else if (e.PropertyName == nameof(IPresentationStateService.SelectedSourceCount) ||
                 e.PropertyName == nameof(IPresentationStateService.SelectedSources))
        {
            RunOnUIThread(() =>
            {
                if (SelectedPreviewSource is null || !SelectedSources.Any(s => s.Id == SelectedPreviewSource.Id))
                {
                    SelectedPreviewSource = SelectedSources.FirstOrDefault(s => s.IsAvailable);
                }

                if (!HasActivePresentation)
                {
                    if (SelectedPresentationSource is null || !SelectedSources.Any(s => s.Id == SelectedPresentationSource.Id))
                    {
                        _isSyncingPresentationSource = true;
                        try
                        {
                            SelectedPresentationSource = SelectedSources.FirstOrDefault(s => s.IsAvailable);
                        }
                        finally
                        {
                            _isSyncingPresentationSource = false;
                        }
                    }
                }
                else
                {
                    // While presenting, keep SelectedPresentationSource aligned with CurrentPresentationSource
                    // so unqueueing the active source does NOT trigger an unintended live switch.
                    if (_presentationCoordinator.CurrentPresentationSource is not null &&
                        SelectedPresentationSource?.Id != _presentationCoordinator.CurrentPresentationSource.Id)
                    {
                        _isSyncingPresentationSource = true;
                        try
                        {
                            SelectedPresentationSource = _presentationCoordinator.CurrentPresentationSource;
                        }
                        finally
                        {
                            _isSyncingPresentationSource = false;
                        }
                    }
                }

                OnPropertyChanged(nameof(SelectedSourceCount));
                OnPropertyChanged(nameof(HasSelectedSources));
                OnPropertyChanged(nameof(HasNoSelectedSources));
                OnPropertyChanged(nameof(SourceSelectorPlaceholderText));
                OnPropertyChanged(nameof(CanStartPresentation));
                OnPropertyChanged(nameof(StartPresentingTooltip));
                OnPropertyChanged(nameof(HasSelectedSourcesVisibility));
                OnPropertyChanged(nameof(NoSelectedSourcesVisibility));
                OnPropertyChanged(nameof(SelectedSources));
                OnPropertyChanged(nameof(EmptyWorkspaceVisibility));
                OnPropertyChanged(nameof(ReadyToPreviewVisibility));
            });
        }
    }

    private void OnCaptureCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(CaptureState));
            OnPropertyChanged(nameof(IsCapturing));
            OnPropertyChanged(nameof(IsStartingCapture));
            OnPropertyChanged(nameof(IsCaptureIdle));
            OnPropertyChanged(nameof(HasCaptureError));
            OnPropertyChanged(nameof(CaptureErrorMessage));
            OnPropertyChanged(nameof(ActiveSourceTitle));
            OnPropertyChanged(nameof(HasActiveSource));
            OnPropertyChanged(nameof(StatusDisplayText));
            OnPropertyChanged(nameof(PreviewImageSource));
            OnPropertyChanged(nameof(LivePreviewVisibility));
            OnPropertyChanged(nameof(StartingCaptureVisibility));
            OnPropertyChanged(nameof(EmptyWorkspaceVisibility));
            OnPropertyChanged(nameof(ReadyToPreviewVisibility));
        });
    }

    private void OnPresentationCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IsOutputWindowOpen));
            OnPropertyChanged(nameof(PresentationOutputStatusText));
            OnPropertyChanged(nameof(IsStarting));
            OnPropertyChanged(nameof(IsPresenting));
            OnPropertyChanged(nameof(IsPresentationPaused));
            OnPropertyChanged(nameof(IsPresentationBlackout));
            OnPropertyChanged(nameof(HasActivePresentation));
            OnPropertyChanged(nameof(PresentationButtonText));
            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(BlackoutButtonText));
            OnPropertyChanged(nameof(CanStartPresentation));
            OnPropertyChanged(nameof(StartPresentingTooltip));
            OnPropertyChanged(nameof(HasCaptureError));
            OnPropertyChanged(nameof(CaptureErrorMessage));
            OnPropertyChanged(nameof(ActiveSourceTitle));
            OnPropertyChanged(nameof(StatusDisplayText));
            NotifyMediaPlaybackProperties();
        });
    }

    #region Background Music Properties & Commands

    public IBackgroundMusicService? BackgroundMusicService => _backgroundMusicService;

    public IReadOnlyList<MusicPlaylist> Playlists => _backgroundMusicService?.Playlists ?? [];

    public MusicPlaylist? ActivePlaylist
    {
        get => _backgroundMusicService?.CurrentPlaylist;
        set
        {
            if (value is not null && _backgroundMusicService is not null && _backgroundMusicService.CurrentPlaylist?.Id != value.Id)
            {
                _backgroundMusicService.SetActivePlaylist(value.Id);
                OnPropertyChanged(nameof(ActivePlaylist));
                OnPropertyChanged(nameof(PlaylistTracks));
                OnPropertyChanged(nameof(HasCurrentPlaylistTracks));
                OnPropertyChanged(nameof(CurrentMusicTrack));
                OnPropertyChanged(nameof(MusicTrackTitle));
            }
        }
    }

    public ObservableCollection<MusicTrack>? PlaylistTracks => ActivePlaylist?.Tracks;

    public bool HasPlaylists => Playlists.Count > 0;

    public bool HasCurrentPlaylistTracks => (PlaylistTracks?.Count ?? 0) > 0;

    public Microsoft.UI.Xaml.Visibility EmptyPlaylistVisibility => HasCurrentPlaylistTracks
        ? Microsoft.UI.Xaml.Visibility.Collapsed
        : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Visibility PlaylistTracksVisibility => HasCurrentPlaylistTracks
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    [ObservableProperty]
    private MusicTrack? _selectedMusicTrack;

    [ObservableProperty]
    private string _newPlaylistName = string.Empty;

    public MusicTrack? CurrentMusicTrack => _backgroundMusicService?.CurrentTrack;

    public string MusicTrackTitle => CurrentMusicTrack is not null
        ? CurrentMusicTrack.DisplayText
        : (ActivePlaylist is not null ? $"Playlist: {ActivePlaylist.Name}" : "No track playing");

    public bool IsMusicPlaying => _backgroundMusicService?.IsPlaying ?? false;

    public bool IsMusicPaused => _backgroundMusicService?.IsPaused ?? false;

    public string MusicPlayPauseGlyph => IsMusicPlaying ? "\uE769" : "\uE768";

    public string MusicPlayPauseTooltip => IsMusicPlaying ? "Pause Background Music" : "Play Background Music";

    public double MusicVolume
    {
        get => (_backgroundMusicService?.Volume ?? 0.5) * 100.0;
        set
        {
            if (_backgroundMusicService is not null)
            {
                _backgroundMusicService.Volume = Math.Clamp(value / 100.0, 0.0, 1.0);
                OnPropertyChanged(nameof(MusicVolume));
                OnPropertyChanged(nameof(MusicVolumePercentText));
                OnPropertyChanged(nameof(MusicMuteButtonGlyph));
            }
        }
    }

    public bool IsMusicMuted => _backgroundMusicService?.IsMuted ?? false;

    public string MusicVolumePercentText => $"{Math.Round(MusicVolume)}%";

    public string MusicMuteButtonGlyph => (IsMusicMuted || MusicVolume == 0) ? "\uE74F" : "\uE767";

    public string MusicMuteButtonTooltip => IsMusicMuted ? "Unmute Background Music" : "Mute Background Music";

    public MusicRepeatMode MusicRepeatMode => _backgroundMusicService?.RepeatMode ?? MusicRepeatMode.RepeatAll;

    public string MusicRepeatGlyph => MusicRepeatMode switch
    {
        MusicRepeatMode.RepeatOne => "\uE8ED",
        MusicRepeatMode.RepeatAll => "\uE8EE",
        _ => "\uF5E7"
    };

    public string MusicRepeatTooltip => MusicRepeatMode switch
    {
        MusicRepeatMode.RepeatOne => "Repeat: One Track",
        MusicRepeatMode.RepeatAll => "Repeat: All Tracks",
        _ => "Repeat: Off"
    };

    public double MusicRepeatOpacity => MusicRepeatMode != MusicRepeatMode.RepeatOff ? 1.0 : 0.4;

    public bool IsMusicShuffleEnabled => _backgroundMusicService?.IsShuffleEnabled ?? false;

    public double MusicShuffleOpacity => IsMusicShuffleEnabled ? 1.0 : 0.4;

    public string MusicShuffleTooltip => IsMusicShuffleEnabled ? "Shuffle: On" : "Shuffle: Off";

    public double MusicPositionSeconds
    {
        get => _backgroundMusicService?.Position.TotalSeconds ?? 0;
        set
        {
            _backgroundMusicService?.Seek(TimeSpan.FromSeconds(value));
            OnPropertyChanged(nameof(MusicPositionSeconds));
            OnPropertyChanged(nameof(MusicPositionText));
        }
    }

    public double MusicDurationSeconds => _backgroundMusicService?.Duration.TotalSeconds ?? 0;

    public string MusicPositionText
    {
        get
        {
            var pos = _backgroundMusicService?.Position ?? TimeSpan.Zero;
            var dur = _backgroundMusicService?.Duration ?? TimeSpan.Zero;
            return $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
    }

    private void OnBackgroundMusicStateChanged(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(IsMusicPlaying));
            OnPropertyChanged(nameof(IsMusicPaused));
            OnPropertyChanged(nameof(MusicPlayPauseGlyph));
            OnPropertyChanged(nameof(MusicPlayPauseTooltip));
            OnPropertyChanged(nameof(MusicPositionText));
            OnPropertyChanged(nameof(MusicPositionSeconds));
            OnPropertyChanged(nameof(MusicDurationSeconds));
        });
    }

    private void OnBackgroundMusicTrackChanged(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(CurrentMusicTrack));
            OnPropertyChanged(nameof(MusicTrackTitle));
            OnPropertyChanged(nameof(MusicDurationSeconds));
            OnPropertyChanged(nameof(MusicPositionSeconds));
            OnPropertyChanged(nameof(MusicPositionText));
        });
    }

    private void OnBackgroundMusicPlaylistChanged(object? sender, EventArgs e)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(Playlists));
            OnPropertyChanged(nameof(ActivePlaylist));
            OnPropertyChanged(nameof(PlaylistTracks));
            OnPropertyChanged(nameof(HasPlaylists));
            OnPropertyChanged(nameof(HasCurrentPlaylistTracks));
            OnPropertyChanged(nameof(EmptyPlaylistVisibility));
            OnPropertyChanged(nameof(PlaylistTracksVisibility));
            OnPropertyChanged(nameof(CurrentMusicTrack));
            OnPropertyChanged(nameof(MusicTrackTitle));
        });
    }

    private void OnBackgroundMusicPositionChanged(object? sender, TimeSpan pos)
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(MusicPositionSeconds));
            OnPropertyChanged(nameof(MusicPositionText));
        });
    }

    [RelayCommand]
    public void ToggleMusicPlayPause()
    {
        _backgroundMusicService?.TogglePlayPause();
    }

    [RelayCommand]
    public async Task PlayMusicTrack(MusicTrack? track)
    {
        if (track is not null && _backgroundMusicService is not null)
        {
            await _backgroundMusicService.PlayTrackAsync(track);
        }
    }

    [RelayCommand]
    public async Task NextMusicTrack()
    {
        if (_backgroundMusicService is not null)
        {
            await _backgroundMusicService.NextTrackAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousMusicTrack()
    {
        if (_backgroundMusicService is not null)
        {
            await _backgroundMusicService.PreviousTrackAsync();
        }
    }

    [RelayCommand]
    public void StopMusic()
    {
        _backgroundMusicService?.Stop();
    }

    [RelayCommand]
    public void ToggleMusicMute()
    {
        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.IsMuted = !_backgroundMusicService.IsMuted;
            OnPropertyChanged(nameof(IsMusicMuted));
            OnPropertyChanged(nameof(MusicMuteButtonGlyph));
            OnPropertyChanged(nameof(MusicMuteButtonTooltip));
        }
    }

    [RelayCommand]
    public void CycleMusicRepeatMode()
    {
        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.RepeatMode = _backgroundMusicService.RepeatMode switch
            {
                MusicRepeatMode.RepeatOff => MusicRepeatMode.RepeatOne,
                MusicRepeatMode.RepeatOne => MusicRepeatMode.RepeatAll,
                _ => MusicRepeatMode.RepeatOff
            };
            OnPropertyChanged(nameof(MusicRepeatMode));
            OnPropertyChanged(nameof(MusicRepeatGlyph));
            OnPropertyChanged(nameof(MusicRepeatTooltip));
            OnPropertyChanged(nameof(MusicRepeatOpacity));
        }
    }

    [RelayCommand]
    public void ToggleMusicShuffle()
    {
        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.IsShuffleEnabled = !_backgroundMusicService.IsShuffleEnabled;
            OnPropertyChanged(nameof(IsMusicShuffleEnabled));
            OnPropertyChanged(nameof(MusicShuffleOpacity));
            OnPropertyChanged(nameof(MusicShuffleTooltip));
        }
    }

    [RelayCommand]
    public void CreateNewPlaylist()
    {
        if (_backgroundMusicService is null)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(NewPlaylistName)
            ? $"Playlist {_backgroundMusicService.Playlists.Count + 1}"
            : NewPlaylistName.Trim();

        var created = _backgroundMusicService.CreatePlaylist(name);
        NewPlaylistName = string.Empty;
        ActivePlaylist = created;
    }

    [RelayCommand]
    public void DeleteCurrentPlaylist()
    {
        if (_backgroundMusicService is null || ActivePlaylist is null)
        {
            return;
        }

        _backgroundMusicService.DeletePlaylist(ActivePlaylist.Id);
        ActivePlaylist = _backgroundMusicService.CurrentPlaylist;
    }

    [RelayCommand]
    public async Task ImportAudioFiles()
    {
        if (_backgroundMusicService is null || ActivePlaylist is null || _mediaPickerService is null)
        {
            return;
        }

        var files = await _mediaPickerService.PickAudioFilesAsync();
        if (files.Count > 0)
        {
            await _backgroundMusicService.AddTracksAsync(ActivePlaylist.Id, files);
        }
    }

    [RelayCommand]
    public void RemoveMusicTrack(MusicTrack? track)
    {
        if (_backgroundMusicService is null || ActivePlaylist is null || track is null)
        {
            return;
        }

        _backgroundMusicService.RemoveTrack(ActivePlaylist.Id, track.Id);
    }

    [RelayCommand]
    public void MoveMusicTrackUp(MusicTrack? track)
    {
        if (_backgroundMusicService is null || ActivePlaylist is null || track is null)
        {
            return;
        }

        int index = ActivePlaylist.Tracks.IndexOf(track);
        if (index > 0)
        {
            _backgroundMusicService.MoveTrack(ActivePlaylist.Id, index, index - 1);
        }
    }

    [RelayCommand]
    public void MoveMusicTrackDown(MusicTrack? track)
    {
        if (_backgroundMusicService is null || ActivePlaylist is null || track is null)
        {
            return;
        }

        int index = ActivePlaylist.Tracks.IndexOf(track);
        if (index >= 0 && index < ActivePlaylist.Tracks.Count - 1)
        {
            _backgroundMusicService.MoveTrack(ActivePlaylist.Id, index, index + 1);
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presentationStateService.PropertyChanged -= OnPresentationStatePropertyChanged;
        _captureCoordinator.PropertyChanged -= OnCaptureCoordinatorPropertyChanged;
        _presentationCoordinator.PropertyChanged -= OnPresentationCoordinatorPropertyChanged;

        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.PlaybackStateChanged -= OnBackgroundMusicStateChanged;
            _backgroundMusicService.TrackChanged -= OnBackgroundMusicTrackChanged;
            _backgroundMusicService.PlaylistChanged -= OnBackgroundMusicPlaylistChanged;
            _backgroundMusicService.PositionChanged -= OnBackgroundMusicPositionChanged;
        }
    }
}
