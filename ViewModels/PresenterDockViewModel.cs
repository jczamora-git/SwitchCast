using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the compact floating presenter companion dock window.
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
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public string StatusDisplayText => Status switch
    {
        PresentationStatus.Active => "LIVE",
        PresentationStatus.Paused => "PAUSED",
        PresentationStatus.Blackout => "BLACKOUT",
        PresentationStatus.Starting => "STARTING...",
        PresentationStatus.Error => "ERROR",
        _ => "STANDBY"
    };

    public string ActiveSourceTitle =>
        _presentationCoordinator.CurrentPresentationSource?.Title ??
        _presentationStateService.ActiveSource?.Title ??
        "No Active Source";

    public bool HasActiveSource =>
        _presentationCoordinator.CurrentPresentationSource is not null ||
        _presentationStateService.ActiveSource is not null;

    public bool IsLive => _presentationCoordinator.IsLive;

    public bool IsPaused => _presentationCoordinator.IsPaused;

    public bool IsBlackout => _presentationCoordinator.IsBlackout;

    public bool IsPresenting => IsLive || IsPaused || IsBlackout;

    public IReadOnlyList<CaptureSource> SelectedSources => _presentationStateService.SelectedSources;

    public bool HasSelectedSources => _presentationStateService.SelectedSourceCount > 0;

    public bool CanSwitchSources => _presentationStateService.SelectedSources.Count(s => s.IsAvailable) > 1;

    public string PauseButtonText => IsPaused ? "Resume" : "Pause";

    public string PauseButtonGlyph => IsPaused ? "\uE768" : "\uE769"; // Play / Pause

    public string BlackoutButtonText => IsBlackout ? "Restore" : "Blackout";

    public string BlackoutButtonGlyph => IsBlackout ? "\uE7B3" : "\uED1A"; // Eye / Closed Eye

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
            if (Status == PresentationStatus.Idle)
            {
                await _presentationCoordinator.StartPresentationAsync(source);
            }
            else
            {
                await _presentationCoordinator.SwitchPresentationSourceAsync(source);
            }
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
            e.PropertyName == nameof(IPresentationStateService.SelectedSources))
        {
            NotifyAllProperties();
        }
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyAllProperties();
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusDisplayText));
        OnPropertyChanged(nameof(ActiveSourceTitle));
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
    }
}
