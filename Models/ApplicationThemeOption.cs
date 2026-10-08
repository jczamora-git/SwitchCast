namespace SwitchCast.Models;

/// <summary>
/// User selectable application theme options.
/// </summary>
public enum ApplicationThemeOption
{
    /// <summary>
    /// Follow the active Windows operating system theme.
    /// </summary>
    System = 0,

    /// <summary>
    /// Force WinUI 3 Light theme.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Force WinUI 3 Dark theme.
    /// </summary>
    Dark = 2
}
