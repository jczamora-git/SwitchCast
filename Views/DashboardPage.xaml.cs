using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Control Dashboard View presenting session status, workspace preview, and quick actions.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        ViewModel.SetDispatcherQueue(DispatcherQueue);
    }

    public DashboardViewModel ViewModel { get; }

    private async void OnViewPreviousCrashReportClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.LatestPreviousCrash is not null && XamlRoot is not null)
        {
            var reportingService = App.Current.Services.GetRequiredService<Services.Diagnostics.IErrorReportingService>();
            var reportBuilder = App.Current.Services.GetRequiredService<Services.Diagnostics.IDiagnosticReportBuilder>();
            await Dialogs.ErrorReportDialog.ShowAsync(XamlRoot, ViewModel.LatestPreviousCrash, reportingService, reportBuilder);
        }
    }

    private void OnDismissCrashInfoBar(InfoBar sender, object args)
    {
        ViewModel.DismissPreviousCrashBanner();
    }

    private void OnMoveTrackUpClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Models.Music.MusicTrack track)
        {
            ViewModel.MoveMusicTrackUp(track);
        }
    }

    private void OnMoveTrackDownClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Models.Music.MusicTrack track)
        {
            ViewModel.MoveMusicTrackDown(track);
        }
    }

    private void OnRemoveTrackClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Models.Music.MusicTrack track)
        {
            ViewModel.RemoveMusicTrack(track);
        }
    }
}
