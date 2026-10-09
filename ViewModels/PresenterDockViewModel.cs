using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the compact floating presenter companion dock window and three-mode source switching.
/// </summary>
public partial class PresenterDockViewModel : ObservableObject
{
    private readonly IPresentationCoordinator _presentationCoordinator;
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly IPresenterDockService _dockService;
    private readonly IApplicationSettingsService _settingsService;

    [ObservableProperty]
    private bool _isCompactMode;

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

        _isCompactMode = _settingsService.CurrentSettings.StartDockInCompactMode;

        _presentationStateService.PropertyChanged += OnStatePropertyChanged;
        _presentationCoordinator.PropertyChanged += OnCoordinatorPropertyChanged;
        _presentationWindowService.DisplayModeChanged += OnWindowDisplayModeChanged;
        _presentationWindowService.WindowOpened += (s, e) => NotifyFullscreenProperties();
        _presentationWindowService.WindowClosed += (s, e) => NotifyFullscreenProperties();

        if (_presentationCoordinator.MediaPresentationService is not null)
        {
            _presentationCoordinator.MediaPresentationService.MediaStateChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(IsActiveSourceVideo));
                OnPropertyChanged(nameof(IsMediaMuted));
                OnPropertyChanged(nameof(MediaVolume));
                OnPropertyChanged(nameof(MediaVolumePercentText));
                OnPropertyChanged(nameof(MediaMuteButtonGlyph));
                OnPropertyChanged(nameof(MediaMuteButtonTooltip));
            };
        }
    }

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

    public string CompactModeGlyph => IsCompactMode ? "\uE740" : "\uE73F"; // Expand / Contract

    public string CompactModeTooltip => IsCompactMode ? "Expand Presenter Dock" : "Collapse to Compact Mode";

    partial void OnIsCompactModeChanged(bool value)
    {
        _settingsService.CurrentSettings.StartDockInCompactMode = value;
        _ = _settingsService.SaveSettingsAsync();
        OnPropertyChanged(nameof(CompactModeGlyph));
        OnPropertyChanged(nameof(CompactModeTooltip));
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
    private void ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
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
            NotifyAllProperties();
        }
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyAllProperties();
    }

    private void OnWindowDisplayModeChanged(object? sender, PresentationDisplayMode mode)
    {
        NotifyFullscreenProperties();
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
        OnPropertyChanged(nameof(CompactModeGlyph));
        OnPropertyChanged(nameof(CompactModeTooltip));
        OnPropertyChanged(nameof(IsActiveSourceVideo));
        OnPropertyChanged(nameof(IsMediaMuted));
        OnPropertyChanged(nameof(MediaVolume));
        OnPropertyChanged(nameof(MediaVolumePercentText));
        OnPropertyChanged(nameof(MediaMuteButtonGlyph));
        OnPropertyChanged(nameof(MediaMuteButtonTooltip));
        NotifyFullscreenProperties();
        NotifyModeProperties();
    }
}
