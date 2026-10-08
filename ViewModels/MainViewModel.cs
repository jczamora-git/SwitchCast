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

    public INavigationService NavigationService => _navigationService;
    public IPresentationStateService PresentationStateService => _presentationStateService;

    private void OnThemeChanged(object? sender, ApplicationThemeOption newTheme)
    {
        CurrentTheme = newTheme;
    }
}
