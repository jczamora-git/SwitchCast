namespace Microsoft.UI.Xaml
{
    /// <summary>
    /// Stubs Microsoft.UI.Xaml.Visibility for unit testing ViewModels in headless test runner.
    /// </summary>
    public enum Visibility
    {
        Visible = 0,
        Collapsed = 1
    }
}

namespace Microsoft.UI.Xaml.Media
{
    /// <summary>
    /// Stubs Microsoft.UI.Xaml.Media.ImageSource for unit testing ViewModels.
    /// </summary>
    public class ImageSource
    {
    }
}

namespace Microsoft.UI.Xaml.Media.Imaging
{
    /// <summary>
    /// Stubs Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource for headless test runner.
    /// </summary>
    public class SoftwareBitmapSource : ImageSource, IDisposable
    {
        public Task SetBitmapAsync(Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Stubs Microsoft.UI.Xaml.Media.Imaging.BitmapImage for headless test runner.
    /// </summary>
    public class BitmapImage : ImageSource
    {
        public Uri? UriSource { get; set; }

        public BitmapImage() { }

        public BitmapImage(Uri uriSource)
        {
            UriSource = uriSource;
        }
    }
}

namespace Microsoft.UI.Dispatching
{
    /// <summary>
    /// Stubs Microsoft.UI.Dispatching.DispatcherQueue for headless test runner.
    /// </summary>
    public class DispatcherQueue
    {
        public static DispatcherQueue? GetForCurrentThread() => null;
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action action)
        {
            action();
            return true;
        }
    }
}


