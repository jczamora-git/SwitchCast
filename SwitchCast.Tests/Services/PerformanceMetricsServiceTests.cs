using SwitchCast.Models.Performance;
using SwitchCast.Services.Performance;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PerformanceMetricsServiceTests
{
    [Fact]
    public void InitialState_HasExpectedDefaults()
    {
        var service = new PerformanceMetricsService();

        Assert.False(service.IsDiagnosticsEnabled);
        var snapshot = service.GetCurrentSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.OutputFps);
        Assert.Equal(0, snapshot.PreviewFps);
        Assert.Equal(0, snapshot.FramesCaptured);
        Assert.Equal(0, snapshot.FramesPresented);
        Assert.Equal(0, snapshot.FramesDropped);
        Assert.Equal(0, snapshot.GpuDeviceResetCount);
    }

    [Fact]
    public void Enable_Disable_TogglesCorrectly()
    {
        var service = new PerformanceMetricsService();

        service.IsDiagnosticsEnabled = true;
        Assert.True(service.IsDiagnosticsEnabled);

        service.IsDiagnosticsEnabled = false;
        Assert.False(service.IsDiagnosticsEnabled);
    }

    [Fact]
    public void RecordStartupDuration_StoresLatency()
    {
        var service = new PerformanceMetricsService();
        service.RecordStartupDuration(245.5);

        var snapshot = service.GetCurrentSnapshot();
        Assert.Equal(245.5, snapshot.StartupDurationMs);
    }

    [Fact]
    public void RecordPresentationStartup_StoresLatency()
    {
        var service = new PerformanceMetricsService();
        service.RecordPresentationStartup(88.2);

        var snapshot = service.GetCurrentSnapshot();
        Assert.Equal(88.2, snapshot.PresentationStartupLatencyMs);
    }

    [Fact]
    public void RecordSourceSwitch_StoresLatency()
    {
        var service = new PerformanceMetricsService();
        service.RecordSourceSwitch(42.1);

        var snapshot = service.GetCurrentSnapshot();
        Assert.Equal(42.1, snapshot.LastSourceSwitchLatencyMs);
    }

    [Fact]
    public void FrameCounters_IncrementCorrectly()
    {
        var service = new PerformanceMetricsService();

        service.RecordFrameCaptured();
        service.RecordFrameCaptured();
        service.RecordOutputFramePresented();
        service.RecordPreviewFramePresented();
        service.RecordPreviewFramePresented();
        service.RecordFrameDropped();
        service.RecordDeviceReset();
        service.SetFrameQueueDepth(3);

        var snapshot = service.GetCurrentSnapshot();
        Assert.Equal(2, snapshot.FramesCaptured);
        Assert.Equal(1, snapshot.FramesPresented);
        Assert.Equal(1, snapshot.FramesDropped);
        Assert.Equal(1, snapshot.GpuDeviceResetCount);
        Assert.Equal(3, snapshot.FrameQueueDepth);
    }

    [Fact]
    public void Reset_ClearsCounters()
    {
        var service = new PerformanceMetricsService();

        service.RecordFrameCaptured();
        service.RecordOutputFramePresented();
        service.RecordFrameDropped();
        service.RecordDeviceReset();
        service.RecordSourceSwitch(50.0);

        service.Reset();

        var snapshot = service.GetCurrentSnapshot();
        Assert.Equal(0, snapshot.FramesCaptured);
        Assert.Equal(0, snapshot.FramesPresented);
        Assert.Equal(0, snapshot.FramesDropped);
        Assert.Equal(0, snapshot.GpuDeviceResetCount);
        Assert.Equal(0, snapshot.LastSourceSwitchLatencyMs);
    }

    [Fact]
    public void Memory_Snapshot_ReturnsValidValues()
    {
        var service = new PerformanceMetricsService();
        var snapshot = service.GetCurrentSnapshot();

        Assert.True(snapshot.ProcessWorkingSetBytes > 0);
    }

    [Fact]
    public void ProcessCpuPercent_Snapshot_CalculatesValidRange()
    {
        var service = new PerformanceMetricsService();
        var snapshot = service.GetCurrentSnapshot();

        Assert.True(snapshot.ProcessCpuPercent >= 0.0 && snapshot.ProcessCpuPercent <= 100.0);
    }
}
