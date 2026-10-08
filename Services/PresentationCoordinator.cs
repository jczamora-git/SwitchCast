using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;
using SwitchCast.Services.Capture;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative coordinator managing the Presentation Output Window, presentation lifecycle, freeze/blackout, and frame distribution.
/// Implements serialized transition management, latest-request-wins coalescing, and non-blocking frame forwarding.
/// </summary>
public sealed partial class PresentationCoordinator : ObservableObject, IPresentationCoordinator
{
    private readonly IPresentationStateService _presentationStateService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly ICaptureCoordinator _captureCoordinator;
    private readonly IPresentationOutputRenderer _outputRenderer;
    private readonly SemaphoreSlim _transitionSemaphore = new(1, 1);

    private CaptureSource? _targetRequestedPresentationSource;
    private long _presentationSequenceNumber;
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
        var targetSource = source ??
                           _presentationStateService.ActiveSource ??
                           _presentationStateService.SelectedSources.FirstOrDefault(s => s.IsAvailable);

        if (targetSource is null || !targetSource.IsAvailable)
        {
            throw new InvalidOperationException("No available presentation source selected.");
        }

        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, targetSource);

        Debug.WriteLine($"[PresentationCoordinator] StartPresentation requested seq={sequence} source={targetSource.Id}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] StartPresentation superseded seq={sequence}");
                return;
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

            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] StartPresentation superseded after capture start seq={sequence}");
                return;
            }

            _outputRenderer.Resume();
            _presentationStateService.SetActiveSource(targetSource);
            _presentationStateService.SetStatus(PresentationStatus.Active);
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation completed seq={sequence} source={targetSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            Debug.WriteLine($"[PresentationCoordinator] StartPresentation failed seq={sequence} error={ex.Message}");
            throw;
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task StopPresentationAsync()
    {
        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, null);

        Debug.WriteLine($"[PresentationCoordinator] StopPresentation requested seq={sequence}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _outputRenderer.Clear();
            _presentationStateService.SetActiveSource(null);
            _presentationStateService.SetStatus(PresentationStatus.Idle);
            LastErrorMessage = null;
            await _captureCoordinator.StopPreviewAsync().ConfigureAwait(false);
            Debug.WriteLine($"[PresentationCoordinator] StopPresentation completed seq={sequence}");
        }
        finally
        {
            _transitionSemaphore.Release();
        }
    }

    public async Task SwitchPresentationSourceAsync(CaptureSource newSource)
    {
        ArgumentNullException.ThrowIfNull(newSource);

        long sequence = Interlocked.Increment(ref _presentationSequenceNumber);
        Volatile.Write(ref _targetRequestedPresentationSource, newSource);

        Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation requested seq={sequence} target={newSource.Id}");

        await _transitionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation superseded seq={sequence}");
                return;
            }

            if (!newSource.IsAvailable)
            {
                throw new InvalidOperationException($"Source '{newSource.Title}' is marked unavailable.");
            }

            if (_presentationStateService.ActiveSource?.Id == newSource.Id && Status == PresentationStatus.Active)
            {
                return;
            }

            LastErrorMessage = null;

            // Switch capture engine source while keeping presentation output window open
            await _captureCoordinator.SwitchPreviewSourceAsync(newSource).ConfigureAwait(false);

            if (Volatile.Read(ref _presentationSequenceNumber) != sequence)
            {
                Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation superseded after capture switch seq={sequence}");
                return;
            }

            _presentationStateService.SetActiveSource(newSource);
            if (Status != PresentationStatus.Paused && Status != PresentationStatus.Blackout)
            {
                _outputRenderer.Resume();
                _presentationStateService.SetStatus(PresentationStatus.Active);
            }
            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation completed seq={sequence} target={newSource.Id}");
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            _presentationStateService.SetStatus(PresentationStatus.Error);
            _outputRenderer.Clear();
            Debug.WriteLine($"[PresentationCoordinator] SwitchPresentation failed seq={sequence} error={ex.Message}");
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

    public async Task SwitchToNextSourceAsync()
    {
        var availableSources = _presentationStateService.SelectedSources.Where(s => s.IsAvailable).ToList();
        if (availableSources.Count == 0)
        {
            return;
        }

        var current = CurrentPresentationSource ?? _presentationStateService.ActiveSource;
        int currentIndex = current is not null ? availableSources.FindIndex(s => s.Id == current.Id) : -1;
        int nextIndex = (currentIndex + 1) % availableSources.Count;
        var nextSource = availableSources[nextIndex];

        if (Status == PresentationStatus.Idle)
        {
            await StartPresentationAsync(nextSource).ConfigureAwait(false);
        }
        else
        {
            await SwitchPresentationSourceAsync(nextSource).ConfigureAwait(false);
        }
    }

    public async Task SwitchToPreviousSourceAsync()
    {
        var availableSources = _presentationStateService.SelectedSources.Where(s => s.IsAvailable).ToList();
        if (availableSources.Count == 0)
        {
            return;
        }

        var current = CurrentPresentationSource ?? _presentationStateService.ActiveSource;
        int currentIndex = current is not null ? availableSources.FindIndex(s => s.Id == current.Id) : -1;
        int prevIndex = (currentIndex - 1 + availableSources.Count) % availableSources.Count;
        var prevSource = availableSources[prevIndex];

        if (Status == PresentationStatus.Idle)
        {
            await StartPresentationAsync(prevSource).ConfigureAwait(false);
        }
        else
        {
            await SwitchPresentationSourceAsync(prevSource).ConfigureAwait(false);
        }
    }

    public async Task SwitchToSourceIndexAsync(int index)
    {
        var availableSources = _presentationStateService.SelectedSources.Where(s => s.IsAvailable).ToList();
        if (index < 0 || index >= availableSources.Count)
        {
            return;
        }

        var targetSource = availableSources[index];
        if (Status == PresentationStatus.Idle)
        {
            await StartPresentationAsync(targetSource).ConfigureAwait(false);
        }
        else
        {
            await SwitchPresentationSourceAsync(targetSource).ConfigureAwait(false);
        }
    }

    private void OnCaptureFrameArrived(object? sender, FrameArrivedEventArgs e)
    {
        if (Status != PresentationStatus.Active)
        {
            return;
        }

        try
        {
            if (e.SharedBitmap is not null)
            {
                _ = _outputRenderer.RenderSharedBitmapAsync(e.SharedBitmap, e.Generation);
            }
            else if (e.SoftwareBitmap is not null)
            {
                _ = _outputRenderer.RenderBitmapAsync(e.SoftwareBitmap);
            }
            else if (e.Frame is not null)
            {
                _ = _outputRenderer.RenderFrameAsync(e.Frame);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PresentationCoordinator] OnCaptureFrameArrived handled error: {ex.Message}");
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
