using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service for discovering running, capturable desktop application windows.
/// </summary>
public interface IWindowDiscoveryService
{
    /// <summary>
    /// Enumerates all eligible, active desktop windows.
    /// </summary>
    Task<IReadOnlyList<WindowSource>> EnumerateWindowsAsync(CancellationToken cancellationToken = default);
}
