using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchCast.Models;
using SwitchCast.Services;

namespace SwitchCast.ViewModels;

/// <summary>
/// Root ViewModel orchestrating shell navigation and window state.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IPresentationStateService _presentationStateService;

    [ObservableProperty]
    private string _windowTitle = "SwitchCast — Screen Sharing Manager";

    [ObservableProperty]
    private ApplicationThemeOption _currentTheme = ApplicationThemeOption.System;

    public MainViewModel(
        INavigationService navigationService,
        IApplicationSettingsService settingsService,
        IPresentationStateService presentationStateService)
    {
        _navigationService = navigationService;
        _settingsService = settingsService;
        _presentationStateService = presentationStateService;

        _currentTheme = _settingsService.CurrentSettings.Theme;
        _settingsService.ThemeChanged += OnThemeChanged;
    }

    public const string CoffeeUrl = "https://paypal.me/itsjczamora";

    public INavigationService NavigationService => _navigationService;
    public IPresentationStateService PresentationStateService => _presentationStateService;

    [RelayCommand]
    public async Task OpenCoffeeLinkAsync()
    {
        try
        {
            var uri = new Uri(CoffeeUrl);
            bool launched = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!launched)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CoffeeUrl) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to open coffee link: {ex.Message}");
        }
    }

    private void OnThemeChanged(object? sender, ApplicationThemeOption newTheme)
    {
        CurrentTheme = newTheme;
    }
}
