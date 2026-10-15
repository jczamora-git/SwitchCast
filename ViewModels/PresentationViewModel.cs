using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services;
using Windows.Media.Playback;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing the dedicated presentation output window display states across all source layers (Capture, Image, Video, Standby, Blackout).
/// Marshals all UI-bound property notifications to PresentationWindow's owning UI thread.
/// </summary>
public partial class PresentationViewModel : ObservableObject, IDisposable
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationCoordinator _presentationCoordinator;
    private IUIThreadDispatcher? _dispatcher;
    private bool _disposed;

    public PresentationViewModel(
        IPresentationStateService presentationStateService,
        IPresentationCoordinator presentationCoordinator,
        IUIThreadDispatcher? uiDispatcher = null)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationCoordinator = presentationCoordinator ?? throw new ArgumentNullException(nameof(presentationCoordinator));

        _dispatcher = UIThreadDispatcherProvider.Resolve(uiDispatcher);

        _presentationStateService.PropertyChanged += OnPresentationStatePropertyChanged;
        _presentationCoordinator.PropertyChanged += OnPresentationCoordinatorPropertyChanged;
    }

    /// <summary>
    /// Configures the authoritative UI thread DispatcherQueue for the Presentation Output Window.
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

    public CaptureSource? ActiveSource => _presentationStateService.ActiveSource;

    public ImageSource? PresentationImageSource => _presentationCoordinator.PresentationImageSource;

    public ImageSource? DirectImageSource =>
        _presentationCoordinator.IsActiveSourcePowerPoint
            ? _presentationCoordinator.PowerPointSlideImageSource
            : _presentationCoordinator.DirectImageSource;

    public MediaPlayer? MediaPlayer => _presentationCoordinator.MediaPlayer;

    public string ActiveSourceTitle => ActiveSource?.Title ?? "Ready to Present";

    public bool IsLiveOrPaused => (Status == PresentationStatus.Active || Status == PresentationStatus.Paused) && ActiveSource is not null;

    public Visibility StandbyVisibility =>
        (Status == PresentationStatus.Idle || Status == PresentationStatus.Starting || Status == PresentationStatus.Error || ActiveSource is null)
        && Status != PresentationStatus.Blackout
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ScreenCaptureVisibility =>
        IsLiveOrPaused && (ActiveSource?.Type == SourceType.Window || ActiveSource?.Type == SourceType.Display || ActiveSource?.Type == SourceType.Camera || (ActiveSource is PowerPointSource ppt && ppt.IsLiveSlideshow))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility DirectImageVisibility =>
        IsLiveOrPaused && (ActiveSource?.Type == SourceType.Image || (ActiveSource is PowerPointSource ppt && !ppt.IsLiveSlideshow))
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility DirectVideoVisibility =>
        IsLiveOrPaused && ActiveSource?.Type == SourceType.Video
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility LiveContentVisibility =>
        IsLiveOrPaused && Status != PresentationStatus.Blackout
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BlackoutVisibility => Status == PresentationStatus.Blackout ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PausedIndicatorVisibility => Status == PresentationStatus.Paused ? Visibility.Visible : Visibility.Collapsed;

    private void OnPresentationStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationStateService.Status) ||
            e.PropertyName == nameof(IPresentationStateService.ActiveSource))
        {
            NotifyAllLayers();
        }
    }

    private void OnPresentationCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPresentationCoordinator.PresentationImageSource))
        {
            RunOnUIThread(() => OnPropertyChanged(nameof(PresentationImageSource)));
        }
        else if (e.PropertyName == nameof(IPresentationCoordinator.DirectImageSource) ||
                 e.PropertyName == nameof(IPresentationCoordinator.PowerPointSlideImageSource))
        {
            RunOnUIThread(() => OnPropertyChanged(nameof(DirectImageSource)));
        }
        else if (e.PropertyName == nameof(IPresentationCoordinator.MediaPlayer))
        {
            RunOnUIThread(() => OnPropertyChanged(nameof(MediaPlayer)));
        }
        else if (e.PropertyName == nameof(IPresentationCoordinator.Status) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsLive) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsPaused) ||
                 e.PropertyName == nameof(IPresentationCoordinator.IsBlackout))
        {
            NotifyAllLayers();
        }
    }

    private void NotifyAllLayers()
    {
        RunOnUIThread(() =>
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(ActiveSource));
            OnPropertyChanged(nameof(ActiveSourceTitle));
            OnPropertyChanged(nameof(IsLiveOrPaused));
            OnPropertyChanged(nameof(StandbyVisibility));
            OnPropertyChanged(nameof(ScreenCaptureVisibility));
            OnPropertyChanged(nameof(DirectImageVisibility));
            OnPropertyChanged(nameof(DirectVideoVisibility));
            OnPropertyChanged(nameof(BlackoutVisibility));
            OnPropertyChanged(nameof(PausedIndicatorVisibility));
            OnPropertyChanged(nameof(DirectImageSource));
            OnPropertyChanged(nameof(PresentationImageSource));
            OnPropertyChanged(nameof(MediaPlayer));
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _presentationStateService.PropertyChanged -= OnPresentationStatePropertyChanged;
        _presentationCoordinator.PropertyChanged -= OnPresentationCoordinatorPropertyChanged;
    }
}
