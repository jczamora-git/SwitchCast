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
        ViewModel = App.Current.Services.GetRequiredService<SourcesViewModel>();
        InitializeComponent();
        ViewModel.SetDispatcherQueue(DispatcherQueue);
    }

    public SourcesViewModel ViewModel { get; }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        double availableWidth = RootContentStackPanel.ActualWidth > 0 ? RootContentStackPanel.ActualWidth : ActualWidth;
        UpdateResponsiveLayout(availableWidth);

        if (!ViewModel.HasDiscoveredSources && !ViewModel.IsRefreshing)
        {
            await ViewModel.RefreshSourcesCommand.ExecuteAsync(null);
        }
    }

    private void OnRootContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
    }

    private void UpdateResponsiveLayout(double availableWidth)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        if (availableWidth >= 840)
        {
            // Wide layout (840 px and above):
            // Header actions right-aligned; search and category dropdown share a toolbar row
            HeaderGrid.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetRow(HeaderActionsPanel, 0);
            Grid.SetColumn(HeaderActionsPanel, 1);
            Grid.SetColumnSpan(HeaderActionsPanel, 1);
            HeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            HeaderActionsPanel.Margin = new Thickness(0);

            Grid.SetRow(SearchBoxContainer, 0);
            Grid.SetColumn(SearchBoxContainer, 0);
            Grid.SetColumnSpan(SearchBoxContainer, 1);

            Grid.SetRow(CategoryComboBox, 0);
            Grid.SetColumn(CategoryComboBox, 1);
            Grid.SetColumnSpan(CategoryComboBox, 1);
            CategoryComboBox.Width = 210;
            CategoryComboBox.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(QueuedOnlyCheckBox, 0);
            Grid.SetColumn(QueuedOnlyCheckBox, 2);
            Grid.SetColumnSpan(QueuedOnlyCheckBox, 1);
            QueuedOnlyCheckBox.Margin = new Thickness(4, 0, 0, 0);

            QueueSummaryHotkeyHint.Visibility = Visibility.Visible;
            RootContentStackPanel.Padding = new Thickness(24, 18, 24, 24);
        }
        else if (availableWidth >= 560)
        {
            // Medium layout (560–839 px):
            // Header actions wrap; search occupies full row; category and Queued Only share next row
            HeaderGrid.RowDefinitions[1].Height = GridLength.Auto;
            Grid.SetRow(HeaderActionsPanel, 1);
            Grid.SetColumn(HeaderActionsPanel, 0);
            Grid.SetColumnSpan(HeaderActionsPanel, 2);
            HeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            HeaderActionsPanel.Margin = new Thickness(0, 8, 0, 0);

            Grid.SetRow(SearchBoxContainer, 0);
            Grid.SetColumn(SearchBoxContainer, 0);
            Grid.SetColumnSpan(SearchBoxContainer, 3);

            Grid.SetRow(CategoryComboBox, 1);
            Grid.SetColumn(CategoryComboBox, 0);
            Grid.SetColumnSpan(CategoryComboBox, 1);
            CategoryComboBox.Width = 210;
            CategoryComboBox.HorizontalAlignment = HorizontalAlignment.Left;

            Grid.SetRow(QueuedOnlyCheckBox, 1);
            Grid.SetColumn(QueuedOnlyCheckBox, 1);
            Grid.SetColumnSpan(QueuedOnlyCheckBox, 2);
            QueuedOnlyCheckBox.Margin = new Thickness(10, 0, 0, 0);

            QueueSummaryHotkeyHint.Visibility = Visibility.Collapsed;
            RootContentStackPanel.Padding = new Thickness(20, 16, 20, 24);
        }
        else
        {
            // Narrow layout (Below 560 px):
            // Stack search, category selector, and secondary controls vertically
            HeaderGrid.RowDefinitions[1].Height = GridLength.Auto;
            Grid.SetRow(HeaderActionsPanel, 1);
            Grid.SetColumn(HeaderActionsPanel, 0);
            Grid.SetColumnSpan(HeaderActionsPanel, 2);
            HeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            HeaderActionsPanel.Margin = new Thickness(0, 8, 0, 0);

            Grid.SetRow(SearchBoxContainer, 0);
            Grid.SetColumn(SearchBoxContainer, 0);
            Grid.SetColumnSpan(SearchBoxContainer, 3);

            Grid.SetRow(CategoryComboBox, 1);
            Grid.SetColumn(CategoryComboBox, 0);
            Grid.SetColumnSpan(CategoryComboBox, 3);
            CategoryComboBox.Width = double.NaN;
            CategoryComboBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            Grid.SetRow(QueuedOnlyCheckBox, 2);
            Grid.SetColumn(QueuedOnlyCheckBox, 0);
            Grid.SetColumnSpan(QueuedOnlyCheckBox, 3);
            QueuedOnlyCheckBox.Margin = new Thickness(0, 4, 0, 0);

            QueueSummaryHotkeyHint.Visibility = Visibility.Collapsed;
            RootContentStackPanel.Padding = new Thickness(16, 12, 16, 20);
        }
    }

    private void OnImportNoticeClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissImportNotice();
    }

    private async void OnRemoveMediaClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SelectableSourceItem item)
        {
            if (item.Source is Models.PowerPointSource)
            {
                ViewModel.RemovePowerPoint(item);
            }
            else
            {
                await ViewModel.RemoveMediaCommand.ExecuteAsync(item);
            }
        }
    }
}

