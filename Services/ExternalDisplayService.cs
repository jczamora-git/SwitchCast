using System.Diagnostics;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Authoritative service managing multi-monitor topology discovery, external presentation display selection,
/// and automatic fullscreen window relocation.
/// </summary>
public sealed class ExternalDisplayService : IExternalDisplayService, IDisposable
{
    private readonly IMonitorDiscoveryService _monitorDiscoveryService;
    private readonly IPresentationWindowService _presentationWindowService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private CancellationTokenSource? _debounceCts;
    private bool _isWatching;
    private bool _disposed;

    public ExternalDisplayService(
        IMonitorDiscoveryService monitorDiscoveryService,
        IPresentationWindowService presentationWindowService,
        IApplicationSettingsService settingsService)
    {
        _monitorDiscoveryService = monitorDiscoveryService ?? throw new ArgumentNullException(nameof(monitorDiscoveryService));
        _presentationWindowService = presentationWindowService ?? throw new ArgumentNullException(nameof(presentationWindowService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _presentationWindowService.WindowClosed += (s, e) =>
        {
            CurrentAssignedDisplay = null;
        };
    }

    public MonitorSource? CurrentAssignedDisplay { get; private set; }

    public bool IsWatching => _isWatching;

    public event EventHandler<MonitorSource?>? PresentationDisplayChanged;

    public async Task<IReadOnlyList<MonitorSource>> GetEligibleExternalDisplaysAsync(CancellationToken cancellationToken = default)
    {
        var monitors = await _monitorDiscoveryService.EnumerateMonitorsAsync(cancellationToken).ConfigureAwait(false);
        return FilterEligibleExternalDisplays(monitors);
    }

    /// <summary>
    /// Pure helper that filters out duplicate/mirrored displays and returns only independent extended non-primary displays.
    /// </summary>
    public static IReadOnlyList<MonitorSource> FilterEligibleExternalDisplays(IReadOnlyList<MonitorSource> monitors)
    {
        if (monitors is null || monitors.Count <= 1)
        {
            return Array.Empty<MonitorSource>();
        }

        // Group by monitor geometry (Left, Top, Width, Height) to detect mirrored/cloned displays
        var uniqueGeometryGroups = monitors
            .GroupBy(m => (m.Left, m.Top, m.Width, m.Height))
            .ToList();

        // If all monitors share identical geometry, they are in Windows Clone / Duplicate mode
        if (uniqueGeometryGroups.Count <= 1)
        {
            return Array.Empty<MonitorSource>();
        }

        // Non-primary monitors that have distinct geometry from the primary monitor(s)
        var primaryGeometries = monitors
            .Where(m => m.IsPrimary)
            .Select(m => (m.Left, m.Top, m.Width, m.Height))
            .ToHashSet();

        var eligible = new List<MonitorSource>();
        var seenGeometries = new HashSet<(int, int, int, int)>();

        foreach (var monitor in monitors.Where(m => !m.IsPrimary))
        {
            var geom = (monitor.Left, monitor.Top, monitor.Width, monitor.Height);

            // Skip if this monitor mirrors a primary display or another already-selected display
            if (primaryGeometries.Contains(geom) || seenGeometries.Contains(geom))
            {
                continue;
            }

            seenGeometries.Add(geom);
            eligible.Add(monitor);
        }

        return eligible;
    }

    public async Task<MonitorSource?> SelectPresentationDisplayAsync(CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleExternalDisplaysAsync(cancellationToken).ConfigureAwait(false);
        if (eligible.Count == 0)
        {
            return null;
        }

        var preferred = _settingsService.CurrentSettings.PreferredDisplayDeviceName;
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var matched = eligible.FirstOrDefault(m =>
                string.Equals(m.DeviceName, preferred, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Id, preferred, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Title, preferred, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                return matched;
            }
        }

        // Default to the first eligible extended display
        return eligible[0];
    }

    public async Task<bool> ApplyDisplayAutomationAsync(bool isStartingPresentation = false, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.AutoDetectExternalDisplay)
        {
            return false;
        }

        if (!_presentationWindowService.IsWindowOpen && !isStartingPresentation)
        {
            return false;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var target = await SelectPresentationDisplayAsync(cancellationToken).ConfigureAwait(false);
            if (target is null)
            {
                return false;
            }

            _presentationWindowService.MoveToMonitor(target, andFullscreen: settings.AutoFullscreenOnExternalDisplay);
            CurrentAssignedDisplay = target;
            PresentationDisplayChanged?.Invoke(this, target);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExternalDisplayService] ApplyDisplayAutomationAsync error: {ex.Message}");
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task HandleDisplayTopologyChangedAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        // Debounce rapid topology changes (e.g. HDMI hotplug handshake)
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(400, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var allMonitors = await _monitorDiscoveryService.EnumerateMonitorsAsync(cancellationToken).ConfigureAwait(false);

            if (_presentationWindowService.IsWindowOpen && CurrentAssignedDisplay is not null)
            {
                // Check if our currently assigned display is still connected
                bool stillConnected = allMonitors.Any(m =>
                    (CurrentAssignedDisplay.MonitorHandle != nint.Zero && m.MonitorHandle == CurrentAssignedDisplay.MonitorHandle) ||
                    (!string.IsNullOrEmpty(CurrentAssignedDisplay.Id) && string.Equals(m.Id, CurrentAssignedDisplay.Id, StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(m.DeviceName, CurrentAssignedDisplay.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                     m.Left == CurrentAssignedDisplay.Left &&
                     m.Top == CurrentAssignedDisplay.Top));

                if (!stillConnected)
                {
                    Debug.WriteLine("[ExternalDisplayService] Assigned external display disconnected; recovering to primary monitor.");
                    _presentationWindowService.RestoreToPrimaryMonitor(windowed: true);
                    CurrentAssignedDisplay = null;
                    PresentationDisplayChanged?.Invoke(this, null);
                    return;
                }
            }

            // If window is open but unassigned, or presentation is active, check if a newly connected display can be utilized
            if (_presentationWindowService.IsWindowOpen &&
                CurrentAssignedDisplay is null &&
                _settingsService.CurrentSettings.AutoDetectExternalDisplay)
            {
                var target = await SelectPresentationDisplayAsync(cancellationToken).ConfigureAwait(false);
                if (target is not null)
                {
                    _presentationWindowService.MoveToMonitor(target, andFullscreen: _settingsService.CurrentSettings.AutoFullscreenOnExternalDisplay);
                    CurrentAssignedDisplay = target;
                    PresentationDisplayChanged?.Invoke(this, target);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ExternalDisplayService] HandleDisplayTopologyChangedAsync error: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void StartWatching()
    {
        _isWatching = true;
    }

    public void StopWatching()
    {
        _isWatching = false;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatching();
        _semaphore.Dispose();
    }
}
