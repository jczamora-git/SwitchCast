namespace SwitchCast.Models.Performance;

/// <summary>
/// Point-in-time performance snapshot capturing frame rates, latencies, memory footprint, and device health.
/// </summary>
public sealed class PerformanceSnapshot
{
    public TimeSpan ApplicationUptime { get; init; }
    public double StartupDurationMs { get; init; }
    public double PresentationStartupLatencyMs { get; init; }
    public double LastSourceSwitchLatencyMs { get; init; }
    public double PreviewFps { get; init; }
    public double OutputFps { get; init; }
    public long FramesCaptured { get; init; }
    public long FramesPresented { get; init; }
    public long FramesDropped { get; init; }
    public int FrameQueueDepth { get; init; }
    public long ProcessWorkingSetBytes { get; init; }
    public long ProcessPrivateBytes { get; init; }
    public double ProcessCpuPercent { get; init; }
    public int GpuDeviceResetCount { get; init; }
    public int MediaPlaybackStalls { get; init; }
    public bool IsDiagnosticsActive { get; init; }

    public double WorkingSetMB => Math.Round(ProcessWorkingSetBytes / (1024.0 * 1024.0), 1);
    public double PrivateMB => Math.Round(ProcessPrivateBytes / (1024.0 * 1024.0), 1);
}
