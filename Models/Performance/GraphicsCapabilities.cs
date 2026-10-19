namespace SwitchCast.Models.Performance;

/// <summary>
/// Authoritative snapshot of system graphics adapter topology, Direct3D feature levels, and capture capabilities.
/// </summary>
public sealed class GraphicsCapabilities
{
    public bool IsD3D11HardwareSupported { get; init; }
    public bool IsWarpSupported { get; init; }
    public bool IsWindowsGraphicsCaptureSupported { get; init; }
    public string FeatureLevel { get; init; } = "Unknown";
    public int FeatureLevelValue { get; init; }
    public GraphicsAdapterInfo? PrimaryAdapter { get; init; }
    public IReadOnlyList<GraphicsAdapterInfo> AvailableAdapters { get; init; } = [];

    public bool IsIntegratedGpu =>
        PrimaryAdapter is not null &&
        (PrimaryAdapter.DedicatedVideoMemoryBytes < 512 * 1024 * 1024 || PrimaryAdapter.VendorId == 0x8086);

    public string SummaryText =>
        $"{PrimaryAdapter?.Name ?? "Default GPU"} ({FeatureLevel}) • {(IsD3D11HardwareSupported ? "Hardware D3D11" : "WARP Software")}";
}
