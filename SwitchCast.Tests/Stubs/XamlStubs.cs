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
    /// Stubs Microsoft.UI.Dispatching.DispatcherQueueHandler for headless test runner.
    /// </summary>
    public delegate void DispatcherQueueHandler();

    /// <summary>
    /// Stubs Microsoft.UI.Dispatching.DispatcherQueuePriority for headless test runner.
    /// </summary>
    public enum DispatcherQueuePriority
    {
        Low = -10,
        Normal = 0,
        High = 10
    }

    /// <summary>
    /// Stubs Microsoft.UI.Dispatching.DispatcherQueueTimer for headless test runner.
    /// </summary>
    public class DispatcherQueueTimer
    {
        public TimeSpan Interval { get; set; }
        public bool IsRepeating { get; set; }
        public bool IsRunning { get; private set; }

        public event Windows.Foundation.TypedEventHandler<DispatcherQueueTimer, object>? Tick;

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;

        public void RaiseTick()
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Stubs Microsoft.UI.Dispatching.DispatcherQueue for headless test runner.
    /// </summary>
    public class DispatcherQueue
    {
        public static DispatcherQueue? GetForCurrentThread() => null;
        public bool HasThreadAccess { get; set; } = true;

        public Func<DispatcherQueueHandler, bool>? EnqueueHandler { get; set; }

        public bool TryEnqueue(DispatcherQueueHandler callback)
        {
            if (EnqueueHandler != null)
            {
                return EnqueueHandler(callback);
            }

            callback();
            return true;
        }

        public bool TryEnqueue(DispatcherQueuePriority priority, DispatcherQueueHandler callback)
        {
            return TryEnqueue(callback);
        }

        public DispatcherQueueTimer CreateTimer() => new DispatcherQueueTimer();
    }
}


