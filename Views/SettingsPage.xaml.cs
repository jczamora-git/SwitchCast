using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Settings view for configuring theme, window properties, and viewing version diagnostics.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
    }

    public SettingsViewModel ViewModel { get; }
}
