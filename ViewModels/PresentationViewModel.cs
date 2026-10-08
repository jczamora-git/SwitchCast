using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the dedicated presentation output window display states.
/// </summary>
public partial class PresentationViewModel : ObservableObject
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationCoordinator _presentationCoordinator;

    public PresentationViewModel(
        IPresentationStateService presentationStateService,
        IPresentationCoordinator presentationCoordinator)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        _presentationCoordinator.PropertyChanged += OnPresentationCoordinatorPropertyChanged;
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public ImageSource? PresentationImageSource => _presentationCoordinator.PresentationImageSource;

    public string ActiveSourceTitle => _presentationStateService.ActiveSource?.Title ?? "Ready to Present";

    public Visibility StandbyVisibility =>
        (Status == PresentationStatus.Idle || Status == PresentationStatus.Starting || Status == PresentationStatus.Error || _presentationStateService.ActiveSource is null)
        && Status != PresentationStatus.Blackout
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility LiveContentVisibility =>
        (Status == PresentationStatus.Active || Status == PresentationStatus.Paused) && _presentationStateService.ActiveSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BlackoutVisibility => Status == PresentationStatus.Blackout ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PausedIndicatorVisibility => Status == PresentationStatus.Paused ? Visibility.Visible : Visibility.Collapsed;

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status) ||
            e.PropertyName == nameof(IPresentationStateService.ActiveSource))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ActiveSourceTitle));
            OnPropertyChanged(nameof(StandbyVisibility));
            OnPropertyChanged(nameof(LiveContentVisibility));
            OnPropertyChanged(nameof(BlackoutVisibility));
            OnPropertyChanged(nameof(PausedIndicatorVisibility));
        }
    }

    private void OnPresentationCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationCoordinator.PresentationImageSource))
        {
            OnPropertyChanged(nameof(PresentationImageSource));
        }
        else if (e.PropertyName == nameof(IPresentationCoordinator.Status) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsLive) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsPaused) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsBlackout))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StandbyVisibility));
            OnPropertyChanged(nameof(LiveContentVisibility));
            OnPropertyChanged(nameof(BlackoutVisibility));
            OnPropertyChanged(nameof(PausedIndicatorVisibility));
        }
    }
}
