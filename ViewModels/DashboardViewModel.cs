using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the presenter dashboard, status displays, and workspace controls.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly INavigationService _navigationService;

    public DashboardViewModel(
        IPresentationStateService presentationStateService,
        INavigationService navigationService)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public string StatusDisplayText => Status switch
    {
        PresentationStatus.Idle => "Not Started",
        PresentationStatus.Starting => "Initializing...",
        PresentationStatus.Active => "Presenting Live",
        PresentationStatus.Paused => "Paused",
        PresentationStatus.Blackout => "Blackout",
        PresentationStatus.Error => "Error",
        _ => "Unknown"
    };

    public string ActiveSourceTitle => _presentationStateService.ActiveSource?.Title ?? "None";

    public int SelectedSourceCount => _presentationStateService.SelectedSourceCount;

    public bool HasSelectedSources => SelectedSourceCount > 0;

    public bool HasActiveSource => _presentationStateService.ActiveSource is not null;

    public Visibility HasSelectedSourcesVisibility => HasSelectedSources ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoSelectedSourcesVisibility => HasSelectedSources ? Visibility.Collapsed : Visibility.Visible;

    public IReadOnlyList<CaptureSource> SelectedSources => _presentationStateService.SelectedSources;

    /// <summary>
    /// Navigates the shell to the Sources management page.
    /// </summary>
    [RelayCommand]
    private void NavigateToSources()
    {
        _navigationService.NavigateTo(typeof(Views.SourcesPage));
    }

    /// <summary>
    /// Placeholder command for Phase 3-5 start presentation (disabled in Phase 2).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartPresentation))]
    private void StartPresentation()
    {
        // Unreachable in Phase 2
    }

    private bool CanStartPresentation() => false;

    /// <summary>
    /// Placeholder command for Phase 5 pause presentation (disabled in Phase 2).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPausePresentation))]
    private void PausePresentation()
    {
        // Unreachable in Phase 2
    }

    private bool CanPausePresentation() => false;

    /// <summary>
    /// Placeholder command for Phase 5 blackout presentation (disabled in Phase 2).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBlackout))]
    private void Blackout()
    {
        // Unreachable in Phase 2
    }

    private bool CanBlackout() => false;

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
        }
    }
}
