using SwitchCast.Models;
using SwitchCast.Services.Camera;
using Xunit;

namespace SwitchCast.Tests.Services;

public class CameraPresentationTests
{
    [Fact]
    public void CameraSource_InitializesWithCorrectTypeAndGlyph()
    {
        var camera = new CameraSource
        {
            Id = "cam_integrated_01",
            Title = "Integrated Camera",
            DeviceId = @"\\?\USB#VID_04F2&PID_B6D9#5&28b03e23&0&5",
            IsInternal = true,
            Width = 1920,
            Height = 1080,
            IsAvailable = true
        };

        Assert.Equal(SourceType.Camera, camera.Type);
        Assert.Equal("\uE960", camera.TypeGlyph);
        Assert.Equal("Camera", camera.CategoryLabel);
        Assert.True(camera.IsInternal);
        Assert.Equal(1920, camera.Width);
        Assert.Equal(1080, camera.Height);
    }

    [Fact]
    public void CameraDiscoveryService_InstantiatesCleanly()
    {
        var discoveryService = new CameraDiscoveryService();
        Assert.NotNull(discoveryService);
    }

    [Fact]
    public void CameraSource_SupportsExternalUsbWebcamConfiguration()
    {
        var usbCamera = new CameraSource
        {
            Id = "cam_usb_logitech",
            Title = "Logitech Brio 4K",
            DeviceId = @"\\?\USB#VID_046D&PID_085E#6&12345678&0&1",
            IsInternal = false,
            Width = 3840,
            Height = 2160,
            IsAvailable = true
        };

        Assert.Equal(SourceType.Camera, usbCamera.Type);
        Assert.False(usbCamera.IsInternal);
        Assert.Equal(3840, usbCamera.Width);
        Assert.Equal(2160, usbCamera.Height);
    }
}
