using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service for discovering connected physical and virtual display monitors.
/// </summary>
public interface IMonitorDiscoveryService
{
    /// <summary>
    /// Enumerates all connected display monitors.
    /// </summary>
    Task<IReadOnlyList<MonitorSource>> EnumerateMonitorsAsync(CancellationToken cancellationToken = default);
}
