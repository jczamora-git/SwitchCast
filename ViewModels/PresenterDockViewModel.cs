using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the compact floating presenter companion dock window and three-mode source switching.
/// </summary>
public partial class PresenterDockViewModel : ObservableObject, IDisposable
{
    private readonly IPresentationCoordinator _presentationCoordinator;
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly IPresenterDockService _dockService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IApplicationLifecycleService? _lifecycleService;
    private readonly Services.Music.IBackgroundMusicService? _backgroundMusicService;
    private readonly INavigationService? _navigationService;
    private readonly IWindowActivationService? _activationService;
    private DispatcherQueue? _dispatcherQueue;
    private IUIThreadDispatcher? _dispatcher;
    private DispatcherQueueTimer? _playbackTimer;
    private bool _isScrubbing;
    private double _scrubbingPositionSeconds;
    private bool _isMusicScrubbing;
    private double _musicScrubbingPositionSeconds;
    private bool _disposed;

    public PresenterDockViewModel(
        IPresentationCoordinator presentationCoordinator,
        IPresentationStateService presentationStateService,
        IPresentationWindowService presentationWindowService,
        IPresenterDockService dockService,
        IApplicationSettingsService settingsService,
        IApplicationLifecycleService? lifecycleService = null,
        IUIThreadDispatcher? uiDispatcher = null,
        Services.Music.IBackgroundMusicService? backgroundMusicService = null,
        INavigationService? navigationService = null,
        IWindowActivationService? activationService = null)
    {
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _lifecycleService = lifecycleService;
        _backgroundMusicService = backgroundMusicService ?? _presentationCoordinator.BackgroundMusicService;
        _navigationService = navigationService;
        _activationService = activationService;

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _dispatcher = UIThreadDispatcherProvider.Resolve(uiDispatcher);

        _presentationStateService.PropertyChanged += OnStatePropertyChanged;
        _presentationCoordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        _presentationWindowService.DisplayModeChanged += OnWindowDisplayModeChanged;
        _presentationWindowService.WindowOpened += OnWindowOpened;
        _presentationWindowService.WindowClosed += OnWindowClosed;

        if (_presentationCoordinator.MediaPresentationService is not null)
        {
            _presentationCoordinator.MediaPresentationService.MediaStateChanged += OnMediaStateChanged;
        }

        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.PlaybackStateChanged += OnBackgroundMusicPlaybackStateChanged;
            _backgroundMusicService.TrackChanged += OnBackgroundMusicTrackChanged;
            _backgroundMusicService.PositionChanged += OnBackgroundMusicPositionChanged;
            _backgroundMusicService.PlaylistChanged += OnBackgroundMusicPlaylistChanged;
        }

        InitializePlaybackTimer();
    }

    /// <summary>
    /// Configures the authoritative UI thread DispatcherQueue for this ViewModel.
    /// </summary>
    public void SetDispatcherQueue(DispatcherQueue? dispatcherQueue)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcherQueue = dispatcherQueue;
        _dispatcher = dispatcherQueue is not null ? new DispatcherQueueAdapter(dispatcherQueue) : null;
        InitializePlaybackTimer();
    }

    /// <summary>
    /// Configures a testable UI thread dispatcher for this ViewModel.
    /// </summary>
    public void SetUIThreadDispatcher(IUIThreadDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private void InitializePlaybackTimer()
    {
        StopPlaybackTimer();

        if (_disposed || _dispatcherQueue is null)
        {
            return;
        }

        try
        {
            _playbackTimer = _dispatcherQueue.CreateTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(250);
            _playbackTimer.IsRepeating = true;
            _playbackTimer.Tick += OnPlaybackTimerTick;
            _playbackTimer.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresenterDockViewModel] Failed to create DispatcherQueueTimer: {ex.Message}");
            _playbackTimer = null;
        }
    }

    private void StopPlaybackTimer()
    {
        if (_playbackTimer is not null)
        {
            try
            {
                _playbackTimer.Stop();
                _playbackTimer.Tick -= OnPlaybackTimerTick;
            }
            catch
            {
                // Ignore cleanup errors
            }
            _playbackTimer = null;
        }
    }

    private void OnPlaybackTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed)
        {
            return;
        }

        Debug.Assert(_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess, "Playback timer tick must execute on UI thread.");

        if (IsActiveSourceVideo && IsVideoPlaying && !_isScrubbing)
        {
            OnPropertyChanged(nameof(VideoPositionSeconds));
            OnPropertyChanged(nameof(VideoPositionText));
        }
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
            bool enqueued = _dispatcher.TryEnqueue(() =>
            {
                if (!_disposed)
                {
                    action();
                }
            });

            if (!enqueued)
            {
                Debug.WriteLine("[PresenterDockViewModel] RunOnUIThread: TryEnqueue returned false. Dropping UI update.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopPlaybackTimer();

        _presentationStateService.PropertyChanged -= OnStatePropertyChanged;
        _presentationCoordinator.PropertyChanged -= OnCoordinatorPropertyChanged;
        _presentationWindowService.DisplayModeChanged -= OnWindowDisplayModeChanged;
        _presentationWindowService.WindowOpened -= OnWindowOpened;
        _presentationWindowService.WindowClosed -= OnWindowClosed;

        if (_presentationCoordinator.MediaPresentationService is not null)
        {
            _presentationCoordinator.MediaPresentationService.MediaStateChanged -= OnMediaStateChanged;
        }

        if (_backgroundMusicService is not null)
        {
            _backgroundMusicService.PlaybackStateChanged -= OnBackgroundMusicPlaybackStateChanged;
            _backgroundMusicService.TrackChanged -= OnBackgroundMusicTrackChanged;
            _backgroundMusicService.PositionChanged -= OnBackgroundMusicPositionChanged;
            _backgroundMusicService.PlaylistChanged -= OnBackgroundMusicPlaylistChanged;
        }
    }

    private void OnWindowOpened(object? sender, EventArgs e) => RunOnUIThread(NotifyFullscreenProperties);

    private void OnWindowClosed(object? sender, EventArgs e) => RunOnUIThread(NotifyFullscreenProperties);

    private void OnMediaStateChanged(object? sender, EventArgs e) => RunOnUIThread(NotifyMediaProperties);

    public PresentationStatus Status => _presentationStateService.Status;

    public PresenterSwitchMode SwitchMode => _presentationStateService.SwitchMode;

    public string SwitchModeBadge => SwitchMode switch
    {
        PresenterSwitchMode.ActiveAndLive => "A+L",
        PresenterSwitchMode.ActiveOnly => "A",
        _ => "L"
    };

    public string SwitchModeTooltip => SwitchMode switch
    {
        PresenterSwitchMode.ActiveAndLive => "Switching Mode: Active + Live (Focus window & switch audience presentation)",
        PresenterSwitchMode.ActiveOnly => "Switching Mode: Active Only (Focus window without changing audience presentation)",
        _ => "Switching Mode: Live Only (Switch audience presentation without changing window focus)"
    };

    public bool IsModeActiveAndLive => SwitchMode == PresenterSwitchMode.ActiveAndLive;

    public bool IsModeActiveOnly => SwitchMode == PresenterSwitchMode.ActiveOnly;

    public bool IsModeLiveOnly => SwitchMode == PresenterSwitchMode.LiveOnly;

    public string StatusDisplayText => Status switch
    {
        PresentationStatus.Active => "LIVE",
        PresentationStatus.Paused => "PAUSED",
        PresentationStatus.Blackout => "BLACKOUT",
        PresentationStatus.Starting => "STARTING...",
        PresentationStatus.Error => "ERROR",
        _ => "STANDBY"
    };

    public CaptureSource? SelectedSource =>
        _presentationStateService.SelectedSource ??
        _presentationCoordinator.CurrentPresentationSource ??
        _presentationStateService.ActiveSource;

    public string ActiveSourceTitle =>
        SelectedSource?.Title ??
        _presentationCoordinator.CurrentPresentationSource?.Title ??
        _presentationStateService.ActiveSource?.Title ??
        "No Active Source";

    public string ActiveSourceGlyph => SelectedSource?.TypeGlyph ?? "\uE7F4";

    public string OnAirSourceTitle =>
        _presentationCoordinator.CurrentPresentationSource?.Title ??
        _presentationStateService.ActiveSource?.Title ??
        "None";

    public string SourceFullTooltip =>
        $"Selected: {ActiveSourceTitle}\nOn-Air: {OnAirSourceTitle}\nMode: {SwitchModeBadge} ({SwitchMode})";

    public bool HasActiveSource =>
        SelectedSource is not null ||
        _presentationCoordinator.CurrentPresentationSource is not null ||
        _presentationStateService.ActiveSource is not null;

    public bool IsLive => _presentationCoordinator.IsLive;

    public bool IsPaused => _presentationCoordinator.IsPaused;

    public bool IsBlackout => _presentationCoordinator.IsBlackout;

    public bool IsPresenting => IsLive || IsPaused || IsBlackout;

    public bool IsActiveSourceVideo => _presentationCoordinator.IsActiveSourceVideo;

    public bool IsActiveSourcePowerPoint => _presentationCoordinator.IsActiveSourcePowerPoint;

    public bool IsActiveSourceCamera => _presentationCoordinator.IsActiveSourceCamera;

    public string PowerPointProgressText => _presentationCoordinator.PowerPointProgressText ?? "PowerPoint";

    public bool HasSecondaryRow => IsActiveSourceVideo || IsActiveSourcePowerPoint;

    public Visibility VideoPlaybackRowVisibility => IsActiveSourceVideo ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PowerPointRowVisibility => IsActiveSourcePowerPoint ? Visibility.Visible : Visibility.Collapsed;

    public bool IsMediaMuted => _presentationCoordinator.MediaPresentationService.IsMuted;

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

    public string MediaVolumePercentText => $"{Math.Round(MediaVolume)}%";

    public string MediaMuteButtonGlyph => (IsMediaMuted || MediaVolume == 0) ? "\uE74F" : "\uE767";

    public string MediaMuteButtonTooltip => IsMediaMuted ? "Unmute Video Audio" : "Mute Video Audio";

    [RelayCommand]
    public void ToggleMediaMute()
    {
        _presentationCoordinator.MediaPresentationService.ToggleMute();
        OnPropertyChanged(nameof(IsMediaMuted));
        OnPropertyChanged(nameof(MediaMuteButtonGlyph));
        OnPropertyChanged(nameof(MediaMuteButtonTooltip));
    }

    public IReadOnlyList<CaptureSource> SelectedSources => _presentationStateService.SelectedSources;

    public bool HasSelectedSources => _presentationStateService.SelectedSourceCount > 0;

    public bool CanSwitchSources => _presentationStateService.SelectedSourceCount > 1;

    public bool IsOutputWindowOpen => _presentationWindowService.IsWindowOpen;

    public bool IsFullscreen => _presentationWindowService.DisplayMode == PresentationDisplayMode.Fullscreen;

    public bool CanToggleFullscreen => IsOutputWindowOpen;

    public string FullscreenButtonGlyph => IsFullscreen ? "\uE73F" : "\uE740"; // Exit Fullscreen (contract) / Enter Fullscreen (expand)

    public string FullscreenButtonTooltip => IsFullscreen ? "Exit Fullscreen Presentation" : "Enter Fullscreen Presentation";

    [RelayCommand]
    public void ToggleFullscreen()
    {
        if (IsOutputWindowOpen)
        {
            _presentationWindowService.ToggleDisplayMode();
            NotifyFullscreenProperties();
        }
    }

    public string PauseButtonText => IsPaused ? "Resume" : "Pause";

    public string PauseButtonGlyph => IsPaused ? "\uE768" : "\uE769"; // Play / Pause

    public string BlackoutButtonText => IsBlackout ? "Restore" : "Blackout";

    public string BlackoutButtonGlyph => IsBlackout ? "\uE7B3" : "\uED1A"; // Eye / Closed Eye

    // ==========================================
    // VIDEO PLAYBACK CONTROLS & TIMELINE
    // ==========================================

    public bool IsVideoPlaying => _presentationCoordinator.MediaPresentationService.IsVideoPlaying;

    public bool IsVideoPaused => _presentationCoordinator.MediaPresentationService.IsVideoPaused;

    public bool IsVideoEnded => _presentationCoordinator.MediaPresentationService.IsVideoEnded;

    public string VideoPlaybackButtonGlyph => IsVideoPlaying ? "\uE769" : "\uE768"; // Pause / Play

    public string VideoPlaybackButtonTooltip => IsVideoPlaying ? "Pause Video" : (IsVideoEnded ? "Replay Video" : "Play Video");

    public double VideoDurationSeconds
    {
        get
        {
            var dur = _presentationCoordinator.MediaPresentationService.Duration;
            if (dur == TimeSpan.Zero && _presentationCoordinator.CurrentPresentationSource is VideoMediaSource v && v.Duration.HasValue)
            {
                dur = v.Duration.Value;
            }
            return Math.Max(0, dur.TotalSeconds);
        }
    }

    public double VideoPositionSeconds
    {
        get
        {
            if (_isScrubbing)
            {
                return _scrubbingPositionSeconds;
            }
            return _presentationCoordinator.MediaPresentationService.Position.TotalSeconds;
        }
        set
        {
            if (_isScrubbing)
            {
                _scrubbingPositionSeconds = value;
                OnPropertyChanged(nameof(VideoPositionSeconds));
                OnPropertyChanged(nameof(VideoPositionText));
            }
        }
    }

    public string VideoPositionText
    {
        get
        {
            var pos = _isScrubbing
                ? TimeSpan.FromSeconds(_scrubbingPositionSeconds)
                : _presentationCoordinator.MediaPresentationService.Position;

            var dur = _presentationCoordinator.MediaPresentationService.Duration;
            if (dur == TimeSpan.Zero && _presentationCoordinator.CurrentPresentationSource is VideoMediaSource v && v.Duration.HasValue)
            {
                dur = v.Duration.Value;
            }

            return $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
    }

    public void StartScrubbing(double currentSeconds)
    {
        _isScrubbing = true;
        _scrubbingPositionSeconds = currentSeconds;
    }

    public void CompleteScrubbing(double targetSeconds)
    {
        _isScrubbing = false;
        var targetTime = TimeSpan.FromSeconds(Math.Clamp(targetSeconds, 0, VideoDurationSeconds));
        _presentationCoordinator.MediaPresentationService.Seek(targetTime);
        OnPropertyChanged(nameof(VideoPositionSeconds));
        OnPropertyChanged(nameof(VideoPositionText));
    }

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
        NotifyMediaProperties();
    }

    [RelayCommand]
    public void RestartVideo()
    {
        _presentationCoordinator.MediaPresentationService.RestartVideo();
        NotifyMediaProperties();
    }

    [RelayCommand]
    public void SeekBackward10()
    {
        var current = _presentationCoordinator.MediaPresentationService.Position;
        var target = current - TimeSpan.FromSeconds(10);
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }
        _presentationCoordinator.MediaPresentationService.Seek(target);
        NotifyMediaProperties();
    }

    [RelayCommand]
    public void SeekForward10()
    {
        var current = _presentationCoordinator.MediaPresentationService.Position;
        var max = TimeSpan.FromSeconds(VideoDurationSeconds);
        var target = current + TimeSpan.FromSeconds(10);
        if (target > max && max > TimeSpan.Zero)
        {
            target = max;
        }
        _presentationCoordinator.MediaPresentationService.Seek(target);
        NotifyMediaProperties();
    }

    [RelayCommand]
    public async Task SetSwitchModeAsync(object? parameter)
    {
        PresenterSwitchMode targetMode;
        if (parameter is PresenterSwitchMode mode)
        {
            targetMode = mode;
        }
        else if (parameter is string modeStr && Enum.TryParse<PresenterSwitchMode>(modeStr, ignoreCase: true, out var parsed))
        {
            targetMode = parsed;
        }
        else
        {
            return;
        }

        _presentationStateService.SetSwitchMode(targetMode);
        _settingsService.CurrentSettings.SwitchMode = targetMode;
        await _settingsService.SaveSettingsAsync();

        NotifyModeProperties();
    }

    [RelayCommand]
    private async Task PowerPointNextSlideAsync()
    {
        try
        {
            await _presentationCoordinator.PowerPointNextSlideAsync();
            OnPropertyChanged(nameof(PowerPointProgressText));
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(PowerPointNextSlideAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task PowerPointPreviousSlideAsync()
    {
        try
        {
            await _presentationCoordinator.PowerPointPreviousSlideAsync();
            OnPropertyChanged(nameof(PowerPointProgressText));
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(PowerPointPreviousSlideAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task NextSourceAsync()
    {
        try
        {
            await _presentationCoordinator.SwitchToNextSourceAsync();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(NextSourceAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task PreviousSourceAsync()
    {
        try
        {
            await _presentationCoordinator.SwitchToPreviousSourceAsync();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(PreviousSourceAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task SwitchSourceAsync(CaptureSource? source)
    {
        if (source is not null && source.IsAvailable)
        {
            try
            {
                await _presentationCoordinator.ExecuteSourceSwitchAsync(source);
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(SwitchSourceAsync), ex, $"SourceId={source.Id}, Type={source.Type}, HResult=0x{ex.HResult:X8}");
            }
        }
    }

    [RelayCommand]
    private async Task TogglePauseAsync()
    {
        try
        {
            if (IsPaused)
            {
                await _presentationCoordinator.ResumePresentationAsync();
            }
            else if (IsLive)
            {
                await _presentationCoordinator.PausePresentationAsync();
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(TogglePauseAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task ToggleBlackoutAsync()
    {
        try
        {
            await _presentationCoordinator.ToggleBlackoutAsync();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(ToggleBlackoutAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private async Task StopPresentationAsync()
    {
        try
        {
            await _presentationCoordinator.StopPresentationAsync();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(StopPresentationAsync), ex, $"HResult=0x{ex.HResult:X8}");
        }
    }

    [RelayCommand]
    private void ShowOutputWindow()
    {
        _presentationWindowService.ShowPresentationWindow();
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        _dockService.ShowDashboard();
    }

    private int _isConfirmingClose;

    /// <summary>
    /// Gets whether a presentation session is currently active (running, starting, paused, blackout, or output window open).
    /// </summary>
    public bool HasActivePresentation =>
        _presentationStateService.Status != PresentationStatus.Idle ||
        _presentationCoordinator.IsLive ||
        _presentationCoordinator.IsPaused ||
        _presentationCoordinator.IsBlackout ||
        _presentationWindowService.IsWindowOpen ||
        _presentationCoordinator.CurrentPresentationSource is not null;

    public bool ShowPresentationCursor => _presentationCoordinator.ShowPresentationCursor;

    public string PresentationCursorGlyph => ShowPresentationCursor ? "\uE890" : "\uED1A";

    public string PresentationCursorTooltip => ShowPresentationCursor
        ? "Hide Presentation Cursor"
        : "Show Presentation Cursor";

    public double PresentationCursorOpacity => ShowPresentationCursor ? 1.0 : 0.4;

    [RelayCommand]
    public async Task TogglePresentationCursorAsync()
    {
        await _presentationCoordinator.SetShowPresentationCursorAsync(!ShowPresentationCursor);
        OnPropertyChanged(nameof(ShowPresentationCursor));
        OnPropertyChanged(nameof(PresentationCursorGlyph));
        OnPropertyChanged(nameof(PresentationCursorTooltip));
        OnPropertyChanged(nameof(PresentationCursorOpacity));
    }

    /// <summary>
    /// Optional confirmation handler for closing dock while a presentation is live.
    /// Returns true if the user confirmed "Stop & Close", or false if canceled.
    /// </summary>
    public Func<Task<bool>>? ConfirmCloseAsync { get; set; }

    [RelayCommand]
    public async Task CloseDockAsync()
    {
        if (_lifecycleService is not null && (_lifecycleService.IsShuttingDown || _lifecycleService.IsShutdownApproved))
        {
            _dockService.CloseDock();
            return;
        }

        if (Interlocked.CompareExchange(ref _isConfirmingClose, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (HasActivePresentation)
            {
                bool confirmed = ConfirmCloseAsync is not null
                    ? await ConfirmCloseAsync().ConfigureAwait(true)
                    : false;

                if (!confirmed)
                {
                    // Cancel: keep Dock open, presentation live, output unchanged
                    return;
                }

                // Stop & Close: stop presentation, wait for cleanup, close dock.
                // Note: StopPresentationAsync authoritatively activates MainWindow and navigates to Dashboard.
                // We must not trigger a redundant ShowDashboard call here to prevent duplicate window activation.
                await _presentationCoordinator.StopPresentationAsync().ConfigureAwait(true);
                _dockService.CloseDock();
            }
            else
            {
                // No active presentation: close immediately without confirmation
                _dockService.CloseDock();
                if (_lifecycleService is null || (!_lifecycleService.IsShuttingDown && !_lifecycleService.IsShutdownApproved))
                {
                    _dockService.ShowDashboard();
                }
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PresenterDockViewModel), nameof(CloseDockAsync), ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isConfirmingClose, 0);
        }
    }

    [RelayCommand]
    private void UnqueueSource(CaptureSource? source)
    {
        if (source is not null)
        {
            _presentationStateService.RemoveSelectedSource(source.Id);
        }
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status) ||
            e.PropertyName == nameof(IPresentationStateService.ActiveSource) ||
            e.PropertyName == nameof(IPresentationStateService.SelectedSources) ||
            e.PropertyName == nameof(IPresentationStateService.SwitchMode) ||
            e.PropertyName == nameof(IPresentationStateService.SelectedSource) ||
            e.PropertyName == nameof(IPresentationStateService.ForegroundSource))
        {
            RunOnUIThread(NotifyAllProperties);
        }
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RunOnUIThread(NotifyAllProperties);
    }

    private void OnWindowDisplayModeChanged(object? sender, PresentationDisplayMode mode)
    {
        RunOnUIThread(NotifyFullscreenProperties);
    }

    private void NotifyFullscreenProperties()
    {
        OnPropertyChanged(nameof(IsOutputWindowOpen));
        OnPropertyChanged(nameof(IsFullscreen));
        OnPropertyChanged(nameof(CanToggleFullscreen));
        OnPropertyChanged(nameof(FullscreenButtonGlyph));
        OnPropertyChanged(nameof(FullscreenButtonTooltip));
    }

    private void NotifyModeProperties()
    {
        OnPropertyChanged(nameof(SwitchMode));
        OnPropertyChanged(nameof(SwitchModeBadge));
        OnPropertyChanged(nameof(SwitchModeTooltip));
        OnPropertyChanged(nameof(IsModeActiveAndLive));
        OnPropertyChanged(nameof(IsModeActiveOnly));
        OnPropertyChanged(nameof(IsModeLiveOnly));
        OnPropertyChanged(nameof(SourceFullTooltip));
    }

    private void NotifyMediaProperties()
    {
        OnPropertyChanged(nameof(IsActiveSourceVideo));
        OnPropertyChanged(nameof(IsVideoPlaying));
        OnPropertyChanged(nameof(IsVideoPaused));
        OnPropertyChanged(nameof(IsVideoEnded));
        OnPropertyChanged(nameof(VideoPlaybackButtonGlyph));
        OnPropertyChanged(nameof(VideoPlaybackButtonTooltip));
        OnPropertyChanged(nameof(VideoDurationSeconds));
        OnPropertyChanged(nameof(VideoPositionSeconds));
        OnPropertyChanged(nameof(VideoPositionText));
        OnPropertyChanged(nameof(IsMediaMuted));
        OnPropertyChanged(nameof(MediaVolume));
        OnPropertyChanged(nameof(MediaVolumePercentText));
        OnPropertyChanged(nameof(MediaMuteButtonGlyph));
        OnPropertyChanged(nameof(MediaMuteButtonTooltip));
        OnPropertyChanged(nameof(HasSecondaryRow));
        OnPropertyChanged(nameof(VideoPlaybackRowVisibility));
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(ActiveSourceTitle));
        OnPropertyChanged(nameof(ActiveSourceGlyph));
        OnPropertyChanged(nameof(OnAirSourceTitle));
        OnPropertyChanged(nameof(SourceFullTooltip));
        OnPropertyChanged(nameof(HasActiveSource));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsBlackout));
        OnPropertyChanged(nameof(IsPresenting));
        OnPropertyChanged(nameof(SelectedSources));
        OnPropertyChanged(nameof(HasSelectedSources));
        OnPropertyChanged(nameof(CanSwitchSources));
        OnPropertyChanged(nameof(PauseButtonText));
        OnPropertyChanged(nameof(PauseButtonGlyph));
        OnPropertyChanged(nameof(BlackoutButtonText));
        OnPropertyChanged(nameof(BlackoutButtonGlyph));
        OnPropertyChanged(nameof(IsActiveSourcePowerPoint));
        OnPropertyChanged(nameof(IsActiveSourceCamera));
        OnPropertyChanged(nameof(PowerPointProgressText));
        OnPropertyChanged(nameof(HasSecondaryRow));
        OnPropertyChanged(nameof(PowerPointRowVisibility));
        OnPropertyChanged(nameof(VideoPlaybackRowVisibility));
        NotifyMediaProperties();
        NotifyFullscreenProperties();
        NotifyModeProperties();
        NotifyBackgroundMusicProperties();
        OnPropertyChanged(nameof(ShowPresentationCursor));
        OnPropertyChanged(nameof(PresentationCursorGlyph));
        OnPropertyChanged(nameof(PresentationCursorTooltip));
        OnPropertyChanged(nameof(PresentationCursorOpacity));
    }

    #region Background Music SwitchDeck Controls

    public bool HasBackgroundMusic => _backgroundMusicService is not null;

    public bool IsBackgroundMusicPlaying => _backgroundMusicService?.IsPlaying ?? false;

    public string BackgroundMusicTrackTitle => _backgroundMusicService?.CurrentTrack?.Title ?? "No Track Playing";

    public string BackgroundMusicPlaylistName => _backgroundMusicService?.CurrentPlaylist?.Name ?? "No Playlist";

    public double BackgroundMusicVolume
    {
        get => (_backgroundMusicService?.Volume ?? 0.5) * 100.0;
        set
        {
            if (_backgroundMusicService is not null)
            {
                double normalized = Math.Clamp(value / 100.0, 0.0, 1.0);
                if (Math.Abs(_backgroundMusicService.Volume - normalized) > 0.001)
                {
                    _backgroundMusicService.Volume = normalized;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundMusicVolumePercentText));
                }
            }
        }
    }

    public string BackgroundMusicVolumePercentText => $"{(int)Math.Round(BackgroundMusicVolume)}%";

    public bool IsBackgroundMusicMuted => _backgroundMusicService?.IsMuted ?? false;

    public double BackgroundMusicPositionSeconds
    {
        get => _isMusicScrubbing ? _musicScrubbingPositionSeconds : (_backgroundMusicService?.Position.TotalSeconds ?? 0.0);
        set
        {
            if (_isMusicScrubbing)
            {
                _musicScrubbingPositionSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundMusicPositionText));
            }
        }
    }

    public double BackgroundMusicDurationSeconds => _backgroundMusicService?.Duration.TotalSeconds ?? 0.0;

    public string BackgroundMusicPositionText
    {
        get
        {
            var pos = _isMusicScrubbing ? TimeSpan.FromSeconds(_musicScrubbingPositionSeconds) : (_backgroundMusicService?.Position ?? TimeSpan.Zero);
            var dur = _backgroundMusicService?.Duration ?? TimeSpan.Zero;
            return $"{pos:mm\\:ss} / {dur:mm\\:ss}";
        }
    }

    public string BackgroundMusicPlayPauseGlyph => IsBackgroundMusicPlaying ? "\uE769" : "\uE768";

    public string BackgroundMusicPlayPauseTooltip => IsBackgroundMusicPlaying ? "Pause (SwitchDeck Music)" : "Play (SwitchDeck Music)";

    public string BackgroundMusicMuteGlyph => IsBackgroundMusicMuted ? "\uE74F" : "\uE767";

    public string BackgroundMusicMuteTooltip => IsBackgroundMusicMuted ? "Unmute Background Music" : "Mute Background Music";

    public Models.Music.MusicRepeatMode BackgroundMusicRepeatMode => _backgroundMusicService?.RepeatMode ?? Models.Music.MusicRepeatMode.RepeatOff;

    public string BackgroundMusicRepeatGlyph => BackgroundMusicRepeatMode switch
    {
        Models.Music.MusicRepeatMode.RepeatOne => "\uE8ED",
        Models.Music.MusicRepeatMode.RepeatAll => "\uE8EE",
        _ => "\uF5E7"
    };

    public string BackgroundMusicRepeatTooltip => BackgroundMusicRepeatMode switch
    {
        Models.Music.MusicRepeatMode.RepeatOne => "Repeat: One Track",
        Models.Music.MusicRepeatMode.RepeatAll => "Repeat: All Tracks",
        _ => "Repeat: Off"
    };

    public bool IsBackgroundMusicShuffleEnabled => _backgroundMusicService?.IsShuffleEnabled ?? false;

    public string BackgroundMusicShuffleTooltip => IsBackgroundMusicShuffleEnabled ? "Shuffle: On" : "Shuffle: Off";

    public double BackgroundMusicShuffleOpacity => IsBackgroundMusicShuffleEnabled ? 1.0 : 0.45;

    public IReadOnlyList<Models.Music.MusicPlaylist> Playlists => _backgroundMusicService?.Playlists ?? Array.Empty<Models.Music.MusicPlaylist>();

    public Models.Music.MusicPlaylist? ActivePlaylist
    {
        get => _backgroundMusicService?.CurrentPlaylist;
        set
        {
            if (_backgroundMusicService is not null && value is not null && _backgroundMusicService.CurrentPlaylist?.Id != value.Id)
            {
                _backgroundMusicService.SetActivePlaylist(value.Id);
                NotifyBackgroundMusicProperties();
            }
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<Models.Music.MusicTrack>? ActivePlaylistTracks => ActivePlaylist?.Tracks;

    public bool HasActivePlaylistTracks => (ActivePlaylist?.Tracks.Count ?? 0) > 0;

    public bool IsBackgroundMusicPaused => _backgroundMusicService?.IsPaused ?? false;

    public string BackgroundMusicStatusText => IsBackgroundMusicPlaying ? "Playing" : (IsBackgroundMusicPaused ? "Paused" : "Stopped");

    [RelayCommand]
    public async Task PlayMusicTrackAsync(Models.Music.MusicTrack? track)
    {
        if (_backgroundMusicService is null || track is null) return;
        await _backgroundMusicService.PlayTrackAsync(track);
        NotifyBackgroundMusicProperties();
    }

    public string MusicButtonTooltip => IsBackgroundMusicPlaying
        ? $"Background Music: {BackgroundMusicTrackTitle} (Playing)"
        : "Background Music (SwitchDeck)";

    public string MusicButtonGlyph => "\uE8D6";

    public double BackgroundMusicButtonOpacity => IsBackgroundMusicPlaying ? 1.0 : 0.65;

    [RelayCommand]
    public async Task ToggleBackgroundMusicPlayPauseAsync()
    {
        if (_backgroundMusicService is null) return;
        if (_backgroundMusicService.IsPlaying)
        {
            _backgroundMusicService.Pause();
        }
        else
        {
            await _backgroundMusicService.PlayAsync();
        }
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public async Task NextBackgroundMusicTrackAsync()
    {
        if (_backgroundMusicService is null) return;
        await _backgroundMusicService.NextTrackAsync();
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public async Task PreviousBackgroundMusicTrackAsync()
    {
        if (_backgroundMusicService is null) return;
        await _backgroundMusicService.PreviousTrackAsync();
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public void ToggleBackgroundMusicShuffle()
    {
        if (_backgroundMusicService is null) return;
        _backgroundMusicService.IsShuffleEnabled = !_backgroundMusicService.IsShuffleEnabled;
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public void CycleBackgroundMusicRepeatMode()
    {
        if (_backgroundMusicService is null) return;
        _backgroundMusicService.RepeatMode = _backgroundMusicService.RepeatMode switch
        {
            Models.Music.MusicRepeatMode.RepeatOff => Models.Music.MusicRepeatMode.RepeatAll,
            Models.Music.MusicRepeatMode.RepeatAll => Models.Music.MusicRepeatMode.RepeatOne,
            _ => Models.Music.MusicRepeatMode.RepeatOff
        };
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public void ToggleBackgroundMusicMute()
    {
        if (_backgroundMusicService is null) return;
        _backgroundMusicService.IsMuted = !_backgroundMusicService.IsMuted;
        NotifyBackgroundMusicProperties();
    }

    public void StartMusicScrubbing()
    {
        _isMusicScrubbing = true;
        _musicScrubbingPositionSeconds = _backgroundMusicService?.Position.TotalSeconds ?? 0.0;
    }

    public void CompleteMusicScrubbing(double targetSeconds)
    {
        _isMusicScrubbing = false;
        _backgroundMusicService?.Seek(TimeSpan.FromSeconds(targetSeconds), "PresenterDockUserScrubbing");
        NotifyBackgroundMusicProperties();
    }

    [RelayCommand]
    public void ManagePlaylists()
    {
        _activationService?.ActivateMainWindow();
        _navigationService?.NavigateToMusicLibrary();
    }

    private void OnBackgroundMusicPlaylistChanged(object? sender, EventArgs e) => RunOnUIThread(() =>
    {
        OnPropertyChanged(nameof(Playlists));
        OnPropertyChanged(nameof(ActivePlaylist));
        OnPropertyChanged(nameof(ActivePlaylistTracks));
        OnPropertyChanged(nameof(HasActivePlaylistTracks));
        OnPropertyChanged(nameof(BackgroundMusicPlaylistName));
        OnPropertyChanged(nameof(BackgroundMusicTrackTitle));
    });

    private void OnBackgroundMusicPlaybackStateChanged(object? sender, EventArgs e) => RunOnUIThread(NotifyBackgroundMusicProperties);

    private void OnBackgroundMusicTrackChanged(object? sender, EventArgs e) => RunOnUIThread(NotifyBackgroundMusicProperties);

    private void OnBackgroundMusicPositionChanged(object? sender, TimeSpan position) => RunOnUIThread(() =>
    {
        if (!_isMusicScrubbing)
        {
            OnPropertyChanged(nameof(BackgroundMusicPositionSeconds));
            OnPropertyChanged(nameof(BackgroundMusicPositionText));
        }
    });

    public void NotifyBackgroundMusicProperties()
    {
        OnPropertyChanged(nameof(HasBackgroundMusic));
        OnPropertyChanged(nameof(IsBackgroundMusicPlaying));
        OnPropertyChanged(nameof(IsBackgroundMusicPaused));
        OnPropertyChanged(nameof(BackgroundMusicStatusText));
        OnPropertyChanged(nameof(ActivePlaylistTracks));
        OnPropertyChanged(nameof(HasActivePlaylistTracks));
        OnPropertyChanged(nameof(BackgroundMusicTrackTitle));
        OnPropertyChanged(nameof(BackgroundMusicPlaylistName));
        OnPropertyChanged(nameof(BackgroundMusicVolume));
        OnPropertyChanged(nameof(BackgroundMusicVolumePercentText));
        OnPropertyChanged(nameof(IsBackgroundMusicMuted));
        OnPropertyChanged(nameof(BackgroundMusicDurationSeconds));
        OnPropertyChanged(nameof(BackgroundMusicPositionSeconds));
        OnPropertyChanged(nameof(BackgroundMusicPositionText));
        OnPropertyChanged(nameof(BackgroundMusicPlayPauseGlyph));
        OnPropertyChanged(nameof(BackgroundMusicPlayPauseTooltip));
        OnPropertyChanged(nameof(BackgroundMusicMuteGlyph));
        OnPropertyChanged(nameof(BackgroundMusicMuteTooltip));
        OnPropertyChanged(nameof(BackgroundMusicRepeatMode));
        OnPropertyChanged(nameof(BackgroundMusicRepeatGlyph));
        OnPropertyChanged(nameof(BackgroundMusicRepeatTooltip));
        OnPropertyChanged(nameof(IsBackgroundMusicShuffleEnabled));
        OnPropertyChanged(nameof(BackgroundMusicShuffleTooltip));
        OnPropertyChanged(nameof(BackgroundMusicShuffleOpacity));
        OnPropertyChanged(nameof(Playlists));
        OnPropertyChanged(nameof(ActivePlaylist));
        OnPropertyChanged(nameof(MusicButtonTooltip));
        OnPropertyChanged(nameof(MusicButtonGlyph));
        OnPropertyChanged(nameof(BackgroundMusicButtonOpacity));
    }

    #endregion
}
