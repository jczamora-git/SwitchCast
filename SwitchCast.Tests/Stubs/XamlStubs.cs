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
}
