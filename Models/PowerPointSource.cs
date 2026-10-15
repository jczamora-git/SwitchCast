namespace SwitchCast.Models;

/// <summary>
/// Domain model representing an imported PowerPoint slide deck presentation source (.pptx).
/// Supports both static slide rendering and native PowerPoint Live slideshow tracking.
/// </summary>
public record PowerPointSource : CaptureSource
{
    public PowerPointSource()
    {
        Type = SourceType.PowerPoint;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PowerPointSource(string filePath)
    {
        Id = $"ppt_{Guid.NewGuid():N}";
        Title = System.IO.Path.GetFileName(filePath);
        Type = SourceType.PowerPoint;
        FilePath = filePath;
    }

    /// <summary>
    /// Absolute local file path to the .pptx presentation file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Total number of slides discovered or rendered in the presentation deck.
    /// </summary>
    public int SlideCount { get; init; }

    /// <summary>
    /// Current 1-based slide index being presented (1 to SlideCount).
    /// </summary>
    public int CurrentSlideIndex { get; init; } = 1;

    /// <summary>
    /// Ordered list of cached image file paths for static slide rendering.
    /// </summary>
    public IReadOnlyList<string> SlideImagePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether this PowerPoint source is running in PowerPoint Live mode or Static Slide mode.
    /// </summary>
    public bool IsLiveSlideshow { get; init; }

    /// <summary>
    /// Human-readable presentation progress string (e.g. "Slide 3 of 24").
    /// </summary>
    public string SlideProgressText => SlideCount > 0 ? $"Slide {CurrentSlideIndex} of {SlideCount}" : "Slide deck";

    /// <summary>
    /// Resolves the file path of the currently active static slide image, or null if none available.
    /// </summary>
    public string? CurrentSlideImagePath
    {
        get
        {
            if (SlideImagePaths is null || SlideImagePaths.Count == 0)
            {
                return null;
            }

            int index = Math.Clamp(CurrentSlideIndex - 1, 0, SlideImagePaths.Count - 1);
            return SlideImagePaths[index];
        }
    }
}
