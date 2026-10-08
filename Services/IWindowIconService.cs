using Microsoft.UI.Xaml.Media;
using SwitchCast.Models;

namespace SwitchCast.Services;

/// <summary>
/// Service responsible for extracting and caching native Windows application and window icons.
/// </summary>
public interface IWindowIconService : IDisposable
{
    /// <summary>
    /// Resolves and returns the native application icon as an ImageSource for the specified capture source.
    /// Returns null if the source is not a window or if icon extraction fails.
    /// </summary>
    Task<ImageSource?> GetIconForSourceAsync(CaptureSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes cached icon entry for a specific source ID.
    /// </summary>
    void Invalidate(string sourceId);

    /// <summary>
    /// Clears all cached icons and releases associated resources.
    /// </summary>
    void ClearCache();
}
