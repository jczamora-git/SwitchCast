using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.Models.Diagnostics;
using SwitchCast.Services.Diagnostics;

namespace SwitchCast.Views.Dialogs;

/// <summary>
/// Professional, custom-styled error reporting and diagnostics dialog for SwitchCast.
/// Displays sanitized incident details, prevents simultaneous duplicate popups, and
/// offers non-destructive copy, export, and GitHub issue reporting actions.
/// </summary>
public sealed partial class ErrorReportDialog : ContentDialog
{
    private static bool _isDialogOpen;
    private static readonly object _dialogLock = new();

    private readonly DiagnosticIncident _incident;
    private readonly IErrorReportingService _reportingService;
    private readonly IDiagnosticReportBuilder _reportBuilder;

    public ErrorReportDialog(
        DiagnosticIncident incident,
        IErrorReportingService reportingService,
        IDiagnosticReportBuilder reportBuilder)
    {
        _incident = incident ?? throw new ArgumentNullException(nameof(incident));
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _reportBuilder = reportBuilder ?? throw new ArgumentNullException(nameof(reportBuilder));

        InitializeComponent();
        PopulateIncidentData();
    }

    /// <summary>
    /// Displays the error reporting dialog safely without risking duplicate concurrent dialogs.
    /// Returns true if the dialog was displayed, or false if another dialog was already active.
    /// </summary>
    public static async Task<bool> ShowAsync(
        XamlRoot xamlRoot,
        DiagnosticIncident incident,
        IErrorReportingService reportingService,
        IDiagnosticReportBuilder reportBuilder)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(reportBuilder);

        lock (_dialogLock)
        {
            if (_isDialogOpen)
            {
                Debug.WriteLine("[ErrorReportDialog] Another error dialog is already open. Skipping duplicate display.");
                return false;
            }

            _isDialogOpen = true;
        }

        try
        {
            var dialog = new ErrorReportDialog(incident, reportingService, reportBuilder)
            {
                XamlRoot = xamlRoot
            };

            await dialog.ShowAsync();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ErrorReportDialog] Failed to display dialog: {ex.Message}");
            return false;
        }
        finally
        {
            lock (_dialogLock)
            {
                _isDialogOpen = false;
            }
        }
    }

    private void PopulateIncidentData()
    {
        IncidentIdBadge.Text = _incident.Id;
        ErrorCodeBadge.Text = _incident.HResultHex;

        if (!string.IsNullOrWhiteSpace(_incident.SafeMessage))
        {
            FriendlyTitleTextBlock.Text = _incident.SafeMessage;
        }

        if (!string.IsNullOrWhiteSpace(_incident.Component))
        {
            FriendlyDescriptionTextBlock.Text = $"An unexpected error occurred in {_incident.Component}. A sanitized diagnostic report is ready.";
        }

        ComponentTextBlock.Text = $"{_incident.Component} (Category: {_incident.Category})";
        OperationTextBlock.Text = $"{_incident.Operation} [Severity: {_incident.Severity}]";
        TimestampTextBlock.Text = $"{_incident.Timestamp:yyyy-MM-dd HH:mm:ss} UTC (Repeated: {_incident.RepeatCount}x)";
        EnvironmentTextBlock.Text = $"{_incident.ApplicationVersion} | {_incident.OsVersion} | {_incident.Architecture}";

        DetailsTextBox.Text = string.IsNullOrWhiteSpace(_incident.StackTrace)
            ? _incident.SafeMessage
            : $"{_incident.ExceptionType}: {_incident.SafeMessage}\n\nStack:\n{_incident.StackTrace}";
    }

    private void OnCopyReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string report = _reportBuilder.BuildPlainTextReport(_incident);
            var package = new DataPackage();
            package.SetText(report);
            Clipboard.SetContent(package);

            FeedbackInfoBar.Severity = InfoBarSeverity.Success;
            FeedbackInfoBar.Title = "Report Copied";
            FeedbackInfoBar.Message = "Sanitized diagnostic report has been copied to your clipboard.";
            FeedbackInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            FeedbackInfoBar.Severity = InfoBarSeverity.Error;
            FeedbackInfoBar.Title = "Copy Failed";
            FeedbackInfoBar.Message = $"Unable to copy report to clipboard: {ex.Message}";
            FeedbackInfoBar.IsOpen = true;
        }
    }

    private async void OnSaveReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string report = _reportBuilder.BuildPlainTextReport(_incident);
            string fileName = $"switchcast-report-{_incident.Id.ToLowerInvariant()}.txt";

            IntPtr hwnd = GetActiveWindow();
            if (App.Current.MainWindow is not null)
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
            }

            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = fileName
            };
            savePicker.FileTypeChoices.Add("Text Report", new List<string> { ".txt" });

            if (hwnd != IntPtr.Zero)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            }

            var file = await savePicker.PickSaveFileAsync();
            if (file is not null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(file, report);

                FeedbackInfoBar.Severity = InfoBarSeverity.Success;
                FeedbackInfoBar.Title = "Report Saved";
                FeedbackInfoBar.Message = $"Report successfully saved to {file.Path}";
                FeedbackInfoBar.IsOpen = true;
                return;
            }
        }
        catch (Exception)
        {
            // Fallback: write directly to export folder under Logs
            try
            {
                string exportDir = Path.Combine(_reportingService.LogsDirectory, "Exports");
                Directory.CreateDirectory(exportDir);
                string exportPath = Path.Combine(exportDir, $"switchcast-report-{_incident.Id.ToLowerInvariant()}.txt");
                File.WriteAllText(exportPath, _reportBuilder.BuildPlainTextReport(_incident));

                FeedbackInfoBar.Severity = InfoBarSeverity.Warning;
                FeedbackInfoBar.Title = "Saved to Logs Directory";
                FeedbackInfoBar.Message = $"File picker unavailable. Report saved to {exportPath}";
                FeedbackInfoBar.IsOpen = true;
            }
            catch (Exception writeEx)
            {
                FeedbackInfoBar.Severity = InfoBarSeverity.Error;
                FeedbackInfoBar.Title = "Save Failed";
                FeedbackInfoBar.Message = $"Unable to save diagnostic report: {writeEx.Message}";
                FeedbackInfoBar.IsOpen = true;
            }
        }
    }

    private async void OnReportGitHubClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string issueUrl = _reportingService.GenerateGitHubIssueUrl(_incident);
            if (Uri.TryCreate(issueUrl, UriKind.Absolute, out var uri))
            {
                bool launched = await Windows.System.Launcher.LaunchUriAsync(uri);
                if (launched)
                {
                    FeedbackInfoBar.Severity = InfoBarSeverity.Informational;
                    FeedbackInfoBar.Title = "GitHub Opened";
                    FeedbackInfoBar.Message = "GitHub issue template opened in your default browser. Please review before submitting.";
                    FeedbackInfoBar.IsOpen = true;
                    return;
                }
            }

            OnCopyReportClick(sender, e);
            FeedbackInfoBar.Title = "Report Copied for GitHub";
            FeedbackInfoBar.Message = "Could not open browser automatically. Report copied to clipboard; please paste into a new GitHub issue.";
        }
        catch (Exception ex)
        {
            FeedbackInfoBar.Severity = InfoBarSeverity.Error;
            FeedbackInfoBar.Title = "GitHub Navigation Failed";
            FeedbackInfoBar.Message = ex.Message;
            FeedbackInfoBar.IsOpen = true;
        }
    }

    private void OnReportDeveloperClick(object sender, RoutedEventArgs e)
    {
        FeedbackInfoBar.Severity = InfoBarSeverity.Informational;
        FeedbackInfoBar.Title = "Direct Submission Unavailable";
        FeedbackInfoBar.Message = "Direct server submission is not configured for this installation. Please use 'Copy Report' or 'Report on GitHub' to share diagnostics safely.";
        FeedbackInfoBar.IsOpen = true;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
}
