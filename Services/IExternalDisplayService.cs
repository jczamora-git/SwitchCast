using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service managing multi-monitor topology discovery, external presentation display selection,
/// and automatic fullscreen window relocation.
/// </summary>
public interface IExternalDisplayService
{
    /// <summary>
    /// Gets the monitor currently hosting or assigned to the presentation output window, or null if unassigned/default.
    /// </summary>
    MonitorSource? CurrentAssignedDisplay { get; }

    /// <summary>
    /// Raised when the active presentation output display destination changes.
    /// </summary>
    event EventHandler<MonitorSource?>? PresentationDisplayChanged;

    /// <summary>
    /// Discovers connected monitors and identifies eligible extended external displays (excluding duplicate/mirrored displays).
    /// </summary>
    Task<IReadOnlyList<MonitorSource>> GetEligibleExternalDisplaysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the best candidate display for presentation output based on topology and user settings.
    /// </summary>
    Task<MonitorSource?> SelectPresentationDisplayAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates display topology and positions the Presentation Output window on the external display if eligible.
    /// </summary>
    Task<bool> ApplyDisplayAutomationAsync(bool isStartingPresentation = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles display topology change events (e.g. monitor connect, disconnect, resolution change).
    /// </summary>
    Task HandleDisplayTopologyChangedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts watching for display topology changes.
    /// </summary>
    void StartWatching();

    /// <summary>
    /// Stops watching for display topology changes.
    /// </summary>
    void StopWatching();
}
