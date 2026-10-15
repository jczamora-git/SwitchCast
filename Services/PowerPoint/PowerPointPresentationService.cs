using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SwitchCast.Models;

namespace SwitchCast.Services.PowerPoint;

/// <summary>
/// Service managing imported PowerPoint presentations, slide cache loading, and slide navigation.
/// </summary>
public sealed class PowerPointPresentationService : IPowerPointPresentationService
{
    private readonly IPresentationSlideRenderer _slideRenderer;
    private ImageSource? _currentSlideImageSource;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public PowerPointPresentationService(IPresentationSlideRenderer? slideRenderer = null)
    {
        _slideRenderer = slideRenderer ?? new PowerPointComSlideRenderer();
    }

    public PowerPointSource? ActivePowerPointSource { get; private set; }

    public int CurrentSlideIndex => ActivePowerPointSource?.CurrentSlideIndex ?? 1;

    public int TotalSlideCount => ActivePowerPointSource?.SlideCount ?? 0;

    public string? CurrentSlideImagePath => ActivePowerPointSource?.CurrentSlideImagePath;

    public ImageSource? CurrentSlideImageSource => _currentSlideImageSource;

    public string? LastErrorMessage { get; private set; }

    public event EventHandler? SlideChanged;

    public async Task<PowerPointSource> ImportPresentationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            LastErrorMessage = $"Presentation file not found at '{filePath}'.";
            throw new FileNotFoundException(LastErrorMessage, filePath);
        }

        string fileName = Path.GetFileName(filePath);
        string cacheDirectory = GetDeckCacheDirectory(filePath);

        // Check if previously rendered and valid cache exists
        if (Directory.Exists(cacheDirectory))
        {
            var cachedImages = Directory.GetFiles(cacheDirectory, "slide_*.png")
                .OrderBy(f => ExtractSlideIndex(f))
                .ToList();

            if (cachedImages.Count > 0)
            {
                Debug.WriteLine($"[PowerPointPresentationService] Loaded {cachedImages.Count} slides from cache for {fileName}");
                return new PowerPointSource
                {
                    Id = $"pptx_{ComputeFileHash(filePath)}",
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    FilePath = filePath,
                    SlideCount = cachedImages.Count,
                    SlideImagePaths = cachedImages,
                    CurrentSlideIndex = 1,
                    IsAvailable = true
                };
            }
        }

        // Render slides via slide renderer
        var result = await _slideRenderer.RenderDeckAsync(filePath, cacheDirectory, cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            LastErrorMessage = result.ErrorMessage ?? "Failed to render PowerPoint slides.";
            Debug.WriteLine($"[PowerPointPresentationService] Render failure: {LastErrorMessage}");

            return new PowerPointSource
            {
                Id = $"pptx_{ComputeFileHash(filePath)}",
                Title = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                SlideCount = 0,
                SlideImagePaths = Array.Empty<string>(),
                CurrentSlideIndex = 1,
                IsAvailable = false
            };
        }

        return new PowerPointSource
        {
            Id = $"pptx_{ComputeFileHash(filePath)}",
            Title = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            SlideCount = result.SlideCount,
            SlideImagePaths = result.SlideImagePaths,
            CurrentSlideIndex = 1,
            IsAvailable = true
        };
    }

    public async Task LoadPresentationAsync(PowerPointSource source)
    {
        if (source is null)
        {
            ClearPresentation();
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            ActivePowerPointSource = source;
            await UpdateSlideImageAsync(source.CurrentSlideImagePath).ConfigureAwait(false);
            SlideChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> NextSlideAsync()
    {
        if (ActivePowerPointSource is null || CurrentSlideIndex >= TotalSlideCount)
        {
            return false;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            int nextIndex = CurrentSlideIndex + 1;
            ActivePowerPointSource = ActivePowerPointSource with { CurrentSlideIndex = nextIndex };
            await UpdateSlideImageAsync(ActivePowerPointSource.CurrentSlideImagePath).ConfigureAwait(false);
            SlideChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> PreviousSlideAsync()
    {
        if (ActivePowerPointSource is null || CurrentSlideIndex <= 1)
        {
            return false;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            int prevIndex = CurrentSlideIndex - 1;
            ActivePowerPointSource = ActivePowerPointSource with { CurrentSlideIndex = prevIndex };
            await UpdateSlideImageAsync(ActivePowerPointSource.CurrentSlideImagePath).ConfigureAwait(false);
            SlideChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> GoToSlideAsync(int slideIndex)
    {
        if (ActivePowerPointSource is null || TotalSlideCount == 0)
        {
            return false;
        }

        int clamped = Math.Clamp(slideIndex, 1, TotalSlideCount);
        if (clamped == CurrentSlideIndex)
        {
            return false;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            ActivePowerPointSource = ActivePowerPointSource with { CurrentSlideIndex = clamped };
            await UpdateSlideImageAsync(ActivePowerPointSource.CurrentSlideImagePath).ConfigureAwait(false);
            SlideChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void ClearPresentation()
    {
        ActivePowerPointSource = null;
        _currentSlideImageSource = null;
        SlideChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task UpdateSlideImageAsync(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            _currentSlideImageSource = null;
            return Task.CompletedTask;
        }

        try
        {
            // WinUI BitmapImage instantiation
            var bitmap = new BitmapImage(new Uri(imagePath));
            _currentSlideImageSource = bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PowerPointPresentationService] Failed to load slide bitmap: {ex.Message}");
            _currentSlideImageSource = null;
        }

        return Task.CompletedTask;
    }

    private static string GetDeckCacheDirectory(string filePath)
    {
        string hash = ComputeFileHash(filePath);
        return Path.Combine(Path.GetTempPath(), "SwitchCast", "PowerPointCache", hash);
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            string key = $"{filePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}";
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
        catch
        {
            return Math.Abs(filePath.GetHashCode()).ToString("x8");
        }
    }

    private static int ExtractSlideIndex(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath); // e.g. "slide_12"
        string[] parts = fileName.Split('_');
        if (parts.Length == 2 && int.TryParse(parts[1], out int index))
        {
            return index;
        }
        return 0;
    }
}
