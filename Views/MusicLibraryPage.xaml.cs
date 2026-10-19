using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SwitchCast.Models.Music;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Dedicated Music &amp; Playlists page for managing background music, audio imports, and playback transport.
/// </summary>
public sealed partial class MusicLibraryPage : Page
{
    public MusicLibraryPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<MusicLibraryViewModel>();
        ViewModel.SetDispatcherQueue(DispatcherQueue);
    }

    public MusicLibraryViewModel ViewModel { get; }

    private async void OnPlayTrackClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && (fe.DataContext is MusicTrack track || (fe as Button)?.Tag is MusicTrack track2 && (track = track2) != null))
        {
            await ViewModel.PlayTrackAsync(track);
        }
    }

    private void OnMoveTrackUpClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && (fe.DataContext is MusicTrack track || (fe as Button)?.Tag is MusicTrack track2 && (track = track2) != null))
        {
            ViewModel.MoveTrackUp(track);
        }
    }

    private void OnMoveTrackDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && (fe.DataContext is MusicTrack track || (fe as Button)?.Tag is MusicTrack track2 && (track = track2) != null))
        {
            ViewModel.MoveTrackDown(track);
        }
    }

    private void OnRemoveTrackClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && (fe.DataContext is MusicTrack track || (fe as Button)?.Tag is MusicTrack track2 && (track = track2) != null))
        {
            ViewModel.RemoveTrack(track);
        }
    }

    private async void OnRenamePlaylistClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlaylist is null || XamlRoot is null)
        {
            return;
        }

        var textBox = new TextBox
        {
            Text = ViewModel.SelectedPlaylist.Name,
            PlaceholderText = "Playlist name",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            Title = "Rename Playlist",
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = textBox,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            ViewModel.RenamePlaylist(textBox.Text);
        }
    }

    private void OnSeekSliderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.StartScrubbing();
    }

    private void OnSeekSliderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Slider slider)
        {
            ViewModel.CompleteScrubbing(slider.Value);
        }
    }
}
