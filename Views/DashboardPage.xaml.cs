using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SwitchCast.ViewModels;

namespace SwitchCast.Views;

/// <summary>
/// Control Dashboard View presenting session status, workspace preview, and quick actions.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
    }

    public DashboardViewModel ViewModel { get; }
}
