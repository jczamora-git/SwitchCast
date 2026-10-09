using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SwitchCast.Models;
using SwitchCast.Services;

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
    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _playbackTimer;
    private bool _isScrubbing;
    private double _scrubbingPositionSeconds;
    private bool _disposed;

    public PresenterDockViewModel(
        IPresentationCoordinator presentationCoordinator,
        IPresentationStateService presentationStateService,
        IPresentationWindowService presentationWindowService,
        IPresenterDockService dockService,
        IApplicationSettingsService settingsService)
    {
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _presentationStateService.PropertyChanged += OnStatePropertyChanged;
        _presentationCoordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        _presentationWindowService.DisplayModeChanged += OnWindowDisplayModeChanged;
        _presentationWindowService.WindowOpened += OnWindowOpened;
        _presentationWindowService.WindowClosed += OnWindowClosed;

        if (_presentationCoordinator.MediaPresentationService is not null)
        {
            _presentationCoordinator.MediaPresentationService.MediaStateChanged += OnMediaStateChanged;
        }

        // Capture ambient dispatcher if initialized on a UI thread
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
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
        InitializePlaybackTimer();
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
        if (_disposed)
        {
            return;
        }

        var dispatcher = _dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher is null)
        {
            // Headless unit test environment fallback
            action();
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            bool enqueued = dispatcher.TryEnqueue(() =>
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

    public bool CanSwitchSources => _presentationStateService.SelectedSources.Count(s => s.IsAvailable) > 1;

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
    private async Task NextSourceAsync()
    {
        await _presentationCoordinator.SwitchToNextSourceAsync();
    }

    [RelayCommand]
    private async Task PreviousSourceAsync()
    {
        await _presentationCoordinator.SwitchToPreviousSourceAsync();
    }

    [RelayCommand]
    private async Task SwitchSourceAsync(CaptureSource? source)
    {
        if (source is not null && source.IsAvailable)
        {
            await _presentationCoordinator.ExecuteSourceSwitchAsync(source);
        }
    }

    [RelayCommand]
    private async Task TogglePauseAsync()
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

    [RelayCommand]
    private async Task ToggleBlackoutAsync()
    {
        await _presentationCoordinator.ToggleBlackoutAsync();
    }

    [RelayCommand]
    private async Task StopPresentationAsync()
    {
        await _presentationCoordinator.StopPresentationAsync();
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

    [RelayCommand]
    private void CloseDock()
    {
        _dockService.CloseDock();
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
        NotifyMediaProperties();
        NotifyFullscreenProperties();
        NotifyModeProperties();
    }
}
