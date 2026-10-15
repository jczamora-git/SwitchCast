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
        _reportingService = App.Current.Services.GetRequiredService<IErrorReportingService>();
        _reportBuilder = App.Current.Services.GetRequiredService<IDiagnosticReportBuilder>();
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnViewIncidentDetailsClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DiagnosticIncident incident && XamlRoot is not null)
        {
            await ErrorReportDialog.ShowAsync(XamlRoot, incident, _reportingService, _reportBuilder);
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
