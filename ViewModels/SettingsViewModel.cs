using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// ViewModel managing application theme configuration, preferences, presenter settings, shortcuts, and version metadata.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IApplicationSettingsService _settingsService;
    private readonly IHotkeyService? _hotkeyService;

    [ObservableProperty]
    private int _selectedCategoryIndex = 0; // 0=General, 1=Appearance, 2=Window, 3=Presenter, 4=Shortcuts

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private bool _rememberWindowDimensions;

    [ObservableProperty]
    private bool _enableGlobalHotkeys;

    [ObservableProperty]
    private bool _autoOpenPresenterDock;

    [ObservableProperty]
    private bool _dockAlwaysOnTop;

    [ObservableProperty]
    private bool _startDockInCompactMode;

    [ObservableProperty]
    private int _selectedSwitchModeIndex;

    [ObservableProperty]
    private string _shortcutSearchQuery = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<HotkeyBinding> _hotkeyBindings;

    public SettingsViewModel(IApplicationSettingsService settingsService, IHotkeyService? hotkeyService = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _hotkeyService = hotkeyService;

        var current = _settingsService.CurrentSettings;
        _selectedThemeIndex = (int)current.Theme;
        _rememberWindowDimensions = current.RememberWindowDimensions;
        _enableGlobalHotkeys = current.EnableGlobalHotkeys;
        _autoOpenPresenterDock = current.AutoOpenPresenterDock;
        _dockAlwaysOnTop = current.DockAlwaysOnTop;
        _startDockInCompactMode = current.StartDockInCompactMode;
        _selectedSwitchModeIndex = (int)current.SwitchMode;
        _hotkeyBindings = current.HotkeyBindings.ToList();
    }

    public string AppName => "SwitchCast";

    public string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string TargetFramework => ".NET 8.0 (WinUI 3 / Windows App SDK)";

    public string PlatformDescription => $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    public string AppDescription =>
        "A lightweight, privacy-first presentation and screen-sharing management utility for Windows desktop.";

    public Visibility GeneralCategoryVisibility => SelectedCategoryIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AppearanceCategoryVisibility => SelectedCategoryIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WindowCategoryVisibility => SelectedCategoryIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PresenterCategoryVisibility => SelectedCategoryIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShortcutsCategoryVisibility => SelectedCategoryIndex == 4 ? Visibility.Visible : Visibility.Collapsed;

    public IEnumerable<HotkeyBinding> FilteredHotkeyBindings
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ShortcutSearchQuery))
            {
                return HotkeyBindings;
            }

            return HotkeyBindings.Where(b =>
                b.Name.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                b.Description.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                b.DisplayString.Contains(ShortcutSearchQuery, StringComparison.OrdinalIgnoreCase));
        }
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(GeneralCategoryVisibility));
        OnPropertyChanged(nameof(AppearanceCategoryVisibility));
        OnPropertyChanged(nameof(WindowCategoryVisibility));
        OnPropertyChanged(nameof(PresenterCategoryVisibility));
        OnPropertyChanged(nameof(ShortcutsCategoryVisibility));
    }

    partial void OnShortcutSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredHotkeyBindings));
    }

    partial void OnHotkeyBindingsChanged(IReadOnlyList<HotkeyBinding> value)
    {
        OnPropertyChanged(nameof(FilteredHotkeyBindings));
    }

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

    async partial void OnEnableGlobalHotkeysChanged(bool value)
    {
        _settingsService.CurrentSettings.EnableGlobalHotkeys = value;
        _hotkeyService?.SetEnabled(value);
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnAutoOpenPresenterDockChanged(bool value)
    {
        _settingsService.CurrentSettings.AutoOpenPresenterDock = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnDockAlwaysOnTopChanged(bool value)
    {
        _settingsService.CurrentSettings.DockAlwaysOnTop = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnStartDockInCompactModeChanged(bool value)
    {
        _settingsService.CurrentSettings.StartDockInCompactMode = value;
        await _settingsService.SaveSettingsAsync();
    }

    async partial void OnSelectedSwitchModeIndexChanged(int value)
    {
        var mode = (PresenterSwitchMode)value;
        _settingsService.CurrentSettings.SwitchMode = mode;
        await _settingsService.SaveSettingsAsync();
    }

    [RelayCommand]
    public async Task ResetHotkeysToDefaultAsync()
    {
        var defaultHotkeys = UserSettings.GetDefaultHotkeys();
        _settingsService.CurrentSettings.HotkeyBindings = defaultHotkeys;
        HotkeyBindings = defaultHotkeys.ToList();
        _hotkeyService?.ReloadSettings();
        await _settingsService.SaveSettingsAsync();
    }

    [RelayCommand]
    public async Task ToggleHotkeyBindingAsync(HotkeyBinding? binding)
    {
        if (binding is null)
        {
            return;
        }

        binding.IsEnabled = !binding.IsEnabled;
        if (binding.IsEnabled)
        {
            _hotkeyService?.RegisterHotkey(binding);
        }
        else
        {
            _hotkeyService?.UnregisterHotkey(binding.Action);
        }

        HotkeyBindings = _settingsService.CurrentSettings.HotkeyBindings.ToList();
        await _settingsService.SaveSettingsAsync();
    }
}
