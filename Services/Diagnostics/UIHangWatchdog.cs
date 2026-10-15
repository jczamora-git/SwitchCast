using System.Diagnostics;
using Microsoft.UI.Dispatching;
using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Proactive UI hang watchdog monitoring UI thread responsiveness without depending on the blocked UI thread.
/// Detects UI message loop starvation, COM deadlocks, and dispatch delays exceeding the configured threshold.
/// </summary>
public sealed class UIHangWatchdog : IDisposable
{
    private readonly DispatcherQueue? _uiDispatcherQueue;
    private readonly IErrorReportingService? _reportingService;
    private readonly TimeSpan _hangThreshold;
    private readonly TimeSpan _checkInterval;
    private readonly Timer _watchdogTimer;

    private long _lastHeartbeatTimestamp;
    private long _hangStartTimestamp;
    private bool _isHung;
    private bool _isCriticalReported;
    private bool _disposed;
    private int _pingInFlight;

    public UIHangWatchdog(
        DispatcherQueue? uiDispatcherQueue,
        IErrorReportingService? reportingService = null,
        TimeSpan? hangThreshold = null,
        TimeSpan? checkInterval = null)
    {
        _uiDispatcherQueue = uiDispatcherQueue;
        _reportingService = reportingService;
        _hangThreshold = hangThreshold ?? TimeSpan.FromSeconds(5.0);
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(2.0);

        _lastHeartbeatTimestamp = Stopwatch.GetTimestamp();

        // Background timer runs independently of UI thread
        _watchdogTimer = new Timer(OnWatchdogTick, null, _checkInterval, _checkInterval);
    }

    /// <summary>
    /// Gets whether the UI thread is currently flagged as unresponsive.
    /// </summary>
    public bool IsHung => _isHung;

    private void OnWatchdogTick(object? state)
    {
        if (_disposed || _uiDispatcherQueue is null)
        {
            return;
        }

        var elapsedSinceHeartbeat = Stopwatch.GetElapsedTime(_lastHeartbeatTimestamp);

        if (elapsedSinceHeartbeat >= _hangThreshold)
        {
            if (!_isHung)
            {
                _isHung = true;
                _hangStartTimestamp = Stopwatch.GetTimestamp();

                SwitchCastDiagnostics.LogWarning(
                    nameof(UIHangWatchdog),
                    $"UI thread unresponsive (threshold: {_hangThreshold.TotalSeconds:F1}s, elapsed: {elapsedSinceHeartbeat.TotalSeconds:F1}s). Possible message starvation or COM deadlock.");
            }
            else if (!_isCriticalReported && elapsedSinceHeartbeat >= TimeSpan.FromSeconds(15.0))
            {
                _isCriticalReported = true;

                _reportingService?.RecordException(
                    nameof(UIHangWatchdog),
                    "UIThreadDeadlock",
                    new TimeoutException($"UI thread message loop unresponsive for {elapsedSinceHeartbeat.TotalSeconds:F1}s."),
                    DiagnosticSeverity.Error,
                    category: "UIHang");
            }
        }

        // Post heartbeat ping to UI thread if no ping is currently in flight
        if (Interlocked.CompareExchange(ref _pingInFlight, 1, 0) == 0)
        {
            bool enqueued = _uiDispatcherQueue.TryEnqueue(() =>
            {
                if (_disposed)
                {
                    return;
                }

                if (_isHung)
                {
                    var totalHungDuration = Stopwatch.GetElapsedTime(_hangStartTimestamp);
                    _isHung = false;
                    _isCriticalReported = false;

                    SwitchCastDiagnostics.LogInfo(
                        nameof(UIHangWatchdog),
                        $"UI thread responsiveness restored after {totalHungDuration.TotalSeconds:F1}s.");
                }

                _lastHeartbeatTimestamp = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _pingInFlight, 0);
            });

            if (!enqueued)
            {
                Interlocked.Exchange(ref _pingInFlight, 0);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watchdogTimer.Dispose();
    }
}
