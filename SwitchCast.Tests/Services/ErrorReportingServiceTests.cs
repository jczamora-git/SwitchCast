using System.Text.RegularExpressions;
using SwitchCast.Models.Diagnostics;
using SwitchCast.Services.Diagnostics;
using Xunit;

namespace SwitchCast.Tests.Services;

public class ErrorReportingServiceTests : IDisposable
{
    private readonly string _testLogsDirectory;
    private readonly DiagnosticRedactionService _redactionService;
    private readonly DiagnosticReportBuilder _reportBuilder;
    private readonly ErrorReportingService _reportingService;

    public ErrorReportingServiceTests()
    {
        _testLogsDirectory = Path.Combine(Path.GetTempPath(), $"SwitchCast_TestLogs_{Guid.NewGuid():N}");
        _redactionService = new DiagnosticRedactionService();
        _reportBuilder = new DiagnosticReportBuilder(_redactionService);
        _reportingService = new ErrorReportingService(_testLogsDirectory, _reportBuilder, _redactionService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogsDirectory))
        {
            try
            {
                Directory.Delete(_testLogsDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public void RecordException_CreatesStructuredIncident_WithCorrectFields()
    {
        var ex = new InvalidOperationException("Failed to transition presentation source");

        var incident = _reportingService.RecordException(
            component: "PresentationCoordinator",
            operation: "SwitchPresentationSourceAsync",
            ex: ex,
            severity: DiagnosticSeverity.Error,
            category: "Presentation",
            currentSourceType: "PowerPoint",
            previousSourceType: "Camera",
            presentationState: "Active");

        Assert.NotNull(incident);
        Assert.StartsWith("SC-", incident.Id);
        Assert.Equal("PresentationCoordinator", incident.Component);
        Assert.Equal("SwitchPresentationSourceAsync", incident.Operation);
        Assert.Equal(DiagnosticSeverity.Error, incident.Severity);
        Assert.Equal("Presentation", incident.Category);
        Assert.Equal("PowerPoint", incident.CurrentSourceType);
        Assert.Equal("Camera", incident.PreviousSourceType);
        Assert.Equal("Active", incident.PresentationState);
        Assert.Equal(ex.GetType().FullName, incident.ExceptionType);
        Assert.Equal(ex.HResult, incident.HResult);
        Assert.Equal($"0x{ex.HResult:X8}", incident.HResultHex);
        Assert.Equal(1, incident.RepeatCount);
    }

    [Fact]
    public void RecordException_DeduplicatesBurstErrors_WithinFiveSeconds()
    {
        var ex = new System.Runtime.InteropServices.COMException("RPC_E_WRONG_THREAD", unchecked((int)0x8001010E));

        var incidentFirst = _reportingService.RecordException("DashboardViewModel", "OnPresentationStatePropertyChanged", ex);
        var incidentSecond = _reportingService.RecordException("DashboardViewModel", "OnPresentationStatePropertyChanged", ex);

        // Same component, operation, HResult, and exception type within 5s window should be deduplicated
        Assert.Equal(incidentFirst.Id, incidentSecond.Id);
        Assert.Equal(2, incidentSecond.RepeatCount);

        var allIncidents = _reportingService.GetRecentIncidents();
        Assert.Single(allIncidents);
        Assert.Equal(2, allIncidents[0].RepeatCount);
    }

    [Fact]
    public void RedactionService_RedactsWindowsUsernamesAndProfiles()
    {
        string raw = @"C:\Users\JohnDoe\AppData\Local\Temp\demo.pptx failed to load";
        string redacted = _redactionService.SanitizeText(raw);

        Assert.DoesNotContain(@"C:\Users\JohnDoe\", redacted);
        Assert.Contains("[USER]", redacted);
    }

    [Fact]
    public void RedactionService_RedactsEmailsAndSecrets()
    {
        string raw = "Contact support@example.com with bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 and password Secret123456";
        string redacted = _redactionService.SanitizeText(raw);

        Assert.DoesNotContain("support@example.com", redacted);
        Assert.Contains("[REDACTED_EMAIL]", redacted);
        Assert.DoesNotContain("Secret123456", redacted);
        Assert.Contains("[REDACTED_SECRET]", redacted);
    }

    [Fact]
    public void ReportBuilder_GeneratesSanitizedPlainText_AndGitHubMarkdown()
    {
        var ex = new System.Runtime.InteropServices.COMException("RPC_E_WRONG_THREAD", unchecked((int)0x8001010E));
        var incident = _reportingService.RecordException("DashboardViewModel", "SelectedPresentationSource", ex);

        string plainText = _reportBuilder.BuildPlainTextReport(incident);
        Assert.Contains("SWITCHCAST DIAGNOSTIC INCIDENT REPORT", plainText);
        Assert.Contains(incident.Id, plainText);
        Assert.Contains("0x8001010E", plainText);
        Assert.Contains("PRIVACY NOTICE", plainText);

        string github = _reportBuilder.BuildGitHubMarkdownReport(incident);
        Assert.Contains("## Description", github);
        Assert.Contains("## Error Information", github);
        Assert.Contains(incident.Id, github);
        Assert.Contains("0x8001010E", github);
    }

    [Fact]
    public void GenerateGitHubIssueUrl_ProducesValidAndSanitizedUrl()
    {
        var ex = new InvalidOperationException("Source switch timeout");
        var incident = _reportingService.RecordException("PresentationCoordinator", "SwitchSourceInternal", ex);

        string url = _reportingService.GenerateGitHubIssueUrl(incident);
        Assert.StartsWith("https://github.com/jczamora-git/SwitchCast/issues/new?", url);
        Assert.Contains("title=", url);
        Assert.Contains("body=", url);

        // Verify it is a valid URI
        Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri));
        Assert.Equal("github.com", uri.Host);
    }

    [Fact]
    public void LogRotation_LimitsIncidentFilesToTen()
    {
        // Generate 15 distinct incidents (different operations to avoid burst deduplication)
        for (int i = 0; i < 15; i++)
        {
            var ex = new Exception($"Exception number {i}");
            _reportingService.RecordException("TestComponent", $"Operation_{i}", ex);
        }

        // Allow background writes to finish
        Thread.Sleep(300);

        var incidentFiles = Directory.GetFiles(_testLogsDirectory, "incident-*.json");
        Assert.True(incidentFiles.Length <= 10, $"Expected at most 10 incident files, found {incidentFiles.Length}");
    }

    [Fact]
    public async Task ClearIncidentHistory_DeletesIncidentFiles()
    {
        var ex = new Exception("Sample test exception");
        _reportingService.RecordException("Component", "Operation", ex);

        Thread.Sleep(200);

        await _reportingService.ClearIncidentHistoryAsync();

        var recent = _reportingService.GetRecentIncidents();
        Assert.Empty(recent);

        var remainingFiles = Directory.GetFiles(_testLogsDirectory, "incident-*.*");
        Assert.Empty(remainingFiles);
    }

    [Fact]
    public void SwitchCastDiagnostics_BridgesToErrorReportingService()
    {
        SwitchCastDiagnostics.Initialize(_reportingService);

        var ex = new InvalidOperationException("Bridge verification error");
        SwitchCastDiagnostics.LogException("DiagnosticsBridgeTest", "ExecuteTest", ex, "TestDetails");

        var incidents = _reportingService.GetRecentIncidents();
        Assert.Contains(incidents, i => i.Component == "DiagnosticsBridgeTest" && i.Operation == "ExecuteTest");
    }

    [Fact]
    public void FlushCriticalIncident_SynchronouslyWritesFile_WithoutBackgroundThread()
    {
        var ex = new AccessViolationException("Native memory corruption");
        var incident = new DiagnosticIncident
        {
            Id = "SC-CRIT-001",
            Component = "Direct3D11Renderer",
            Operation = "RenderBitmapAsync",
            Severity = DiagnosticSeverity.Critical,
            SafeMessage = ex.Message,
            ExceptionType = ex.GetType().FullName,
            HResult = ex.HResult,
            StackTrace = ex.StackTrace ?? "at Direct3D11Renderer.RenderBitmapAsync()",
            Timestamp = DateTimeOffset.UtcNow
        };

        _reportingService.FlushCriticalIncident(incident);

        var incidentFiles = Directory.GetFiles(_testLogsDirectory, "incident-SC-CRIT-001*.json");
        Assert.Single(incidentFiles);

        string content = File.ReadAllText(incidentFiles[0]);
        Assert.Contains("SC-CRIT-001", content);
        Assert.Contains("Direct3D11Renderer", content);
    }

    [Fact]
    public void GetPreviousCrashReports_ReturnsCriticalIncidents()
    {
        var ex = new Exception("Critical terminating crash");
        var incident = _reportingService.RecordException(
            "App",
            "OnUnhandledException",
            ex,
            severity: DiagnosticSeverity.Critical);

        _reportingService.FlushCriticalIncident(incident);

        var crashReports = _reportingService.GetPreviousCrashReports();
        Assert.NotEmpty(crashReports);
        Assert.Contains(crashReports, r => r.Id == incident.Id);
    }

    [Fact]
    public void RecordException_SanitizesErroneousCurrentSourceType_ToNone()
    {
        var ex = new System.Runtime.InteropServices.COMException("Unspecified error", unchecked((int)0x80004005));
        var incident = _reportingService.RecordException(
            component: "DashboardPage",
            operation: "LookupConverter",
            ex: ex,
            severity: DiagnosticSeverity.Critical,
            category: "UI",
            currentSourceType: "HResult=0x80004005, Message=Unspecified error");

        Assert.Equal("None", incident.CurrentSourceType);
        Assert.Equal(unchecked((int)0x80004005), incident.HResult);
        Assert.Equal("0x80004005", incident.HResultHex);
        Assert.Equal("Unspecified error", incident.SafeMessage);

        string formatted = _reportBuilder.BuildPlainTextReport(incident);
        Assert.Contains("Active Source:     None", formatted);
        Assert.Contains("0x80004005", formatted);
    }

    [Fact]
    public void GetRecentIncidents_ReadsSavedDiskIncidents_AndDeduplicatesWithInMemory()
    {
        var ex1 = new InvalidOperationException("First error in memory and disk");
        var incident1 = _reportingService.RecordException("ComponentA", "OpA", ex1);
        _reportingService.FlushCriticalIncident(incident1);

        // Create a separate service instance pointing to the same logs directory (simulating app restart)
        var restoredService = new ErrorReportingService(_testLogsDirectory, _reportBuilder, _redactionService);
        var recent = restoredService.GetRecentIncidents();

        Assert.NotEmpty(recent);
        Assert.Contains(recent, i => i.Id == incident1.Id && i.SafeMessage.Contains("First error"));
    }

    [Fact]
    public async Task ClearIncidentHistoryAsync_PurgesDiskFilesAndRecentList()
    {
        var ex = new Exception("Error to clear");
        var incident = _reportingService.RecordException("TestComp", "TestOp", ex);
        _reportingService.FlushCriticalIncident(incident);

        Assert.NotEmpty(_reportingService.GetRecentIncidents());
        var filesBefore = Directory.GetFiles(_testLogsDirectory, "incident-*.json");
        Assert.NotEmpty(filesBefore);

        await _reportingService.ClearIncidentHistoryAsync();

        Assert.Empty(_reportingService.GetRecentIncidents());
        var filesAfter = Directory.GetFiles(_testLogsDirectory, "incident-*.json");
        Assert.Empty(filesAfter);
    }
}
