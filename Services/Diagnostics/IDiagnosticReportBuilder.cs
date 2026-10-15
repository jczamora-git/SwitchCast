using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Service responsible for rendering diagnostic incidents into structured human-readable text, markdown, and JSON.
/// </summary>
public interface IDiagnosticReportBuilder
{
    /// <summary>
    /// Builds a clean, human-readable plain text report.
    /// </summary>
    string BuildPlainTextReport(DiagnosticIncident incident);

    /// <summary>
    /// Builds a structured markdown issue template suitable for GitHub community reporting.
    /// </summary>
    string BuildGitHubMarkdownReport(DiagnosticIncident incident);

    /// <summary>
    /// Builds a formatted JSON document representing the incident metadata.
    /// </summary>
    string BuildJsonReport(DiagnosticIncident incident);
}
