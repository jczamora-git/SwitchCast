using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class WindowActivationServiceTests
{
    [Fact]
    public void ActivateSource_NullSource_ReturnsFalse()
    {
        var service = new Win32WindowActivationService();
        var result = service.ActivateSource(null);
        Assert.False(result);
    }

    [Fact]
    public void ActivateSource_MonitorSource_ReturnsFalseWithoutThrowing()
    {
        var service = new Win32WindowActivationService();
        var monitor = new MonitorSource
        {
            Id = "mon-1",
            Title = "Display 1",
            DeviceName = @"\\.\DISPLAY1",
            MonitorHandle = 0x1234
        };

        var result = service.ActivateSource(monitor);
        Assert.False(result);
    }

    [Fact]
    public void ActivateSource_WindowSourceWithZeroHandle_ReturnsFalse()
    {
        var service = new Win32WindowActivationService();
        var window = new WindowSource
        {
            Id = "win-0",
            Title = "Invalid Window",
            WindowHandle = IntPtr.Zero
        };

        var result = service.ActivateSource(window);
        Assert.False(result);
    }

    [Fact]
    public void ActivateWindow_ZeroHandle_ReturnsFalse()
    {
        var service = new Win32WindowActivationService();
        var result = service.ActivateWindow(IntPtr.Zero);
        Assert.False(result);
    }
}
