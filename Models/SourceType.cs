namespace SwitchCast.Models;

/// <summary>
/// Defines the category of capture source.
/// </summary>
public enum SourceType
{
    /// <summary>
    /// Running application desktop window.
    /// </summary>
    Window,

    /// <summary>
    /// Connected physical or virtual display monitor.
    /// </summary>
    Display,

    /// <summary>
    /// Direct local static image file.
    /// </summary>
    Image,

    /// <summary>
    /// Direct local video media file.
    /// </summary>
    Video
}
