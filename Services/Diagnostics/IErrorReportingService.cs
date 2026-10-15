using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Authoritative production-grade error reporting and diagnostics manager.
/// </summary>
public interface IErrorReportingService
{
    /// <summary>
    /// Event raised when a new error or critical incident is logged.
    /// </summary>
    event EventHandler<DiagnosticIncident>? IncidentReported;

    /// <summary>
    /// Absolute filesystem path to the local diagnostics log directory.
    /// </summary>
    string LogsDirectory { get; }

    /// <summary>
    /// Records a lightweight operational event to the diagnostic ring buffer and daily log.
    /// </summary>
    void RecordEvent(
        string component,
        string message,
        DiagnosticSeverity severity = DiagnosticSeverity.Info,
        string? sourceDetails = null);

    /// <summary>
    /// Records a structured exception incident, generating a report, deduplicating bursts, and writing local logs.
    /// </summary>
    DiagnosticIncident RecordException(
        string component,
        string operation,
        Exception ex,
        DiagnosticSeverity severity = DiagnosticSeverity.Error,
        string? category = null,
        string? currentSourceType = null,
        string? previousSourceType = null,
        string? presentationState = null);

    /// <summary>
    /// Returns the recent diagnostic incidents recorded during this or recent sessions.
    /// </summary>
    IReadOnlyList<DiagnosticIncident> GetRecentIncidents();

    /// <summary>
    /// Returns the bounded recent event timeline.
    /// </summary>
    IReadOnlyList<DiagnosticEvent> GetRecentEvents();

    /// <summary>
    /// Clears local incident history and deletes stored incident files.
    /// </summary>
    Task ClearIncidentHistoryAsync();

    /// <summary>
    /// Exports the specified incident report to a destination file path.
    /// </summary>
    Task<string> ExportIncidentReportAsync(string incidentId, string destinationFilePath);

    /// <summary>
    /// Generates a sanitized URL pre-populating a new GitHub issue for community reporting.
    /// </summary>
    string GenerateGitHubIssueUrl(DiagnosticIncident incident);
}
