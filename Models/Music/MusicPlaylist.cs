using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SwitchCast.Models.Music;

/// <summary>
/// Represents a named collection of background music tracks.
/// </summary>
public partial class MusicPlaylist : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "New Playlist";

    public ObservableCollection<MusicTrack> Tracks { get; set; } = [];

    public TimeSpan TotalDuration => TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks));

    public int TrackCount => Tracks.Count;

    public string SummaryText => $"{TrackCount} {(TrackCount == 1 ? "track" : "tracks")} • {(TotalDuration.TotalHours >= 1 ? TotalDuration.ToString(@"h\:mm\:ss") : TotalDuration.ToString(@"m\:ss"))}";

    public void NotifyTracksChanged()
    {
        OnPropertyChanged(nameof(TotalDuration));
        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(SummaryText));
    }
}
