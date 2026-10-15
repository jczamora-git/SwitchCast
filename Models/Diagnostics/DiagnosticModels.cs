using System.Text.Json.Serialization;

namespace SwitchCast.Models.Diagnostics;

/// <summary>
/// Severity levels for diagnostic events and incidents.
/// </summary>
public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

/// <summary>
/// Represents a single chronological timeline event in the diagnostic telemetry buffer.
/// </summary>
public sealed record DiagnosticEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;

    public string Component { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? SourceDetails { get; init; }

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public override string ToString() =>
        $"[{FormattedTimestamp}] [{Severity}] [{Component}] {Message}{(string.IsNullOrWhiteSpace(SourceDetails) ? "" : $" ({SourceDetails})")}";
}

/// <summary>
/// Authoritative domain model representing a structured, actionable error incident report.
/// </summary>
public sealed record DiagnosticIncident
{
    public string Id { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string ApplicationVersion { get; init; } = "1.0.0";

    public string OsVersion { get; init; } = string.Empty;

    public string Architecture { get; init; } = string.Empty;

    public string Category { get; init; } = "General";

    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Error;

    public string Component { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string? ExceptionType { get; init; }

    public int? HResult { get; init; }

    [JsonIgnore]
    public string HResultHex => HResult.HasValue ? $"0x{HResult.Value:X8}" : "N/A";

    public string SafeMessage { get; init; } = string.Empty;

    public string? StackTrace { get; init; }

    public string? InnerException { get; init; }

    public string? CurrentSourceType { get; init; }

    public string? PreviousSourceType { get; init; }

    public long? TransitionSequence { get; init; }

    public string? PresentationState { get; init; }

    public string Recoverability { get; init; } = "Recovered";

    public int RepeatCount { get; set; } = 1;

    public IReadOnlyList<DiagnosticEvent> RecentTimeline { get; init; } = [];

    [JsonIgnore]
    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string DisplayTitle => $"{Component}.{Operation} ({HResultHex})";
}
