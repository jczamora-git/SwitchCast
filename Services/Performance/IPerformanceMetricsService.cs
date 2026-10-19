using SwitchCast.Models.Performance;

namespace SwitchCast.Services.Performance;

/// <summary>
/// Collects and aggregates real-time performance indicators (latencies, frame rates, memory, drops).
/// Uses lightweight thread-safe atomic counters with zero per-frame disk allocation.
/// </summary>
public interface IPerformanceMetricsService
{
    /// <summary>
    /// Gets or sets a value indicating whether diagnostic performance collection is enabled.
    /// </summary>
    bool IsDiagnosticsEnabled { get; set; }

    /// <summary>
    /// Records application initialization latency.
    /// </summary>
    void RecordStartupDuration(double durationMs);

    /// <summary>
    /// Records presentation startup latency from start request to first frame presented.
    /// </summary>
    void RecordPresentationStartup(double durationMs);

    /// <summary>
    /// Records source switch latency.
    /// </summary>
    void RecordSourceSwitch(double durationMs);

    /// <summary>
    /// Records an acquired capture frame.
    /// </summary>
    void RecordFrameCaptured();

    /// <summary>
    /// Records a rendered preview frame.
    /// </summary>
    void RecordPreviewFramePresented();

    /// <summary>
    /// Records a rendered presentation output frame.
    /// </summary>
    void RecordOutputFramePresented();

    /// <summary>
    /// Records a superseded or dropped frame.
    /// </summary>
    void RecordFrameDropped();

    /// <summary>
    /// Updates current frame queue depth.
    /// </summary>
    void SetFrameQueueDepth(int depth);

    /// <summary>
    /// Records a GPU device reset / recovery event.
    /// </summary>
    void RecordDeviceReset();

    /// <summary>
    /// Records a media playback stall or hitch.
    /// </summary>
    void RecordMediaStall();

    /// <summary>
    /// Generates an immutable point-in-time performance snapshot.
    /// </summary>
    PerformanceSnapshot GetCurrentSnapshot();
}
