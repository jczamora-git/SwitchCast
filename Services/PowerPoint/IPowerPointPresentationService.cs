using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Service managing imported PowerPoint presentations, slide cache loading, and slide navigation.
/// </summary>
public interface IPowerPointPresentationService
{
    /// <summary>
    /// Active PowerPoint presentation source currently loaded.
    /// </summary>
    PowerPointSource? ActivePowerPointSource { get; }

    /// <summary>
    /// 1-based index of the currently displayed slide.
    /// </summary>
    int CurrentSlideIndex { get; }

    /// <summary>
    /// Total number of slides in the active deck.
    /// </summary>
    int TotalSlideCount { get; }

    /// <summary>
    /// Local file path of the currently displayed slide image.
    /// </summary>
    string? CurrentSlideImagePath { get; }

    /// <summary>
    /// WinUI ImageSource representation of the currently displayed slide image.
    /// </summary>
    ImageSource? CurrentSlideImageSource { get; }

    /// <summary>
    /// Last error message encountered during presentation import or slide loading.
    /// </summary>
    string? LastErrorMessage { get; }

    /// <summary>
    /// Raised whenever the current slide advances, recedes, or changes.
    /// </summary>
    event EventHandler? SlideChanged;

    /// <summary>
    /// Imports a local .pptx file, renders slides if necessary, and returns a configured PowerPointSource.
    /// </summary>
    Task<PowerPointSource> ImportPresentationAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the specified PowerPointSource into the active presentation pipeline.
    /// </summary>
    Task LoadPresentationAsync(PowerPointSource source);

    /// <summary>
    /// Advances to the next slide in the active deck.
    /// </summary>
    Task<bool> NextSlideAsync();

    /// <summary>
    /// Navigates to the previous slide in the active deck.
    /// </summary>
    Task<bool> PreviousSlideAsync();

    /// <summary>
    /// Jumps directly to a specific 1-based slide index.
    /// </summary>
    Task<bool> GoToSlideAsync(int slideIndex);

    /// <summary>
    /// Clears and unloads the active presentation deck.
    /// </summary>
    void ClearPresentation();
}
