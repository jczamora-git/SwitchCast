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

    /// <summary>
    /// Updates window position coordinates and persists if enabled.
    /// </summary>
    Task SetWindowPositionAsync(int x, int y);

    /// <summary>
    /// Updates both window dimensions and position and persists if enabled.
    /// </summary>
    Task SetWindowPlacementAsync(double width, double height, int? x, int? y);

    /// <summary>
    /// Updates whether the presentation cursor is visible in presentation output.
    /// </summary>
    Task SetShowPresentationCursorAsync(bool show);

    /// <summary>
    /// Updates media video audio playback volume and persists to storage.
    /// </summary>
    Task SetMediaVolumeAsync(double volume);

    /// <summary>
    /// Updates media video audio mute state and persists to storage.
    /// </summary>
    Task SetMediaMutedAsync(bool isMuted);

    /// <summary>
    /// Updates background music audio playback volume and persists to storage.
    /// </summary>
    Task SetBackgroundMusicVolumeAsync(double volume);

    /// <summary>
    /// Updates background music audio mute state and persists to storage.
    /// </summary>
    Task SetBackgroundMusicMutedAsync(bool isMuted);
}
