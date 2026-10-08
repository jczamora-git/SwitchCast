using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service governing persistent local user preferences and theme configuration.
/// </summary>
public interface IApplicationSettingsService
{
    /// <summary>
    /// Current cached in-memory user settings.
    /// </summary>
    UserSettings CurrentSettings { get; }

    /// <summary>
    /// Event fired whenever the application theme preference is changed.
    /// </summary>
    event EventHandler<ApplicationThemeOption>? ThemeChanged;

    /// <summary>
    /// Loads settings from local storage. If missing or corrupted, initializes safe defaults.
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    /// Persists current settings to local storage asynchronously.
    /// </summary>
    Task SaveSettingsAsync();

    /// <summary>
    /// Updates the theme option and triggers notification / persistence.
    /// </summary>
    Task SetThemeAsync(ApplicationThemeOption theme);

    /// <summary>
    /// Updates window dimensions and persists if enabled.
    /// </summary>
    Task SetWindowDimensionsAsync(double width, double height);
}
