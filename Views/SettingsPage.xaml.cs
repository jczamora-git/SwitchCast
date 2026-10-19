using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.Models.Diagnostics;
using SwitchCast.Services.Diagnostics;
using SwitchCast.ViewModels;
using SwitchCast.Views.Dialogs;

namespace SwitchCast.Views;

/// <summary>
/// Settings view for configuring theme, window properties, viewing version diagnostics, and reviewing error reports.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly IErrorReportingService _reportingService;
    private readonly IDiagnosticReportBuilder _reportBuilder;

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        ViewModel.SetDispatcherQueue(DispatcherQueue);
        _reportingService = App.Current.Services.GetRequiredService<IErrorReportingService>();
        _reportBuilder = App.Current.Services.GetRequiredService<IDiagnosticReportBuilder>();

        Loaded += (s, e) => ViewModel.OnPageLoaded();
        Unloaded += (s, e) => ViewModel.OnPageUnloaded();
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnViewIncidentDetailsClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DiagnosticIncident incident)
        {
            var targetRoot = App.MainWindowInstance?.Content?.XamlRoot ?? XamlRoot;
            await ErrorReportDialog.ShowAsync(targetRoot, incident, _reportingService, _reportBuilder);
        }
    }

    private async void OnClearIncidentHistoryClick(object sender, RoutedEventArgs e)
    {
        var targetRoot = App.MainWindowInstance?.Content?.XamlRoot ?? XamlRoot;
        if (targetRoot is null) return;

        var confirmDialog = new ContentDialog
        {
            Title = "Clear Incident History?",
            Content = "All local diagnostic reports and incident logs will be permanently deleted. This action cannot be undone.",
            PrimaryButtonText = "Clear History",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = targetRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ClearDiagnosticsHistoryAsync();
        }
    }

    private void OnCopyIncidentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DiagnosticIncident incident)
        {
            try
            {
                string report = _reportBuilder.BuildPlainTextReport(incident);
                var dataPackage = new DataPackage();
                dataPackage.SetText(report);
                Clipboard.SetContent(dataPackage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Failed to copy incident report: {ex.Message}");
            }
        }
    }
}
