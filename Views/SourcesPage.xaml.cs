using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Source management page for discovering and organizing presentation windows and monitors.
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
}
