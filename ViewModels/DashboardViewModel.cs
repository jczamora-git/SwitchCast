using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Capture;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the presenter dashboard, live preview capture, and dedicated presentation output orchestration.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly INavigationService _navigationService;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationCoordinator _presentationCoordinator;

    [ObservableProperty]
    private CaptureSource? _selectedPreviewSource;

    [ObservableProperty]
    private CaptureSource? _selectedPresentationSource;

    public DashboardViewModel(
        IPresentationStateService presentationStateService,
        INavigationService navigationService,
        ICaptureCoordinator captureCoordinator,
        IPresentationCoordinator presentationCoordinator)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        _captureCoordinator.PropertyChanged += OnCaptureCoordinatorPropertyChanged;
        _presentationCoordinator.PropertyChanged += OnPresentationCoordinatorPropertyChanged;

        // Default selected sources if sources are already queued
        var defaultSource = _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);
        _selectedPreviewSource = defaultSource;
        _selectedPresentationSource = defaultSource;
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

    public bool IsPresenting => _presentationCoordinator.IsLive;

    public bool IsPresentationPaused => _presentationCoordinator.IsPaused;

    public bool IsPresentationBlackout => _presentationCoordinator.IsBlackout;

    public bool HasActivePresentation => IsPresenting || IsPresentationPaused || IsPresentationBlackout;

    public string PresentationOutputStatusText => IsOutputWindowOpen ? "Window Open" : "Window Closed";

    public string PresentationButtonText => HasActivePresentation ? "Stop Presenting" : "Start Presenting";

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
            SelectedPreviewSource = newSource;
            return;
        }

        if (IsCapturing)
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
    /// Starts presenting to the dedicated Presentation Output Window.
    /// </summary>
    [RelayCommand]
    public async Task StartPresentationAsync()
    {
        var targetSource = SelectedPresentationSource ?? SelectedPreviewSource ?? SelectedSources.FirstOrDefault(s => s.IsAvailable);
        if (targetSource is null)
        {
            return;
        }

        try
        {
            await _presentationCoordinator.StartPresentationAsync(targetSource).ConfigureAwait(false);
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
    /// Pauses presentation output, freezing the current frame.
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
            SelectedPresentationSource = newSource;
            return;
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
        if (IsCapturing && value is not null && value.IsAvailable && value.Id != _captureCoordinator.CurrentPreviewSource?.Id)
        {
            _ = SwitchPreviewSourceAsync(value);
        }
    }

    partial void OnSelectedPresentationSourceChanged(CaptureSource? value)
    {
        if (HasActivePresentation && value is not null && value.IsAvailable && value.Id != _presentationCoordinator.CurrentPresentationSource?.Id)
        {
            _ = SwitchPresentationSourceAsync(value);
        }
    }

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusDisplayText));
            OnPropertyChanged(nameof(IsPresenting));
            OnPropertyChanged(nameof(IsPresentationPaused));
            OnPropertyChanged(nameof(IsPresentationBlackout));
            OnPropertyChanged(nameof(HasActivePresentation));
            OnPropertyChanged(nameof(PresentationButtonText));
            OnPropertyChanged(nameof(PauseButtonText));
            OnPropertyChanged(nameof(BlackoutButtonText));
        }
        else if (e.PropertyName == nameof(IPresentationStateService.ActiveSource))
        {
            OnPropertyChanged(nameof(ActiveSourceTitle));
            OnPropertyChanged(nameof(HasActiveSource));
        }
        else if (e.PropertyName == nameof(IPresentationStateService.SelectedSourceCount) ||
                 e.PropertyName == nameof(IPresentationStateService.SelectedSources))
        {
            OnPropertyChanged(nameof(SelectedSourceCount));
            OnPropertyChanged(nameof(HasSelectedSources));
            OnPropertyChanged(nameof(HasSelectedSourcesVisibility));
            OnPropertyChanged(nameof(NoSelectedSourcesVisibility));
            OnPropertyChanged(nameof(SelectedSources));
            OnPropertyChanged(nameof(EmptyWorkspaceVisibility));
            OnPropertyChanged(nameof(ReadyToPreviewVisibility));

            if (SelectedPreviewSource is null || !SelectedSources.Any(s => s.Id == SelectedPreviewSource.Id))
            {
                SelectedPreviewSource = SelectedSources.FirstOrDefault(s => s.IsAvailable);
            }

            if (SelectedPresentationSource is null || !SelectedSources.Any(s => s.Id == SelectedPresentationSource.Id))
            {
                SelectedPresentationSource = SelectedSources.FirstOrDefault(s => s.IsAvailable);
            }
        }
    }

    private void OnCaptureCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
    }

    private void OnPresentationCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsOutputWindowOpen));
        OnPropertyChanged(nameof(PresentationOutputStatusText));
        OnPropertyChanged(nameof(IsPresenting));
        OnPropertyChanged(nameof(IsPresentationPaused));
        OnPropertyChanged(nameof(IsPresentationBlackout));
        OnPropertyChanged(nameof(HasActivePresentation));
        OnPropertyChanged(nameof(PresentationButtonText));
        OnPropertyChanged(nameof(PauseButtonText));
        OnPropertyChanged(nameof(BlackoutButtonText));
        OnPropertyChanged(nameof(HasCaptureError));
        OnPropertyChanged(nameof(CaptureErrorMessage));
        OnPropertyChanged(nameof(ActiveSourceTitle));
        OnPropertyChanged(nameof(StatusDisplayText));
    }
}
