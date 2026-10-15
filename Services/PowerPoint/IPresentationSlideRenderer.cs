namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Result of a slide deck rendering operation.
/// </summary>
public record SlideDeckRenderResult
{
    /// <summary>
    /// Whether slide rendering succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Total number of slides rendered.
    /// </summary>
    public int SlideCount { get; init; }

    /// <summary>
    /// File paths of exported slide images, in 1-based slide order.
    /// </summary>
    public IReadOnlyList<string> SlideImagePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// User-friendly error message if rendering failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Pluggable abstraction for rendering PowerPoint (.pptx) presentation decks into slide image assets.
/// </summary>
public interface IPresentationSlideRenderer
{
    /// <summary>
    /// Whether the rendering engine is installed and available on this system.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Name or description of the rendering engine.
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// Renders slides from the given PPTX file into the target directory.
    /// </summary>
    Task<SlideDeckRenderResult> RenderDeckAsync(string pptxPath, string cacheDirectory, CancellationToken cancellationToken = default);
}
