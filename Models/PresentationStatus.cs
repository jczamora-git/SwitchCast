namespace SwitchCast.Models;

/// <summary>
/// Represents the high-level operational state of the presentation session.
/// </summary>
public enum PresentationStatus
{
    /// <summary>
    /// Presentation has not been started.
    /// </summary>
    Idle,

    /// <summary>
    /// Presentation session is initializing capture pipelines.
    /// </summary>
    Starting,

    /// <summary>
    /// Active capture source is currently rendering to presentation output.
    /// </summary>
    Active,

    /// <summary>
    /// Presentation is temporarily paused (frozen frame / placeholder).
    /// </summary>
    Paused,

    /// <summary>
    /// Presentation is blacked out (solid black screen).
    /// </summary>
    Blackout,

    /// <summary>
    /// An unrecoverable capture or rendering error occurred.
    /// </summary>
    Error
}
