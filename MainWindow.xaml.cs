using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using SwitchCast.Views;
using WinRT.Interop;

namespace SwitchCast;

/// <summary>
/// Primary desktop application shell hosting navigation and view transitions.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly INavigationService _navigationService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly IPresentationCoordinator _presentationCoordinator;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Current.Services.GetRequiredService<MainViewModel>();
        _navigationService = App.Current.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Current.Services.GetRequiredService<IApplicationSettingsService>();
        _lifecycleService = App.Current.Services.GetRequiredService<IApplicationLifecycleService>();
        _presentationCoordinator = App.Current.Services.GetRequiredService<IPresentationCoordinator>();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _navigationService.Navigated += OnNavigationServiceNavigated;

        InitializeAppWindow();
        ApplyTheme(_viewModel.CurrentTheme);
    }

    public MainViewModel ViewModel => _viewModel;

    private void InitializeAppWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            _appWindow.Title = _viewModel.WindowTitle;

            var settings = _settingsService.CurrentSettings;
            var width = (int)Math.Max(800, settings.WindowWidth);
            var height = (int)Math.Max(600, settings.WindowHeight);

            _appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            _appWindow.Closing += OnAppWindowClosing;

            UpdateTitleBarColors(_viewModel.CurrentTheme);
        }
    }

    private void OnNavViewLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Initialize(ContentFrame);
        _navigationService.NavigateTo<DashboardPage>();
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void OnNavViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _navigationService.NavigateTo<SettingsPage>();
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    _navigationService.NavigateTo<DashboardPage>();
                    break;
                case "Sources":
                    _navigationService.NavigateTo<SourcesPage>();
                    break;
            }
        }
    }

    private void OnNavigationServiceNavigated(object? sender, Type pageType)
    {
        if (pageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
        }
        else if (pageType == typeof(DashboardPage))
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
        else if (pageType == typeof(SourcesPage))
        {
            NavView.SelectedItem = NavView.MenuItems[1];
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentTheme))
        {
            ApplyTheme(_viewModel.CurrentTheme);
        }
        else if (e.PropertyName == nameof(MainViewModel.WindowTitle) && _appWindow is not null)
        {
            _appWindow.Title = _viewModel.WindowTitle;
        }
    }

    private void ApplyTheme(ApplicationThemeOption theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            ApplicationThemeOption.Light => ElementTheme.Light,
            ApplicationThemeOption.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        UpdateTitleBarColors(theme);
    }

    private void UpdateTitleBarColors(ApplicationThemeOption theme)
    {
        if (_appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        var isDark = theme == ApplicationThemeOption.Dark ||
                     (theme == ApplicationThemeOption.System && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 240);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(35, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(55, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
        }
        else
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 30);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(45, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 160, 160, 160);
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_lifecycleService.IsShutdownApproved)
        {
            // Shutdown has already been confirmed and authorized; allow closure to complete.
            return;
        }

        // Synchronously intercept and cancel window destruction to prompt the user.
        args.Cancel = true;

        if (_lifecycleService.IsExitConfirmationOpen)
        {
            // Prevent duplicate dialog prompts if user rapidly clicks X or presses Alt+F4.
            return;
        }

        _lifecycleService.IsExitConfirmationOpen = true;

        try
        {
            if (Content?.XamlRoot is null)
            {
                // Fallback if visual root is not accessible
                _lifecycleService.ApproveShutdown();
                await _lifecycleService.ExecuteShutdownAsync();
                Close();
                return;
            }

            bool isPresenting = _presentationCoordinator.IsLive ||
                                _presentationCoordinator.IsPaused ||
                                _presentationCoordinator.IsBlackout;

            var dialog = new ContentDialog
            {
                Title = "Exit SwitchCast?",
                Content = isPresenting
                    ? "Your live presentation will stop, and all SwitchCast windows will close. Are you sure you want to exit?"
                    : "Are you sure you want to exit SwitchCast?",
                PrimaryButtonText = "Exit SwitchCast",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                _lifecycleService.ApproveShutdown();

                if (_appWindow is not null)
                {
                    var size = _appWindow.Size;
                    await _settingsService.SetWindowDimensionsAsync(size.Width, size.Height);
                }

                await _lifecycleService.ExecuteShutdownAsync();

                // Close MainWindow through the authorized exit path
                Close();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] Exit confirmation dialog error: {ex.Message}");
        }
        finally
        {
            _lifecycleService.IsExitConfirmationOpen = false;
        }
    }
}
