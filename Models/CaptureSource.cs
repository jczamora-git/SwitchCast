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
    /// Whether this source is currently available and valid for capture.
    /// </summary>
    public bool IsAvailable { get; init; } = true;
}
