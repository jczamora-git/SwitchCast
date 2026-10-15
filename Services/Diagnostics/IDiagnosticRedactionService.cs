using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Service providing privacy-first redaction and sanitization of diagnostic reports and logs.
/// </summary>
public interface IDiagnosticRedactionService
{
    /// <summary>
    /// Sanitizes plain text by stripping user profile names, personal paths, emails, and credentials.
    /// </summary>
    string SanitizeText(string? text);

    /// <summary>
    /// Produces a privacy-safe clone of a diagnostic incident suitable for public sharing or GitHub reporting.
    /// </summary>
    DiagnosticIncident SanitizeIncident(DiagnosticIncident incident);
}
