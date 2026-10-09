namespace SwitchCast.Models;

/// <summary>
/// Defines the display presentation mode of the Presentation Output Window.
/// </summary>
public enum PresentationDisplayMode
{
    /// <summary>
    /// Normal windowed presentation with custom title bar and window chrome.
    /// </summary>
    Windowed,

    /// <summary>
    /// True fullscreen presentation occupying the entire monitor without borders, title bar, or controls.
    /// </summary>
    Fullscreen
}
