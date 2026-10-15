using System.Text;
using System.Text.Json;
using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Default builder formatting incidents into sanitized plain text, GitHub markdown, and JSON.
/// </summary>
public sealed class DiagnosticReportBuilder : IDiagnosticReportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IDiagnosticRedactionService _redactionService;

    public DiagnosticReportBuilder(IDiagnosticRedactionService? redactionService = null)
    {
        _redactionService = redactionService ?? new DiagnosticRedactionService();
    }

    public string BuildPlainTextReport(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var safe = _redactionService.SanitizeIncident(incident);

        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                      SWITCHCAST DIAGNOSTIC INCIDENT REPORT                     ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Incident ID:       {safe.Id}");
        sb.AppendLine($"Timestamp (UTC):   {safe.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Application:       SwitchCast v{safe.ApplicationVersion}");
        sb.AppendLine($"Environment:       {safe.OsVersion} ({safe.Architecture})");
        sb.AppendLine($"Severity:          {safe.Severity}");
        sb.AppendLine($"Category:          {safe.Category}");
        sb.AppendLine($"Component:         {safe.Component}");
        sb.AppendLine($"Operation:         {safe.Operation}");
        sb.AppendLine($"Error Code:        {safe.HResultHex}");
        sb.AppendLine($"Exception Type:    {safe.ExceptionType ?? "N/A"}");
        sb.AppendLine($"Recoverability:    {safe.Recoverability}");
        if (safe.RepeatCount > 1)
        {
            sb.AppendLine($"Repeat Count:      {safe.RepeatCount} occurrences");
        }
        sb.AppendLine($"Presentation State:{safe.PresentationState ?? "N/A"}");
        sb.AppendLine($"Active Source:     {safe.CurrentSourceType ?? "None"}");
        if (!string.IsNullOrWhiteSpace(safe.PreviousSourceType))
        {
            sb.AppendLine($"Previous Source:   {safe.PreviousSourceType}");
        }
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("ERROR MESSAGE:");
        sb.AppendLine(safe.SafeMessage);

        if (!string.IsNullOrWhiteSpace(safe.StackTrace))
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("STACK TRACE:");
            sb.AppendLine(safe.StackTrace);
        }

        if (!string.IsNullOrWhiteSpace(safe.InnerException))
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("INNER EXCEPTION:");
            sb.AppendLine(safe.InnerException);
        }

        if (safe.RecentTimeline.Count > 0)
        {
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("RECENT EVENT TIMELINE:");
            foreach (var ev in safe.RecentTimeline)
            {
                sb.AppendLine($"  [{ev.FormattedTimestamp}] [{ev.Severity}] [{ev.Component}] {ev.Message}");
            }
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("PRIVACY NOTICE: Personal file paths and usernames have been automatically redacted.");
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }

    public string BuildGitHubMarkdownReport(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var safe = _redactionService.SanitizeIncident(incident);

        var sb = new StringBuilder();
        sb.AppendLine("## Description");
        sb.AppendLine($"An error occurred during **{safe.Component}.{safe.Operation}**.");
        sb.AppendLine();
        sb.AppendLine("## Expected Behavior");
        sb.AppendLine("Source transition should complete smoothly without interrupting presentation.");
        sb.AppendLine();
        sb.AppendLine("## Steps to Reproduce");
        sb.AppendLine("1. Queue presentation sources.");
        sb.AppendLine($"2. Attempt transition to {safe.CurrentSourceType ?? "target source"}.");
        sb.AppendLine("3. See error notification.");
        sb.AppendLine();
        sb.AppendLine("## Error Information");
        sb.AppendLine($"- **Incident ID:** `{safe.Id}`");
        sb.AppendLine($"- **App Version:** `SwitchCast {safe.ApplicationVersion}`");
        sb.AppendLine($"- **OS / Architecture:** `{safe.OsVersion} ({safe.Architecture})`");
        sb.AppendLine($"- **Error Code:** `{safe.HResultHex}`");
        sb.AppendLine($"- **Exception Type:** `{safe.ExceptionType ?? "N/A"}`");
        sb.AppendLine($"- **Category:** `{safe.Category}`");
        sb.AppendLine($"- **Recoverability:** `{safe.Recoverability}`");
        sb.AppendLine();
        sb.AppendLine("## Diagnostics");
        sb.AppendLine("```text");
        sb.AppendLine(safe.SafeMessage);
        if (!string.IsNullOrWhiteSpace(safe.StackTrace))
        {
            sb.AppendLine();
            sb.AppendLine(safe.StackTrace);
        }
        sb.AppendLine("```");
        sb.AppendLine();

        if (safe.RecentTimeline.Count > 0)
        {
            sb.AppendLine("<details>");
            sb.AppendLine("<summary>Recent Event Timeline</summary>");
            sb.AppendLine();
            sb.AppendLine("```text");
            foreach (var ev in safe.RecentTimeline)
            {
                sb.AppendLine($"[{ev.FormattedTimestamp}] [{ev.Severity}] [{ev.Component}] {ev.Message}");
            }
            sb.AppendLine("```");
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }

    public string BuildJsonReport(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var safe = _redactionService.SanitizeIncident(incident);
        return JsonSerializer.Serialize(safe, JsonOptions);
    }
}
