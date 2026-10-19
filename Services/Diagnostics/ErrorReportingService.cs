using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Production error reporting and diagnostics manager storing local structured logs and incident reports under LocalAppData.
/// Features non-blocking writes, burst deduplication, automatic log rotation, and privacy-first sanitization.
/// </summary>
public sealed class ErrorReportingService : IErrorReportingService
{
    private const int MaxTimelineEvents = 100;
    private const int MaxStoredIncidents = 10;
    private const int DeduplicationWindowSeconds = 5;

    private readonly string _logsDirectory;
    private readonly IDiagnosticReportBuilder _reportBuilder;
    private readonly IDiagnosticRedactionService _redactionService;

    private readonly object _stateLock = new();
    private readonly object _persistLock = new();
    private Task _lastPersistTask = Task.CompletedTask;
    private readonly List<DiagnosticEvent> _timelineEvents = [];
    private readonly List<DiagnosticIncident> _recentIncidents = [];
    private readonly ConcurrentDictionary<string, (DiagnosticIncident Incident, DateTimeOffset LastSeen)> _dedupCache = new();

    private readonly string _appVersion;
    private readonly string _osVersion;
    private readonly string _architecture;

    public event EventHandler<DiagnosticIncident>? IncidentReported;

    public string LogsDirectory => _logsDirectory;

    public ErrorReportingService(
        string? customLogsDirectory = null,
        IDiagnosticReportBuilder? reportBuilder = null,
        IDiagnosticRedactionService? redactionService = null)
    {
        _redactionService = redactionService ?? new DiagnosticRedactionService();
        _reportBuilder = reportBuilder ?? new DiagnosticReportBuilder(_redactionService);

        if (!string.IsNullOrWhiteSpace(customLogsDirectory))
        {
            _logsDirectory = customLogsDirectory;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logsDirectory = Path.Combine(localAppData, "SwitchCast", "Logs");
        }

        try
        {
            Directory.CreateDirectory(_logsDirectory);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReportingService] Could not create logs directory: {ex.Message}");
        }

        var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        _appVersion = entryAssembly.GetName().Version?.ToString(3) ?? "1.0.0";
        _osVersion = RuntimeInformation.OSDescription;
        _architecture = RuntimeInformation.ProcessArchitecture.ToString();

        // Background housekeeping
        _ = Task.Run(PurgeOldLogsAsync);
    }

    public void RecordEvent(
        string component,
        string message,
        DiagnosticSeverity severity = DiagnosticSeverity.Info,
        string? sourceDetails = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(message);

        var ev = new DiagnosticEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Severity = severity,
            Component = component,
            Message = message,
            SourceDetails = sourceDetails
        };

        lock (_stateLock)
        {
            _timelineEvents.Add(ev);
            if (_timelineEvents.Count > MaxTimelineEvents)
            {
                _timelineEvents.RemoveAt(0);
            }
        }

        AppendToDailyLogAsync(ev);
    }

    public DiagnosticIncident RecordException(
        string component,
        string operation,
        Exception ex,
        DiagnosticSeverity severity = DiagnosticSeverity.Error,
        string? category = null,
        string? currentSourceType = null,
        string? previousSourceType = null,
        string? presentationState = null)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(ex);

        int hresult = ex.HResult;
        string exType = ex.GetType().FullName ?? ex.GetType().Name;
        string dedupKey = $"{component}|{operation}|{exType}|0x{hresult:X8}";
        var now = DateTimeOffset.UtcNow;

        // Deduplicate burst occurrences within time window
        if (_dedupCache.TryGetValue(dedupKey, out var existing))
        {
            if (now - existing.LastSeen < TimeSpan.FromSeconds(DeduplicationWindowSeconds))
            {
                existing.Incident.RepeatCount++;
                _dedupCache[dedupKey] = (existing.Incident, now);

                RecordEvent(
                    component,
                    $"Repeated exception (count={existing.Incident.RepeatCount}): {ex.Message}",
                    DiagnosticSeverity.Warning,
                    $"HResult=0x{hresult:X8}");

                return existing.Incident;
            }
        }

        // Snapshot current timeline
        List<DiagnosticEvent> timelineSnapshot;
        lock (_stateLock)
        {
            timelineSnapshot = _timelineEvents.TakeLast(20).ToList();
        }

        string incidentId = $"SC-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        string cleanSourceType = !string.IsNullOrWhiteSpace(currentSourceType) && !currentSourceType.Contains('=') && !currentSourceType.Contains("HResult", StringComparison.OrdinalIgnoreCase)
            ? currentSourceType
            : "None";

        var incident = new DiagnosticIncident
        {
            Id = incidentId,
            Timestamp = now,
            ApplicationVersion = _appVersion,
            OsVersion = _osVersion,
            Architecture = _architecture,
            Category = category ?? ClassifyCategory(component, operation, ex),
            Severity = severity,
            Component = component,
            Operation = operation,
            ExceptionType = exType,
            HResult = hresult,
            SafeMessage = string.IsNullOrWhiteSpace(ex.Message) ? "An unexpected exception occurred." : ex.Message,
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException?.ToString(),
            CurrentSourceType = cleanSourceType,
            PreviousSourceType = previousSourceType,
            PresentationState = presentationState,
            Recoverability = severity == DiagnosticSeverity.Critical ? "Unrecoverable" : "Recovered",
            RepeatCount = 1,
            RecentTimeline = timelineSnapshot
        };

        _dedupCache[dedupKey] = (incident, now);

        lock (_stateLock)
        {
            _recentIncidents.Insert(0, incident);
            if (_recentIncidents.Count > 20)
            {
                _recentIncidents.RemoveAt(_recentIncidents.Count - 1);
            }
        }

        RecordEvent(
            component,
            $"INCIDENT {incident.Id}: {incident.SafeMessage}",
            severity,
            $"HResult=0x{hresult:X8}");

        PersistIncidentInternal(incident, isSynchronous: severity == DiagnosticSeverity.Critical);

        try
        {
            IncidentReported?.Invoke(this, incident);
        }
        catch (Exception callbackEx)
        {
            Debug.WriteLine($"[ErrorReportingService] Error invoking IncidentReported handler: {callbackEx.Message}");
        }

        return incident;
    }

    public IReadOnlyList<DiagnosticIncident> GetRecentIncidents()
    {
        var incidentsMap = new Dictionary<string, DiagnosticIncident>(StringComparer.OrdinalIgnoreCase);

        // 1. Include in-memory incidents first
        lock (_stateLock)
        {
            foreach (var inc in _recentIncidents)
            {
                incidentsMap[inc.Id] = inc;
            }
        }

        // 2. Load persisted incidents from disk to preserve history across restarts
        try
        {
            if (Directory.Exists(_logsDirectory))
            {
                var files = Directory.GetFiles(_logsDirectory, "incident-*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(MaxStoredIncidents);

                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file.FullName);
                        var inc = JsonSerializer.Deserialize<DiagnosticIncident>(json);
                        if (inc is not null && !incidentsMap.ContainsKey(inc.Id))
                        {
                            incidentsMap[inc.Id] = inc;
                        }
                    }
                    catch
                    {
                        // Ignore individual corrupted file
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReportingService] GetRecentIncidents disk read failure: {ex.Message}");
        }

        return incidentsMap.Values.OrderByDescending(i => i.Timestamp).ToList();
    }

    public IReadOnlyList<DiagnosticEvent> GetRecentEvents()
    {
        lock (_stateLock)
        {
            return _timelineEvents.ToList();
        }
    }

    public async Task ClearIncidentHistoryAsync()
    {
        Task pending;
        lock (_persistLock)
        {
            pending = _lastPersistTask;
        }

        try
        {
            await pending;
        }
        catch { }

        lock (_stateLock)
        {
            _recentIncidents.Clear();
            _dedupCache.Clear();
        }

        try
        {
            if (Directory.Exists(_logsDirectory))
            {
                var files = Directory.GetFiles(_logsDirectory, "incident-*.*");
                foreach (var file in files)
                {
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            File.Delete(file);
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(50);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReportingService] Failed to clear incident files: {ex.Message}");
        }
    }

    public async Task<string> ExportIncidentReportAsync(string incidentId, string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(incidentId);
        ArgumentNullException.ThrowIfNull(destinationFilePath);

        DiagnosticIncident? target = null;
        lock (_stateLock)
        {
            target = _recentIncidents.FirstOrDefault(i => i.Id == incidentId);
        }

        if (target is null)
        {
            // Attempt to load from disk
            string jsonFile = Path.Combine(_logsDirectory, $"incident-{incidentId}.json");
            if (File.Exists(jsonFile))
            {
                var json = await File.ReadAllTextAsync(jsonFile).ConfigureAwait(false);
                target = JsonSerializer.Deserialize<DiagnosticIncident>(json);
            }
        }

        if (target is null)
        {
            throw new FileNotFoundException($"Incident '{incidentId}' could not be found.");
        }

        string reportText = destinationFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? _reportBuilder.BuildJsonReport(target)
            : _reportBuilder.BuildPlainTextReport(target);

        var dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(destinationFilePath, reportText).ConfigureAwait(false);
        return destinationFilePath;
    }

    public string GenerateGitHubIssueUrl(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        string title = Uri.EscapeDataString($"[Bug]: {incident.Component}.{incident.Operation} ({incident.HResultHex})");
        string markdown = _reportBuilder.BuildGitHubMarkdownReport(incident);

        // Keep URL under standard query string length bounds (~2000 chars)
        if (markdown.Length > 1500)
        {
            markdown = markdown[..1500] + "\n\n...(report truncated for URL limits; use 'Copy Report' for full details)";
        }

        string body = Uri.EscapeDataString(markdown);
        return $"https://github.com/jczamora-git/SwitchCast/issues/new?title={title}&body={body}";
    }

    private void AppendToDailyLogAsync(DiagnosticEvent ev)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                Directory.CreateDirectory(_logsDirectory);
                string logFile = Path.Combine(_logsDirectory, $"switchcast-{DateTime.UtcNow:yyyyMMdd}.log");
                string line = $"{ev.ToString()}\n";
                await File.AppendAllTextAsync(logFile, line).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorReportingService] AppendToDailyLogAsync failure: {ex.Message}");
            }
        });
    }

    public void FlushCriticalIncident(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        PersistIncidentInternal(incident, isSynchronous: true);

        try
        {
            Directory.CreateDirectory(_logsDirectory);
            string logFile = Path.Combine(_logsDirectory, $"switchcast-{DateTime.UtcNow:yyyyMMdd}.log");
            string line = $"[{incident.Timestamp:HH:mm:ss.fff}] [Critical] [{incident.Component}] {incident.Operation} FAILED | Type={incident.ExceptionType} | HResult=0x{incident.HResult:X8} | Thread={Environment.CurrentManagedThreadId} | Message={incident.SafeMessage}\nStack: {incident.StackTrace}\n";
            File.AppendAllText(logFile, line);
        }
        catch
        {
            // Fail-safe
        }
    }

    public IReadOnlyList<DiagnosticIncident> GetPreviousCrashReports()
    {
        var crashes = new List<DiagnosticIncident>();
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                return crashes;
            }

            var files = Directory.GetFiles(_logsDirectory, "incident-*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var incident = JsonSerializer.Deserialize<DiagnosticIncident>(json);
                    if (incident is not null &&
                        (incident.Severity == DiagnosticSeverity.Critical ||
                         string.Equals(incident.Recoverability, "Unrecoverable", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(incident.Operation, "UnhandledException", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(incident.Category, "Crash", StringComparison.OrdinalIgnoreCase)))
                    {
                        crashes.Add(incident);
                    }
                }
                catch
                {
                    // Ignore corrupted individual files
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReportingService] GetPreviousCrashReports error: {ex.Message}");
        }

        return crashes.OrderByDescending(c => c.Timestamp).ToList();
    }

    private void PersistIncidentInternal(DiagnosticIncident incident, bool isSynchronous)
    {
        void WriteFiles()
        {
            try
            {
                Directory.CreateDirectory(_logsDirectory);

                // Write plain text report
                string txtFile = Path.Combine(_logsDirectory, $"incident-{incident.Id}.txt");
                string txtReport = _reportBuilder.BuildPlainTextReport(incident);
                File.WriteAllText(txtFile, txtReport);

                // Write JSON representation
                string jsonFile = Path.Combine(_logsDirectory, $"incident-{incident.Id}.json");
                string jsonReport = _reportBuilder.BuildJsonReport(incident);
                File.WriteAllText(jsonFile, jsonReport);

                // Enforce max stored incident files
                RotateIncidentFiles();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ErrorReportingService] PersistIncidentInternal failure: {ex.Message}");
            }
        }

        if (isSynchronous)
        {
            WriteFiles();
        }
        else
        {
            lock (_persistLock)
            {
                _lastPersistTask = Task.Run(WriteFiles);
            }
        }
    }

    private void RotateIncidentFiles()
    {
        try
        {
            var txtFiles = Directory.GetFiles(_logsDirectory, "incident-*.txt")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            if (txtFiles.Count > MaxStoredIncidents)
            {
                foreach (var file in txtFiles.Skip(MaxStoredIncidents))
                {
                    try
                    {
                        file.Delete();
                        string jsonCounterpart = Path.ChangeExtension(file.FullName, ".json");
                        if (File.Exists(jsonCounterpart))
                        {
                            File.Delete(jsonCounterpart);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private async Task PurgeOldLogsAsync()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-30);
            var logFiles = Directory.GetFiles(_logsDirectory, "switchcast-*.log");
            foreach (var file in logFiles)
            {
                var info = new FileInfo(file);
                if (info.CreationTimeUtc < cutoff)
                {
                    try
                    {
                        info.Delete();
                    }
                    catch { }
                }
            }
        }
        catch { }

        await Task.CompletedTask;
    }

    private static string ClassifyCategory(string component, string operation, Exception ex)
    {
        if (component.Contains("PowerPoint", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("PowerPoint", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("PowerPoint", StringComparison.OrdinalIgnoreCase))
        {
            return "PowerPoint";
        }

        if (component.Contains("Camera", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Camera", StringComparison.OrdinalIgnoreCase))
        {
            return "Camera";
        }

        if (component.Contains("Media", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Image", StringComparison.OrdinalIgnoreCase))
        {
            return "Media";
        }

        if (component.Contains("Capture", StringComparison.OrdinalIgnoreCase) ||
            operation.Contains("Capture", StringComparison.OrdinalIgnoreCase))
        {
            return "Capture";
        }

        if (component.Contains("Renderer", StringComparison.OrdinalIgnoreCase) ||
            component.Contains("Direct3D", StringComparison.OrdinalIgnoreCase))
        {
            return "GraphicsPipeline";
        }

        if (ex is System.Runtime.InteropServices.COMException comEx && comEx.HResult == unchecked((int)0x8001010E))
        {
            return "UIThreading";
        }

        return "SourceTransition";
    }
}
