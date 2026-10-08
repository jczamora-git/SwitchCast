using SwitchCast.Services.Capture;
using Windows.Graphics.Imaging;
using Xunit;

namespace SwitchCast.Tests.Services;

public class RefCountedSoftwareBitmapTests
{
    [Fact]
    public void Constructor_InitializesWithProvidedRefCount()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var refCounted = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 2);

        Assert.Equal(2, refCounted.RefCount);
        Assert.Equal(10, refCounted.PixelWidth);
        Assert.Equal(10, refCounted.PixelHeight);
        Assert.Same(rawBitmap, refCounted.Bitmap);
    }

    [Fact]
    public void Constructor_WhenInitialCountZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: -1));
    }

    [Fact]
    public void Constructor_WhenBitmapNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RefCountedSoftwareBitmap(null!));
    }

    [Fact]
    public void TryAddRef_WhenActive_IncrementsRefCountAndReturnsTrue()
    {
        using var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var refCounted = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        var success = refCounted.TryAddRef();

        Assert.True(success);
        Assert.Equal(2, refCounted.RefCount);
    }

    [Fact]
    public void Release_DecrementsRefCount_AndDisposesAtZero()
    {
        var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var refCounted = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 2);

        refCounted.Release();
        Assert.Equal(1, refCounted.RefCount);

        refCounted.Release();
        Assert.Equal(0, refCounted.RefCount);

        // After reaching 0, TryAddRef must fail
        var addResult = refCounted.TryAddRef();
        Assert.False(addResult);
    }

    [Fact]
    public void MultipleRelease_AfterZero_IsIdempotentAndSafe()
    {
        var rawBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 10, 10, BitmapAlphaMode.Premultiplied);
        var refCounted = new RefCountedSoftwareBitmap(rawBitmap, initialRefCount: 1);

        refCounted.Release();
        Assert.Equal(0, refCounted.RefCount);

        // Additional releases should not throw
        refCounted.Release();
        refCounted.Release();
        refCounted.Dispose();
    }
}
