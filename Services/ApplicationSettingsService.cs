using System.Text.Json;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// File-backed persistent application settings manager storing JSON configuration under LocalAppData.
/// </summary>
public class ApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public ApplicationSettingsService(string? customSettingsPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customSettingsPath))
        {
            _settingsFilePath = customSettingsPath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "SwitchCast");
            _settingsFilePath = Path.Combine(appDir, "settings.json");
        }
    }

    public UserSettings CurrentSettings { get; private set; } = new();

    public event EventHandler<ApplicationThemeOption>? ThemeChanged;

    public async Task LoadSettingsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                CurrentSettings = new UserSettings();
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
            CurrentSettings = settings ?? new UserSettings();
        }
        catch
        {
            // Fall back cleanly to safe defaults on read/corruption failures
            CurrentSettings = new UserSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveSettingsAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(CurrentSettings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Silently suppress non-fatal local IO errors
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SetThemeAsync(ApplicationThemeOption theme)
    {
        if (CurrentSettings.Theme == theme)
        {
            return;
        }

        CurrentSettings.Theme = theme;
        ThemeChanged?.Invoke(this, theme);
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public async Task SetWindowDimensionsAsync(double width, double height)
    {
        if (!CurrentSettings.RememberWindowDimensions)
        {
            return;
        }

        CurrentSettings.WindowWidth = width;
        CurrentSettings.WindowHeight = height;
        await SaveSettingsAsync().ConfigureAwait(false);
    }
}
