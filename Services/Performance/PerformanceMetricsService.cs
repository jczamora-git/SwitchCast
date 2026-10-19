using System.Diagnostics;
using SwitchCast.Models.Performance;
using SwitchCast.Services.Diagnostics;

namespace SwitchCast.Services.Performance;

/// <summary>
/// Authoritative real-time performance telemetry collector.
/// Employs non-blocking atomic counters and rolling time windows with zero hot-path heap allocations.
/// </summary>
public sealed class PerformanceMetricsService : IPerformanceMetricsService
{
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

    private double _startupDurationMs;
    private double _presentationStartupLatencyMs;
    private double _lastSourceSwitchLatencyMs;

    private long _framesCaptured;
    private long _previewFramesPresented;
    private long _outputFramesPresented;
    private long _framesDropped;
    private int _frameQueueDepth;
    private int _deviceResetCount;
    private int _mediaStallCount;

    // Rolling FPS tracking
    private long _lastPreviewFpsCalculationTime = Stopwatch.GetTimestamp();
    private long _previewFramesInWindow;
    private double _currentPreviewFps;

    private long _lastOutputFpsCalculationTime = Stopwatch.GetTimestamp();
    private long _outputFramesInWindow;
    private double _currentOutputFps;

    public bool IsDiagnosticsEnabled { get; set; }

    public void RecordStartupDuration(double durationMs)
    {
        _startupDurationMs = durationMs;
        if (IsDiagnosticsEnabled)
        {
            SwitchCastDiagnostics.LogInfo(nameof(PerformanceMetricsService), $"App Startup Latency={durationMs:F1}ms");
        }
    }

    public void RecordPresentationStartup(double durationMs)
    {
        _presentationStartupLatencyMs = durationMs;
        if (IsDiagnosticsEnabled)
        {
            SwitchCastDiagnostics.LogInfo(nameof(PerformanceMetricsService), $"Presentation Startup Latency={durationMs:F1}ms");
        }
    }

    public void RecordSourceSwitch(double durationMs)
    {
        _lastSourceSwitchLatencyMs = durationMs;
        if (IsDiagnosticsEnabled)
        {
            SwitchCastDiagnostics.LogInfo(nameof(PerformanceMetricsService), $"Source Switch Latency={durationMs:F1}ms");
        }
    }

    public void RecordFrameCaptured()
    {
        Interlocked.Increment(ref _framesCaptured);
    }

    public void RecordPreviewFramePresented()
    {
        Interlocked.Increment(ref _previewFramesPresented);

        long count = Interlocked.Increment(ref _previewFramesInWindow);
        long now = Stopwatch.GetTimestamp();
        long elapsedTicks = now - Volatile.Read(ref _lastPreviewFpsCalculationTime);
        if (elapsedTicks >= Stopwatch.Frequency) // ~1 second
        {
            double seconds = (double)elapsedTicks / Stopwatch.Frequency;
            Volatile.Write(ref _currentPreviewFps, count / seconds);
            Interlocked.Exchange(ref _previewFramesInWindow, 0);
            Volatile.Write(ref _lastPreviewFpsCalculationTime, now);
        }
    }

    public void RecordOutputFramePresented()
    {
        Interlocked.Increment(ref _outputFramesPresented);

        long count = Interlocked.Increment(ref _outputFramesInWindow);
        long now = Stopwatch.GetTimestamp();
        long elapsedTicks = now - Volatile.Read(ref _lastOutputFpsCalculationTime);
        if (elapsedTicks >= Stopwatch.Frequency) // ~1 second
        {
            double seconds = (double)elapsedTicks / Stopwatch.Frequency;
            Volatile.Write(ref _currentOutputFps, count / seconds);
            Interlocked.Exchange(ref _outputFramesInWindow, 0);
            Volatile.Write(ref _lastOutputFpsCalculationTime, now);
        }
    }

    public void RecordFrameDropped()
    {
        Interlocked.Increment(ref _framesDropped);
    }

    public void SetFrameQueueDepth(int depth)
    {
        Volatile.Write(ref _frameQueueDepth, Math.Max(0, depth));
    }

    public void RecordDeviceReset()
    {
        Interlocked.Increment(ref _deviceResetCount);
        SwitchCastDiagnostics.LogWarning(nameof(PerformanceMetricsService), "Direct3D 11 device reset event recorded.");
    }

    public void RecordMediaStall()
    {
        Interlocked.Increment(ref _mediaStallCount);
        if (IsDiagnosticsEnabled)
        {
            SwitchCastDiagnostics.LogWarning(nameof(PerformanceMetricsService), "Media playback stall event recorded.");
        }
    }

    /// <summary>
    /// Resets all counters and accumulated metrics.
    /// </summary>
    public void Reset()
    {
        _startupDurationMs = 0;
        _presentationStartupLatencyMs = 0;
        _lastSourceSwitchLatencyMs = 0;
        Interlocked.Exchange(ref _framesCaptured, 0);
        Interlocked.Exchange(ref _previewFramesPresented, 0);
        Interlocked.Exchange(ref _outputFramesPresented, 0);
        Interlocked.Exchange(ref _framesDropped, 0);
        Interlocked.Exchange(ref _frameQueueDepth, 0);
        Interlocked.Exchange(ref _deviceResetCount, 0);
        Interlocked.Exchange(ref _mediaStallCount, 0);
        Volatile.Write(ref _currentPreviewFps, 0);
        Volatile.Write(ref _currentOutputFps, 0);
    }

    public PerformanceSnapshot GetCurrentSnapshot()
    {
        long workingSet = 0;
        long privateBytes = 0;

        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            workingSet = currentProcess.WorkingSet64;
            privateBytes = currentProcess.PrivateMemorySize64;
        }
        catch
        {
            // Fallback if process inspection is restricted
        }

        return new PerformanceSnapshot
        {
            ApplicationUptime = _uptimeStopwatch.Elapsed,
            StartupDurationMs = _startupDurationMs,
            PresentationStartupLatencyMs = _presentationStartupLatencyMs,
            LastSourceSwitchLatencyMs = _lastSourceSwitchLatencyMs,
            PreviewFps = Math.Round(Volatile.Read(ref _currentPreviewFps), 1),
            OutputFps = Math.Round(Volatile.Read(ref _currentOutputFps), 1),
            FramesCaptured = Volatile.Read(ref _framesCaptured),
            FramesPresented = Volatile.Read(ref _outputFramesPresented),
            FramesDropped = Volatile.Read(ref _framesDropped),
            FrameQueueDepth = Volatile.Read(ref _frameQueueDepth),
            ProcessWorkingSetBytes = workingSet,
            ProcessPrivateBytes = privateBytes,
            GpuDeviceResetCount = Volatile.Read(ref _deviceResetCount),
            MediaPlaybackStalls = Volatile.Read(ref _mediaStallCount),
            IsDiagnosticsActive = IsDiagnosticsEnabled
        };
    }
}
