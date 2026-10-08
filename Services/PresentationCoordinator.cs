using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services.Capture;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative coordinator managing the Presentation Output Window, presentation lifecycle, freeze/blackout, and frame distribution.
/// </summary>
public sealed partial class PresentationCoordinator : ObservableObject, IPresentationCoordinator
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationOutputRenderer _outputRenderer;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

    private PresentationStatus _previousStatusBeforeBlackout = PresentationStatus.Active;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public PresentationCoordinator(
        IPresentationStateService presentationStateService,
        IPresentationWindowService presentationWindowService,
        ICaptureCoordinator captureCoordinator,
        IPresentationOutputRenderer outputRenderer)
    {
        _presentationStateService = presentationStateService ?? throw new ArgumentNullException(nameof(presentationStateService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _captureCoordinator = captureCoordinator ?? throw new ArgumentNullException(nameof(captureCoordinator));
        _outputRenderer = outputRenderer ?? throw new ArgumentNullException(nameof(outputRenderer));

        _presentationStateService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IPresentationStateService.Status) ||
                e.PropertyName == nameof(IPresentationStateService.ActiveSource))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(CurrentPresentationSource));
                OnPropertyChanged(nameof(IsLive));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsBlackout));
            }
        };

        _captureCoordinator.FrameArrived += OnCaptureFrameArrived;
        _presentationWindowService.WindowOpened += (s, e) => OnPropertyChanged(nameof(IsOutputWindowOpen));
        _presentationWindowService.WindowClosed += OnWindowClosed;
    }

    public PresentationStatus Status => _presentationStateService.Status;

    public CaptureSource? CurrentPresentationSource => _presentationStateService.ActiveSource;

    public bool IsOutputWindowOpen => _presentationWindowService.IsWindowOpen;

    public bool IsLive => Status == PresentationStatus.Active;

    public bool IsPaused => Status == PresentationStatus.Paused;

    public bool IsBlackout => Status == PresentationStatus.Blackout;

    public ImageSource? PresentationImageSource => _outputRenderer.PresentationImageSource;

    public Task OpenOutputWindowAsync()
    {
        _presentationWindowService.ShowPresentationWindow();
        OnPropertyChanged(nameof(IsOutputWindowOpen));
        return Task.CompletedTask;
    }

    public async Task CloseOutputWindowAsync()
    {
        if (Status != PresentationStatus.Idle)
        {
            await StopPresentationAsync().ConfigureAwait(false);
        }

        _presentationWindowService.ClosePresentationWindow();
        OnPropertyChanged(nameof(IsOutputWindowOpen));
    }

    public async Task StartPresentationAsync(CaptureSource? source = null)
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var targetSource = source ??
                               _presentationStateService.ActiveSource ??
                               _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);

            if (targetSource is null || !targetSource.IsAvailable)
            {
                throw new InvalidOperationException("No available presentation source selected.");
            }

            LastErrorMessage = null;
            _presentationStateService.SetStatus(PresentationStatus.Starting);

            // Ensure presentation output window is visible
            _presentationWindowService.ShowPresentationWindow();
            OnPropertyChanged(nameof(IsOutputWindowOpen));

            // Start or switch underlying capture pipeline
            if (_captureCoordinator.State != CaptureState.Capturing)
            {
                await _captureCoordinator.StartPreviewAsync(targetSource).ConfigureAwait(false);
            }
            else if (_captureCoordinator.CurrentPreviewSource?.Id != targetSource.Id)
            {
                await _captureCoordinator.SwitchPreviewSourceAsync(targetSource).ConfigureAwait(false);
            }

            _outputRenderer.Resume();
            _presentationStateService.SetActiveSource(targetSource);
            _presentationStateService.SetStatus(PresentationStatus.Active);
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task StopPresentationAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _outputRenderer.Clear();
            _presentationStateService.SetActiveSource(null);
            _presentationStateService.SetStatus(PresentationStatus.Idle);
            LastErrorMessage = null;
            await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task SwitchPresentationSourceAsync(CaptureSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!newSource.IsAvailable)
            {
                throw new InvalidOperationException($"Source '{newSource.Title}' is marked unavailable.");
            }

            if (_presentationStateService.ActiveSource?.Id == newSource.Id && Status == PresentationStatus.Active)
            {
                return;
            }

            LastErrorMessage = null;

            // Switch capture engine source while keeping the presentation output window open
            await _captureCoordinator.SwitchPreviewSourceAsync(newSource).ConfigureAwait(false);

            _presentationStateService.SetActiveSource(newSource);
            if (Status != PresentationStatus.Paused && Status != PresentationStatus.Blackout)
            {
                _outputRenderer.Resume();
                _presentationStateService.SetStatus(PresentationStatus.Active);
            }
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task PausePresentationAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Active)
            {
                _outputRenderer.Freeze();
                _presentationStateService.SetStatus(PresentationStatus.Paused);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task ResumePresentationAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Paused || Status == PresentationStatus.Blackout)
            {
                _outputRenderer.Resume();
                _presentationStateService.SetStatus(PresentationStatus.Active);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task ToggleBlackoutAsync()
    {
        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == PresentationStatus.Blackout)
            {
                if (_previousStatusBeforeBlackout == PresentationStatus.Active)
                {
                    _outputRenderer.Resume();
                    _presentationStateService.SetStatus(PresentationStatus.Active);
                }
                else
                {
                    _outputRenderer.Freeze();
                    _presentationStateService.SetStatus(PresentationStatus.Paused);
                }
            }
            else if (Status == PresentationStatus.Active || Status == PresentationStatus.Paused)
            {
                _previousStatusBeforeBlackout = Status;
                _presentationStateService.SetStatus(PresentationStatus.Blackout);
            }
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    private async void OnCaptureFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (Status != PresentationStatus.Active)
        {
            return;
        }

        try
        {
            if (e.SharedBitmap is not null)
            {
                await _outputRenderer.RenderSharedBitmapAsync(e.SharedBitmap).ConfigureAwait(false);
            }
            else if (e.SoftwareBitmap is not null)
            {
                await _outputRenderer.RenderBitmapAsync(e.SoftwareBitmap).ConfigureAwait(false);
            }
            else if (e.Frame is not null)
            {
                await _outputRenderer.RenderFrameAsync(e.Frame).ConfigureAwait(false);
            }
        }
        catch
        {
            // Transient frame render exceptions suppressed to prevent crashing in async void
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsOutputWindowOpen));

        if (Status != PresentationStatus.Idle)
        {
            _ = StopPresentationAsync();
        }
    }

    public void Dispose()
    {
        _captureCoordinator.FrameArrived -= OnCaptureFrameArrived;
        _presentationWindowService.WindowClosed -= OnWindowClosed;

        _outputRenderer.Dispose();
        _transitionSemaphore.Dispose();
    }
}
