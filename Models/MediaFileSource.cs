namespace SwitchCast.Models;

/// <summary>
/// Domain model representing a local media file presentation source (Image or Video).
/// </summary>
public abstract record MediaFileSource : CaptureSource
{
    /// <summary>
    /// Absolute local file path to the media file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Size of the media file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Pixel width of the media asset if available.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Pixel height of the media asset if available.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Formatted human-readable file size string (e.g., "2.4 MB").
    /// </summary>
    public string FormattedFileSize
    {
        get
        {
            if (FileSizeBytes < 1024)
            {
                return $"{FileSizeBytes} B";
            }
            if (FileSizeBytes < 1024 * 1024)
            {
                return $"{FileSizeBytes / 1024.0:F1} KB";
            }
            if (FileSizeBytes < 1024 * 1024 * 1024)
            {
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
            return $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}

/// <summary>
/// Domain model representing a static local image presentation source (PNG, JPG, BMP, etc.).
/// </summary>
public record ImageMediaSource : MediaFileSource
{
    public ImageMediaSource()
    {
        Type = SourceType.Image;
    }
}

/// <summary>
/// Domain model representing a local video presentation source (MP4, M4V, WMV, etc.).
/// </summary>
public record VideoMediaSource : MediaFileSource
{
    public VideoMediaSource()
    {
        Type = SourceType.Video;
    }

    /// <summary>
    /// Total duration of the video asset if available.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Whether this video loops playback automatically when it reaches the end.
    /// </summary>
    public bool IsLooping { get; init; } = false;

    /// <summary>
    /// Formatted human-readable duration string (e.g., "02:35").
    /// </summary>
    public string FormattedDuration => Duration.HasValue
        ? (Duration.Value.TotalHours >= 1
            ? Duration.Value.ToString(@"hh\:mm\:ss")
            : Duration.Value.ToString(@"mm\:ss"))
        : "--:--";
}
