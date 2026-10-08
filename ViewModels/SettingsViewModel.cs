using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing application theme configuration, preferences, and version metadata.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IApplicationSettingsService _settingsService;

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _rememberWindowDimensions;

    public SettingsViewModel(IApplicationSettingsService settingsService)
    {
        _settingsService = settingsService;

        _selectedThemeIndex = (int)_settingsService.CurrentSettings.Theme;
        _rememberWindowDimensions = _settingsService.CurrentSettings.RememberWindowDimensions;
    }

    public string AppName => "SwitchCast";

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string TargetFramework => ".NET 8.0 (WinUI 3 / Windows App SDK)";

    public string PlatformDescription => $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    public string AppDescription =>
        "A lightweight, privacy-first presentation and screen-sharing management utility for Windows desktop.";

    async partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = (ApplicationThemeOption)value;
        await _settingsService.SetThemeAsync(theme);
    }

    async partial void OnRememberWindowDimensionsChanged(bool value)
    {
        _settingsService.CurrentSettings.RememberWindowDimensions = value;
        await _settingsService.SaveSettingsAsync();
    }
}
