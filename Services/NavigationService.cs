using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace SwitchCast.Services;

/// <summary>
/// Default implementation of the WinUI 3 Navigation Service.
/// </summary>
public class NavigationService : INavigationService
{
    private Frame? _frame;

    public Type? CurrentPageType => _frame?.CurrentSourcePageType;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public event EventHandler<Type>? Navigated;

    public void Initialize(object frame)
    {
        if (frame is Frame winUIFrame)
        {
            Initialize(winUIFrame);
        }
    }

    public void Initialize(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (_frame is not null)
        {
            _frame.Navigated -= OnFrameNavigated;
        }

        _frame = frame;
        _frame.Navigated += OnFrameNavigated;
    }

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        if (_frame is null)
        {
            return false;
        }

        if (_frame.CurrentSourcePageType == pageType)
        {
            return false;
        }

        return _frame.Navigate(pageType, parameter);
    }

    public bool NavigateTo<T>(object? parameter = null) where T : class
    {
        return NavigateTo(typeof(T), parameter);
    }

    public bool GoBack()
    {
        if (_frame is not null && _frame.CanGoBack)
        {
            _frame.GoBack();
            return true;
        }

        return false;
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType is not null)
        {
            Navigated?.Invoke(this, e.SourcePageType);
        }
    }
}
