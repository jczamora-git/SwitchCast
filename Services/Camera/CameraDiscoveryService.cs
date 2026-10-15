using System.Diagnostics;
using Windows.Devices.Enumeration;
using SwitchCast.Models;

namespace SwitchCast.Services.Camera;

/// <summary>
/// Native Windows device enumeration service for discovering integrated and USB camera devices.
/// </summary>
public sealed class CameraDiscoveryService : ICameraDiscoveryService, IDisposable
{
    private DeviceWatcher? _deviceWatcher;
    private bool _disposed;

    public CameraDiscoveryService()
    {
        InitializeWatcher();
    }

    public event EventHandler? CamerasChanged;

    private void InitializeWatcher()
    {
        try
        {
            _deviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
            _deviceWatcher.Added += (s, e) => CamerasChanged?.Invoke(this, EventArgs.Empty);
            _deviceWatcher.Removed += (s, e) => CamerasChanged?.Invoke(this, EventArgs.Empty);
            _deviceWatcher.Updated += (s, e) => CamerasChanged?.Invoke(this, EventArgs.Empty);
            _deviceWatcher.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraDiscoveryService] DeviceWatcher start error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<CameraSource>> EnumerateCamerasAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<CameraSource>();

        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cancellationToken).ConfigureAwait(false);

            int displayIndex = 1;
            foreach (var device in devices)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                bool isInternal = device.EnclosureLocation is not null &&
                    (device.EnclosureLocation.Panel == Panel.Front || device.EnclosureLocation.Panel == Panel.Back);
                string friendlyTitle = !string.IsNullOrWhiteSpace(device.Name)
                    ? device.Name
                    : $"Camera {displayIndex}";

                if (isInternal && !friendlyTitle.Contains("Integrated", StringComparison.OrdinalIgnoreCase))
                {
                    friendlyTitle = $"{friendlyTitle} (Integrated)";
                }

                result.Add(new CameraSource
                {
                    Id = $"cam_{device.Id.GetHashCode():x8}",
                    DeviceId = device.Id,
                    Title = friendlyTitle,
                    IsInternal = isInternal,
                    IsAvailable = device.IsEnabled
                });

                displayIndex++;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraDiscoveryService] EnumerateCamerasAsync error: {ex.Message}");
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_deviceWatcher is not null &&
                (_deviceWatcher.Status == DeviceWatcherStatus.Started || _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                _deviceWatcher.Stop();
            }
        }
        catch { }
    }
}
