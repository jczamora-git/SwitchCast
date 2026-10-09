using System.Diagnostics;
using SwitchCast.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace SwitchCast.Services.Media;

/// <summary>
/// Implementation of media discovery and metadata extraction using Windows imaging and storage APIs.
/// </summary>
public class MediaDiscoveryService : IMediaDiscoveryService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"
    };

    private static readonly HashSet<string> SupportedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".wmv", ".mov", ".avi", ".mkv"
    };

    public bool IsSupportedImage(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
        {
            return false;
        }

        var ext = Path.GetExtension(filePathOrExtension);
        if (string.IsNullOrEmpty(ext))
        {
            ext = filePathOrExtension.StartsWith('.') ? filePathOrExtension : "." + filePathOrExtension;
        }

        return SupportedImageExtensions.Contains(ext);
    }

    public bool IsSupportedVideo(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
        {
            return false;
        }

        var ext = Path.GetExtension(filePathOrExtension);
        if (string.IsNullOrEmpty(ext))
        {
            ext = filePathOrExtension.StartsWith('.') ? filePathOrExtension : "." + filePathOrExtension;
        }

        return SupportedVideoExtensions.Contains(ext);
    }

    public async Task<MediaFileSource?> CreateMediaSourceFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        var fileName = Path.GetFileName(normalizedPath);
        var id = $"media:{normalizedPath.ToLowerInvariant()}";
        bool exists = File.Exists(normalizedPath);

        long sizeBytes = 0;
        if (exists)
        {
            try
            {
                var fi = new FileInfo(normalizedPath);
                sizeBytes = fi.Length;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaDiscoveryService] FileInfo error: {ex.Message}");
            }
        }

        if (IsSupportedImage(normalizedPath))
        {
            int? width = null;
            int? height = null;

            if (exists)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(normalizedPath);
                    using var stream = await file.OpenReadAsync();
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    width = (int)decoder.PixelWidth;
                    height = (int)decoder.PixelHeight;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MediaDiscoveryService] Image metadata extraction failed for {fileName}: {ex.Message}");
                }
            }

            return new ImageMediaSource
            {
                Id = id,
                Title = fileName,
                FilePath = normalizedPath,
                FileSizeBytes = sizeBytes,
                Width = width,
                Height = height,
                IsAvailable = exists
            };
        }

        if (IsSupportedVideo(normalizedPath))
        {
            int? width = null;
            int? height = null;
            TimeSpan? duration = null;

            if (exists)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(normalizedPath);
                    var videoProps = await file.Properties.GetVideoPropertiesAsync();
                    if (videoProps is not null)
                    {
                        if (videoProps.Duration > TimeSpan.Zero)
                        {
                            duration = videoProps.Duration;
                        }
                        if (videoProps.Width > 0)
                        {
                            width = (int)videoProps.Width;
                        }
                        if (videoProps.Height > 0)
                        {
                            height = (int)videoProps.Height;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MediaDiscoveryService] Video metadata extraction failed for {fileName}: {ex.Message}");
                }
            }

            return new VideoMediaSource
            {
                Id = id,
                Title = fileName,
                FilePath = normalizedPath,
                FileSizeBytes = sizeBytes,
                Width = width,
                Height = height,
                Duration = duration,
                IsAvailable = exists
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<MediaFileSource>> LoadPersistedMediaAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new List<MediaFileSource>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                var mediaSource = await CreateMediaSourceFromFileAsync(path).ConfigureAwait(false);
                if (mediaSource is not null)
                {
                    result.Add(mediaSource);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaDiscoveryService] Failed loading persisted media '{path}': {ex.Message}");
            }
        }

        return result;
    }
}
