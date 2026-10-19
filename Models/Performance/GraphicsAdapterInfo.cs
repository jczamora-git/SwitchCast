namespace SwitchCast.Models.Performance;

/// <summary>
/// Diagnostic metadata describing an individual physical or virtual graphics adapter.
/// </summary>
public sealed class GraphicsAdapterInfo
{
    public string Name { get; init; } = "Unknown Adapter";
    public uint VendorId { get; init; }
    public uint DeviceId { get; init; }
    public ulong DedicatedVideoMemoryBytes { get; init; }
    public ulong DedicatedSystemMemoryBytes { get; init; }
    public ulong SharedSystemMemoryBytes { get; init; }
    public bool IsSoftware { get; init; }

    public string VendorName => VendorId switch
    {
        0x8086 => "Intel",
        0x10DE => "NVIDIA",
        0x1002 => "AMD",
        0x1414 => "Microsoft",
        _ => "Generic"
    };

    public string DedicatedMemoryMB => $"{DedicatedVideoMemoryBytes / (1024 * 1024)} MB";
    public string SharedMemoryMB => $"{SharedSystemMemoryBytes / (1024 * 1024)} MB";
}
