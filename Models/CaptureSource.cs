namespace SwitchCast.Models;

/// <summary>
/// Abstract base domain model for any capturable presentation source (window or monitor).
/// </summary>
public abstract record CaptureSource
{
    /// <summary>
    /// Unique identifier for this capture source instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable title or label of the source.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The category of capture source.
    /// </summary>
    public SourceType Type { get; init; }

    /// <summary>
    /// Whether this source is currently available and valid for capture or presentation.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    /// <summary>
    /// Segoe Fluent icon glyph associated with this source type.
    /// </summary>
    public string TypeGlyph => Type switch
    {
        SourceType.Window => "\uE7F4",
        SourceType.Display => "\uE790",
        SourceType.Image => "\uEB9F",
        SourceType.Video => "\uE714",
        _ => "\uE7F4"
    };

    /// <summary>
    /// Friendly category label for the source type.
    /// </summary>
    public string CategoryLabel => Type switch
    {
        SourceType.Window => "Window",
        SourceType.Display => "Display",
        SourceType.Image => "Image",
        SourceType.Video => "Video",
        _ => "Source"
    };
}
