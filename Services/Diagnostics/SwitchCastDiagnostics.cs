using System.Diagnostics;
using SwitchCast.Models.Diagnostics;

namespace SwitchCast.Services.Diagnostics;

/// <summary>
/// Authoritative diagnostic instrumentation logger providing structured, high-fidelity logging for COM,
/// threading, frame pipeline, and hardware operations with exact HRESULTs and stack traces.
/// Bridges seamlessly to the central IErrorReportingService incident and file logging pipeline.
/// </summary>
public static class SwitchCastDiagnostics
{
    private static IErrorReportingService? _errorReportingService;

    /// <summary>
    /// Configures the active IErrorReportingService backend instance.
    /// </summary>
    public static void Initialize(IErrorReportingService reportingService)
    {
        _errorReportingService = reportingService;
    }

    /// <summary>
    /// Gets the current reporting service instance if initialized.
    /// </summary>
    public static IErrorReportingService? Service => _errorReportingService;

    /// <summary>
    /// Logs a detailed exception event with type, hex HRESULT, thread ID, message, and full stack trace.
    /// Excludes sensitive screen pixels and user credentials.
    /// </summary>
    public static void LogException(
        string component,
        string operation,
        Exception ex,
        string? sourceDetails = null,
        long elapsedMs = -1)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(ex);

        int hresult = ex.HResult;
        string threadInfo = $"Thread={Environment.CurrentManagedThreadId}";
        string elapsedInfo = elapsedMs >= 0 ? $" | Elapsed={elapsedMs}ms" : string.Empty;
        string sourceInfo = !string.IsNullOrEmpty(sourceDetails) ? $" | Source={sourceDetails}" : string.Empty;

        string formatted = $"[{component}] {operation} FAILED | Type={ex.GetType().FullName} | HResult=0x{hresult:X8} | {threadInfo}{elapsedInfo}{sourceInfo}\n" +
                           $"Message: {(string.IsNullOrWhiteSpace(ex.Message) ? "<empty message>" : ex.Message)}\n" +
                           $"Stack: {ex.StackTrace}";

        Debug.WriteLine(formatted);

        try
        {
            _errorReportingService?.RecordException(
                component,
                operation,
                ex,
                DiagnosticSeverity.Error,
                category: null,
                currentSourceType: sourceDetails);
        }
        catch (Exception serviceEx)
        {
            Debug.WriteLine($"[SwitchCastDiagnostics] Error forwarding to ErrorReportingService: {serviceEx.Message}");
        }
    }

    /// <summary>
    /// Logs an operational diagnostic event for runtime tracking and pipeline instrumentation.
    /// </summary>
    public static void LogInfo(string component, string message, string? sourceDetails = null)
    {
        Debug.WriteLine($"[{component}] {message} | Thread={Environment.CurrentManagedThreadId}");

        try
        {
            _errorReportingService?.RecordEvent(
                component,
                message,
                DiagnosticSeverity.Info,
                sourceDetails);
        }
        catch (Exception serviceEx)
        {
            Debug.WriteLine($"[SwitchCastDiagnostics] Error forwarding to ErrorReportingService: {serviceEx.Message}");
        }
    }

    /// <summary>
    /// Logs a warning diagnostic event for runtime tracking and pipeline instrumentation.
    /// </summary>
    public static void LogWarning(string component, string message, string? sourceDetails = null)
    {
        Debug.WriteLine($"[{component}] WARNING: {message} | Thread={Environment.CurrentManagedThreadId}");

        try
        {
            _errorReportingService?.RecordEvent(
                component,
                message,
                DiagnosticSeverity.Warning,
                sourceDetails);
        }
        catch (Exception serviceEx)
        {
            Debug.WriteLine($"[SwitchCastDiagnostics] Error forwarding to ErrorReportingService: {serviceEx.Message}");
        }
    }
}
