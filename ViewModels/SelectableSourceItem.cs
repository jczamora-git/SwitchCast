using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using SwitchCast.Models;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel wrapping a discoverable CaptureSource (Window, Display, Image, Video) with interactive selection state.
/// </summary>
public partial class SelectableSourceItem : ObservableObject
{
    private readonly Action<SelectableSourceItem, bool>? _onSelectionChanged;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource? _iconSource;

    [ObservableProperty]
    private bool _hasIconSource;

    public bool HasNoIconSource => !HasIconSource;

    public SelectableSourceItem(
        CaptureSource source,
        bool isInitiallySelected = false,
        Action<SelectableSourceItem, bool>? onSelectionChanged = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _isSelected = isInitiallySelected;
        _isAvailable = source.IsAvailable;
        _onSelectionChanged = onSelectionChanged;
    }

    public CaptureSource Source { get; }

    public string Id => Source.Id;

    public string Title => Source.Title;

    public SourceType Type => Source.Type;

    public string TypeGlyph => Source.TypeGlyph;

    public string CategoryLabel => Source.CategoryLabel;

    public bool IsMediaSource => Source is MediaFileSource;

    public string? FilePath => (Source as MediaFileSource)?.FilePath;

    public Visibility UnavailableBadgeVisibility => IsAvailable ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RemoveButtonVisibility => IsMediaSource ? Visibility.Visible : Visibility.Collapsed;

    public string Subtitle => Source switch
    {
        WindowSource w => string.IsNullOrWhiteSpace(w.ProcessName)
            ? $"Window Handle: 0x{w.WindowHandle:X}"
            : $"Process: {w.ProcessName} (PID: {w.ProcessId})",
        MonitorSource m => $"Resolution: {m.Width} × {m.Height} • {(m.IsPrimary ? "Primary Display" : "Secondary Display")}",
        ImageMediaSource img => (img.Width.HasValue && img.Height.HasValue)
            ? $"Image • {img.Width} × {img.Height} • {img.FormattedFileSize}"
            : $"Image • {img.FormattedFileSize}",
        VideoMediaSource vid => (vid.Width.HasValue && vid.Height.HasValue)
            ? $"Video • {vid.FormattedDuration} • {vid.Width} × {vid.Height} • {vid.FormattedFileSize}"
            : $"Video • {vid.FormattedDuration} • {vid.FormattedFileSize}",
        _ => string.Empty
    };

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke(this, value);
    }

    partial void OnIsAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(UnavailableBadgeVisibility));
    }

    partial void OnIconSourceChanged(Microsoft.UI.Xaml.Media.ImageSource? value)
    {
        HasIconSource = value is not null;
    }

    partial void OnHasIconSourceChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoIconSource));
    }
}
