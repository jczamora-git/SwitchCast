using System.Text.RegularExpressions;
using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Privacy-first redaction service ensuring user credentials, personal directories, emails, and tokens are scrubbed.
/// </summary>
public sealed class DiagnosticRedactionService : IDiagnosticRedactionService
{
    private readonly string _currentUserName;
    private readonly string _userProfileDir;

    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AuthTokenRegex = new(
        @"(bearer\s+[a-zA-Z0-9_\-\.]{16,}|token\s*[:=]\s*[a-zA-Z0-9_\-\.]{16,}|password\s*[:=\s]\s*[^\s&]+|api[_\-]?key\s*[:=]\s*[a-zA-Z0-9_\-\.]{16,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UserPathRegex = new(
        @"[a-zA-Z]:\\Users\\[^\s\\/""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public DiagnosticRedactionService(string? customUserName = null, string? customProfileDir = null)
    {
        _currentUserName = !string.IsNullOrWhiteSpace(customUserName)
            ? customUserName
            : Environment.UserName;

        _userProfileDir = !string.IsNullOrWhiteSpace(customProfileDir)
            ? customProfileDir
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string SanitizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string sanitized = text;

        // 1. Redact full user profile directory (e.g. C:\Users\JohnDoe -> %USERPROFILE%)
        if (!string.IsNullOrWhiteSpace(_userProfileDir))
        {
            sanitized = sanitized.Replace(_userProfileDir, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        // 2. Redact any other C:\Users\<Username> patterns
        sanitized = UserPathRegex.Replace(sanitized, @"C:\Users\[USER]");

        // 3. Redact explicit username string occurrences if longer than 2 characters
        if (!string.IsNullOrWhiteSpace(_currentUserName) && _currentUserName.Length > 2)
        {
            sanitized = sanitized.Replace(_currentUserName, "[USER]", StringComparison.OrdinalIgnoreCase);
        }

        // 4. Redact email addresses
        sanitized = EmailRegex.Replace(sanitized, "[REDACTED_EMAIL]");

        // 5. Redact auth tokens / API keys / passwords
        sanitized = AuthTokenRegex.Replace(sanitized, "[REDACTED_SECRET]");

        return sanitized;
    }

    public DiagnosticIncident SanitizeIncident(DiagnosticIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var sanitizedTimeline = incident.RecentTimeline.Select(e => new DiagnosticEvent
        {
            Timestamp = e.Timestamp,
            Severity = e.Severity,
            Component = SanitizeText(e.Component),
            Message = SanitizeText(e.Message),
            SourceDetails = SanitizeText(e.SourceDetails)
        }).ToList();

        return incident with
        {
            SafeMessage = SanitizeText(incident.SafeMessage),
            StackTrace = SanitizeText(incident.StackTrace),
            InnerException = SanitizeText(incident.InnerException),
            Component = SanitizeText(incident.Component),
            Operation = SanitizeText(incident.Operation),
            RecentTimeline = sanitizedTimeline
        };
    }
}
