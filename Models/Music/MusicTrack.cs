using CommunityToolkit.Mvvm.ComponentModel;

namespace SwitchCast.Models.Music;

/// <summary>
/// Represents an individual audio track within a background music playlist.
/// </summary>
public partial class MusicTrack : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string? _artist;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MissingBadgeVisibility))]
    private bool _isAvailable = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayingIndicatorVisibility))]
    private bool _isCurrentlyPlaying;

    public Microsoft.UI.Xaml.Visibility MissingBadgeVisibility => IsAvailable ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Visibility PlayingIndicatorVisibility => IsCurrentlyPlaying ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string DurationText => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");

    public string FormattedDuration => DurationText;

    public string DisplayText => string.IsNullOrWhiteSpace(Artist)
        ? (string.IsNullOrWhiteSpace(Title) ? "Untitled Track" : Title)
        : $"{Title} — {Artist}";
}
