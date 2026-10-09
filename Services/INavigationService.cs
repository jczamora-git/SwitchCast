namespace SwitchCast.Services;

/// <summary>
/// Service governing view navigation within the application shell frame.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Type of the currently navigated page.
    /// </summary>
    Type? CurrentPageType { get; }

    /// <summary>
    /// Whether navigation can go back in the history stack.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Fired when navigation to a page finishes.
    /// </summary>
    event EventHandler<Type>? Navigated;

    /// <summary>
    /// Initializes the navigation service with the host frame control.
    /// </summary>
    void Initialize(object frame);

    /// <summary>
    /// Navigates to a specific Page type.
    /// </summary>
    bool NavigateTo(Type pageType, object? parameter = null);

    /// <summary>
    /// Navigates to a specific Page by generic type parameter.
    /// </summary>
    bool NavigateTo<T>(object? parameter = null) where T : class;

    /// <summary>
    /// Navigates to the primary Control Dashboard view.
    /// </summary>
    bool NavigateToDashboard(object? parameter = null);

    /// <summary>
    /// Navigates back one step in the navigation stack.
    /// </summary>
    bool GoBack();
}
