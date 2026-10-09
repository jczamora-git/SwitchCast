using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Source management page for discovering and organizing presentation windows, monitors, and direct media files.
/// </summary>
public sealed partial class SourcesPage : Page
{
    public SourcesPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SourcesViewModel>();
    }

    public SourcesViewModel ViewModel { get; }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasDiscoveredSources && !ViewModel.IsRefreshing)
        {
            await ViewModel.RefreshSourcesCommand.ExecuteAsync(null);
        }
    }

    private async void OnRemoveMediaClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectableSourceItem item)
        {
            await ViewModel.RemoveMediaCommand.ExecuteAsync(item);
        }
    }
}
