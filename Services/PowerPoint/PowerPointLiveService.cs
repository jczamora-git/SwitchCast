using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SwitchCast.Services.Diagnostics;

namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Authoritative service managing interactive PowerPoint Live slideshow sessions via windowed PowerPoint COM automation.
/// Enforces windowed slideshow mode (ppShowTypeWindow) to strictly prevent PowerPoint from hijacking external displays.
/// Maintains a single persistent STA worker thread to strictly guarantee COM apartment affinity across all operations.
/// </summary>
public sealed class PowerPointLiveService : IPowerPointLiveService, IDisposable
{
    private const string PowerPointProgId = "PowerPoint.Application";
    private const int ppShowTypeWindow = 2; // Windowed slideshow setting

    private object? _pptApp;
    private object? _presentation;
    private object? _slideShowWindow;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly BlockingCollection<Action> _staQueue = new();
    private Thread? _staThread;
    private CancellationTokenSource? _staCts;
    private bool _disposed;

    public bool IsPowerPointInstalled
    {
        get
        {
            try
            {
                var type = Type.GetTypeFromProgID(PowerPointProgId);
                return type is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsLiveSlideshowRunning { get; private set; }

    public nint SlideshowWindowHandle { get; private set; }

    public int CurrentSlideIndex { get; private set; } = 1;

    public int TotalSlideCount { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public event EventHandler? SlideshowStarted;
    public event EventHandler? SlideshowEnded;
    public event EventHandler? SlidePositionChanged;

    public async Task<bool> StartSlideshowAsync(string pptxPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pptxPath))
        {
            LastErrorMessage = $"Presentation file not found at '{pptxPath}'.";
            return false;
        }

        if (!IsPowerPointInstalled)
        {
            LastErrorMessage = "Microsoft PowerPoint is not installed on this system. PowerPoint Live requires an installed copy of Microsoft PowerPoint.";
            return false;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureStaThreadRunning();

            var tcs = new TaskCompletionSource<bool>();

            _staQueue.Add(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pptType = Type.GetTypeFromProgID(PowerPointProgId);
                    if (pptType is null)
                    {
                        LastErrorMessage = "Failed to resolve PowerPoint COM type.";
                        tcs.SetResult(false);
                        return;
                    }

                    _pptApp = Activator.CreateInstance(pptType);
                    if (_pptApp is null)
                    {
                        LastErrorMessage = "Unable to instantiate Microsoft PowerPoint.";
                        tcs.SetResult(false);
                        return;
                    }

                    var presentations = pptType.InvokeMember("Presentations", System.Reflection.BindingFlags.GetProperty, null, _pptApp, null);
                    if (presentations is null)
                    {
                        LastErrorMessage = "Unable to access PowerPoint Presentations collection.";
                        tcs.SetResult(false);
                        return;
                    }

                    // Open(FileName, ReadOnly: -1, Untitled: 0, WithWindow: -1)
                    object[] openArgs = [pptxPath, -1, 0, -1];
                    _presentation = presentations.GetType().InvokeMember("Open", System.Reflection.BindingFlags.InvokeMethod, null, presentations, openArgs);

                    if (_presentation is null)
                    {
                        LastErrorMessage = "Unable to open presentation document in PowerPoint.";
                        tcs.SetResult(false);
                        return;
                    }

                    var slides = _presentation.GetType().InvokeMember("Slides", System.Reflection.BindingFlags.GetProperty, null, _presentation, null);
                    TotalSlideCount = (int)(slides?.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, slides, null) ?? 0);

                    var slideShowSettings = _presentation.GetType().InvokeMember("SlideShowSettings", System.Reflection.BindingFlags.GetProperty, null, _presentation, null);
                    if (slideShowSettings is null)
                    {
                        LastErrorMessage = "Unable to access SlideShowSettings.";
                        tcs.SetResult(false);
                        return;
                    }

                    // CRITICAL: Force windowed slideshow (ppShowTypeWindow = 2) so PowerPoint never collides with external projector
                    slideShowSettings.GetType().InvokeMember("ShowType", System.Reflection.BindingFlags.SetProperty, null, slideShowSettings, [ppShowTypeWindow]);

                    // Run()
                    _slideShowWindow = slideShowSettings.GetType().InvokeMember("Run", System.Reflection.BindingFlags.InvokeMethod, null, slideShowSettings, null);

                    if (_slideShowWindow is null)
                    {
                        LastErrorMessage = "Unable to start windowed slideshow.";
                        tcs.SetResult(false);
                        return;
                    }

                    int hwndInt = (int)(_slideShowWindow.GetType().InvokeMember("HWND", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null) ?? 0);
                    SlideshowWindowHandle = new nint(hwndInt);

                    CurrentSlideIndex = 1;
                    IsLiveSlideshowRunning = true;
                    LastErrorMessage = null;

                    SlideshowStarted?.Invoke(this, EventArgs.Empty);
                    SlidePositionChanged?.Invoke(this, EventArgs.Empty);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(StartSlideshowAsync), ex, sourceDetails: Path.GetFileName(pptxPath));
                    LastErrorMessage = $"Failed to start PowerPoint Live slideshow (0x{ex.HResult:X8}): {ex.Message}";
                    CleanUpComSession();
                    tcs.SetResult(false);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task NextAsync()
    {
        if (!IsLiveSlideshowRunning || _slideShowWindow is null)
        {
            return Task.CompletedTask;
        }

        return RunOnStaAsync(() =>
        {
            try
            {
                var view = _slideShowWindow.GetType().InvokeMember("View", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null);
                if (view is not null)
                {
                    view.GetType().InvokeMember("Next", System.Reflection.BindingFlags.InvokeMethod, null, view, null);
                    PollSlidePositionInternal();
                }
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(NextAsync), ex);
            }
        });
    }

    public Task PreviousAsync()
    {
        if (!IsLiveSlideshowRunning || _slideShowWindow is null)
        {
            return Task.CompletedTask;
        }

        return RunOnStaAsync(() =>
        {
            try
            {
                var view = _slideShowWindow.GetType().InvokeMember("View", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null);
                if (view is not null)
                {
                    view.GetType().InvokeMember("Previous", System.Reflection.BindingFlags.InvokeMethod, null, view, null);
                    PollSlidePositionInternal();
                }
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(PreviousAsync), ex);
            }
        });
    }

    public Task GoToSlideAsync(int slideIndex)
    {
        if (!IsLiveSlideshowRunning || _slideShowWindow is null)
        {
            return Task.CompletedTask;
        }

        return RunOnStaAsync(() =>
        {
            try
            {
                var view = _slideShowWindow.GetType().InvokeMember("View", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null);
                if (view is not null)
                {
                    view.GetType().InvokeMember("GotoSlide", System.Reflection.BindingFlags.InvokeMethod, null, view, [slideIndex]);
                    PollSlidePositionInternal();
                }
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(GoToSlideAsync), ex);
            }
        });
    }

    public Task StopSlideshowAsync()
    {
        if (!IsLiveSlideshowRunning)
        {
            return Task.CompletedTask;
        }

        return RunOnStaAsync(() =>
        {
            CleanUpComSession();
        });
    }

    private void PollSlidePositionInternal()
    {
        if (!IsLiveSlideshowRunning || _slideShowWindow is null)
        {
            return;
        }

        try
        {
            var view = _slideShowWindow.GetType().InvokeMember("View", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null);
            if (view is not null)
            {
                // CurrentShowPosition returns the 1-based current slide index in the running slideshow
                int pos = (int)(view.GetType().InvokeMember("CurrentShowPosition", System.Reflection.BindingFlags.GetProperty, null, view, null) ?? CurrentSlideIndex);
                if (pos > 0 && pos != CurrentSlideIndex)
                {
                    CurrentSlideIndex = pos;
                    SlidePositionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (COMException comEx) when (comEx.ErrorCode == unchecked((int)0x80010108)) // RPC_E_DISCONNECTED
        {
            // Slideshow window has been closed externally by user
            CleanUpComSession();
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(PollSlidePositionInternal), ex);
        }
    }

    private Task RunOnStaAsync(Action action)
    {
        if (_disposed || _staCts is null || _staCts.IsCancellationRequested || _staQueue.IsAddingCompleted)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        try
        {
            _staQueue.Add(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    private void EnsureStaThreadRunning()
    {
        if (_staThread is not null && _staThread.IsAlive)
        {
            return;
        }

        _staCts = new CancellationTokenSource();
        _staThread = new Thread(StaWorkerLoop)
        {
            Name = "SwitchCast.PowerPointLiveSTA",
            IsBackground = true
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();
    }

    private void StaWorkerLoop()
    {
        while (_staCts is not null && !_staCts.Token.IsCancellationRequested)
        {
            try
            {
                if (_staQueue.TryTake(out var action, 150, _staCts.Token))
                {
                    action();
                }
                else
                {
                    // Periodic polling to synchronize external slideshow clicks/keyboard/remote navigation
                    PollSlidePositionInternal();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(StaWorkerLoop), ex);
            }
        }
    }

    private void CleanUpComSession()
    {
        try
        {
            if (_slideShowWindow is not null)
            {
                try
                {
                    var view = _slideShowWindow.GetType().InvokeMember("View", System.Reflection.BindingFlags.GetProperty, null, _slideShowWindow, null);
                    view?.GetType().InvokeMember("Exit", System.Reflection.BindingFlags.InvokeMethod, null, view, null);
                }
                catch { }

                Marshal.ReleaseComObject(_slideShowWindow);
                _slideShowWindow = null;
            }

            if (_presentation is not null)
            {
                try
                {
                    _presentation.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, _presentation, null);
                }
                catch { }

                Marshal.ReleaseComObject(_presentation);
                _presentation = null;
            }

            if (_pptApp is not null)
            {
                try
                {
                    _pptApp.GetType().InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, _pptApp, null);
                }
                catch { }

                Marshal.ReleaseComObject(_pptApp);
                _pptApp = null;
            }
        }
        catch (Exception ex)
        {
            SwitchCastDiagnostics.LogException(nameof(PowerPointLiveService), nameof(CleanUpComSession), ex);
        }
        finally
        {
            SlideshowWindowHandle = nint.Zero;
            IsLiveSlideshowRunning = false;
            SlideshowEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_staThread is not null && _staThread.IsAlive)
        {
            try
            {
                _staQueue.Add(CleanUpComSession);
            }
            catch { }

            _staCts?.Cancel();
            _staThread.Join(500);
        }

        _staQueue.Dispose();
        _staCts?.Dispose();
        _semaphore.Dispose();
    }
}
