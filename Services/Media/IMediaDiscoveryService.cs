using SwitchCast.Models;

namespace SwitchCast.Services.Media;

/// <summary>
/// Service responsible for discovering, validating, and extracting metadata from local media files.
/// </summary>
public interface IMediaDiscoveryService
{
    /// <summary>
    /// Checks whether the specified file extension is a supported image format.
    /// </summary>
    bool IsSupportedImage(string filePathOrExtension);

    /// <summary>
    /// Checks whether the specified file extension is a supported video format.
    /// </summary>
    bool IsSupportedVideo(string filePathOrExtension);

    /// <summary>
    /// Asynchronously creates and validates a MediaFileSource (ImageMediaSource or VideoMediaSource) from a local file path.
    /// </summary>
    Task<MediaFileSource?> CreateMediaSourceFromFileAsync(string filePath);

    /// <summary>
    /// Asynchronously loads a list of persisted media file paths and reconciles availability.
    /// </summary>
    Task<IReadOnlyList<MediaFileSource>> LoadPersistedMediaAsync(IEnumerable<string> paths);
}
