using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class WindowIconServiceTests
{
    [Fact]
    public async Task GetIconForSourceAsync_NullSource_ReturnsNull()
    {
        using var service = new Win32WindowIconService();
        var icon = await service.GetIconForSourceAsync(null!);
        Assert.Null(icon);
    }

    [Fact]
    public async Task GetIconForSourceAsync_MonitorSource_ReturnsNullWithoutThrowing()
    {
        using var service = new Win32WindowIconService();
        var monitor = new MonitorSource
        {
            Id = "mon_1",
            Title = "Primary Display",
            DeviceName = @"\\.\DISPLAY1",
            MonitorHandle = 0x1234
        };

        var icon = await service.GetIconForSourceAsync(monitor);
        Assert.Null(icon);
    }

    [Fact]
    public async Task GetIconForSourceAsync_WindowSourceWithZeroHandle_ReturnsNull()
    {
        using var service = new Win32WindowIconService();
        var window = new WindowSource
        {
            Id = "win_0",
            Title = "Invalid Window",
            WindowHandle = IntPtr.Zero
        };

        var icon = await service.GetIconForSourceAsync(window);
        Assert.Null(icon);
    }

    [Fact]
    public void Invalidate_And_ClearCache_DoNotThrow()
    {
        using var service = new Win32WindowIconService();
        service.Invalidate("test_source");
        service.Invalidate(string.Empty);
        service.ClearCache();
        service.Dispose();
    }
}
