namespace SwitchCast.Models;

/// <summary>
/// Serializable local user settings and preferences.
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Preferred application UI theme.
    /// </summary>
    public ApplicationThemeOption Theme { get; set; } = ApplicationThemeOption.System;

    /// <summary>
    /// Last saved window width in DIPs.
    /// </summary>
    public double WindowWidth { get; set; } = 1000;

    /// <summary>
    /// Last saved window height in DIPs.
    /// </summary>
    public double WindowHeight { get; set; } = 700;

    /// <summary>
    /// Whether to restore window dimensions on startup.
    /// </summary>
    public bool RememberWindowDimensions { get; set; } = true;
}
