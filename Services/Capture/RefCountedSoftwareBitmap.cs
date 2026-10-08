using Windows.Graphics.Imaging;

namespace SwitchCast.Services.Capture;

/// <summary>
/// Thread-safe reference-counted wrapper around a WinRT SoftwareBitmap ensuring deterministic disposal
/// across multiple asynchronous UI and capture consumers without premature memory release or leaks.
/// </summary>
public sealed class RefCountedSoftwareBitmap : IDisposable
{
    private readonly SoftwareBitmap _bitmap;
    private int _refCount;
    private int _isDisposed;

    /// <summary>
    /// Initializes a new instance of <see cref="RefCountedSoftwareBitmap"/> with the specified initial reference count.
    /// </summary>
    /// <param name="bitmap">The underlying WinRT SoftwareBitmap.</param>
    /// <param name="initialRefCount">The initial reference count (typically 1 for the creator).</param>
    public RefCountedSoftwareBitmap(SoftwareBitmap bitmap, int initialRefCount = 1)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        if (initialRefCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRefCount), "Initial reference count must be greater than zero.");
        }

        _refCount = initialRefCount;
    }

    /// <summary>
    /// The underlying WinRT SoftwareBitmap.
    /// </summary>
    public SoftwareBitmap Bitmap => _bitmap;

    /// <summary>
    /// Width of the bitmap in pixels.
    /// </summary>
    public int PixelWidth => _bitmap.PixelWidth;

    /// <summary>
    /// Height of the bitmap in pixels.
    /// </summary>
    public int PixelHeight => _bitmap.PixelHeight;

    /// <summary>
    /// Gets the current reference count.
    /// </summary>
    public int RefCount => Volatile.Read(ref _refCount);

    /// <summary>
    /// Attempts to increment the reference count. Returns false if the object has already been fully released.
    /// </summary>
    public bool TryAddRef()
    {
        while (true)
        {
            int current = Volatile.Read(ref _refCount);
            if (current <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Decrements the reference count and disposes the underlying SoftwareBitmap when the count reaches zero.
    /// </summary>
    public void Release()
    {
        int remaining = Interlocked.Decrement(ref _refCount);
        if (remaining == 0)
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                try
                {
                    _bitmap.Dispose();
                }
                catch
                {
                    // Ignore transient disposal exceptions
                }
            }
        }
        else if (remaining < 0)
        {
            // Already released and disposed
        }
    }

    /// <summary>
    /// Decrements the reference count (equivalent to <see cref="Release"/>).
    /// </summary>
    public void Dispose()
    {
        Release();
    }
}
