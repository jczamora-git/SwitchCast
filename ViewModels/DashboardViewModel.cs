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
/// ViewModel managing the presenter dashboard, live preview capture, and workspace controls.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly INavigationService _navigationService;
    private readonly ICaptureCoordinator _captureCoordinator;

    [ObservableProperty]
    private CaptureSource? _selectedPreviewSource;

    public DashboardViewModel(
        IPresentationStateService presentationStateService,
        INavigationService navigationService,
        ICaptureCoordinator captureCoordinator)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        _captureCoordinator.PropertyChanged += OnCaptureCoordinatorPropertyChanged;

        // Default selected preview source if sources are already queued
        _selectedPreviewSource = _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public string StatusDisplayText => Status switch
    {
        PresentationStatus.Idle => IsCapturing ? "Live Preview Active" : "Not Started",
        PresentationStatus.Starting => "Initializing...",
        PresentationStatus.Active => "Presenting Live",
        PresentationStatus.Paused => "Paused",
        PresentationStatus.Blackout => "Blackout",
        PresentationStatus.Error => "Error",
        _ => "Unknown"
    };

    public string ActiveSourceTitle => _captureCoordinator.CurrentPreviewSource?.Title ?? _presentationStateService.ActiveSource?.Title ?? "None";

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public bool HasActiveSource => _captureCoordinator.CurrentPreviewSource is not null || _presentationStateService.ActiveSource is not null;

    public IReadOnlyList<CaptureSource> SelectedSources => _presentationStateService.SelectedSources;

    public CaptureState CaptureState => _captureCoordinator.State;

    public bool IsCapturing => CaptureState == CaptureState.Capturing;

    public bool IsStartingCapture => CaptureState == CaptureState.Starting;

    public bool IsCaptureIdle => CaptureState == CaptureState.Idle;

    public bool HasCaptureError => !string.IsNullOrWhiteSpace(_captureCoordinator.LastErrorMessage);

    public string CaptureErrorMessage => _captureCoordinator.LastErrorMessage ?? string.Empty;

    public ImageSource? PreviewImageSource => _captureCoordinator.PreviewImageSource;

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
            // Error state is captured and bound via ICaptureCoordinator.LastErrorMessage
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
                // Error state is captured and bound via ICaptureCoordinator.LastErrorMessage
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

    /// <summary>
    /// Placeholder command for Phase 4 start presentation (disabled in Phase 3).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartPresentation))]
    private void StartPresentation()
    {
        // Unreachable in Phase 3
    }

    private bool CanStartPresentation() => false;

    /// <summary>
    /// Placeholder command for Phase 5 pause presentation (disabled in Phase 3).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPausePresentation))]
    private void PausePresentation()
    {
        // Unreachable in Phase 3
    }

    private bool CanPausePresentation() => false;

    /// <summary>
    /// Placeholder command for Phase 5 blackout presentation (disabled in Phase 3).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBlackout))]
    private void Blackout()
    {
        // Unreachable in Phase 3
    }

    private bool CanBlackout() => false;

    partial void OnSelectedPreviewSourceChanged(CaptureSource? value)
    {
        if (IsCapturing && value is not null && value.IsAvailable && value.Id != _captureCoordinator.CurrentPreviewSource?.Id)
        {
            _ = SwitchPreviewSourceAsync(value);
        }
    }

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusDisplayText));
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
}
