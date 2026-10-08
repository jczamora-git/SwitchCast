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

    [Fact]
    public async Task GetIconForSourceAsync_WithValidProcessExecutablePath_ExtractsIconAndReusesCache()
    {
        using var service = new Win32WindowIconService();
        string explorerPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        var window1 = new WindowSource
        {
            Id = "win_exp_1",
            Title = "File Explorer",
            WindowHandle = 0x5678,
            ProcessId = 100,
            ProcessName = "explorer",
            ProcessPath = explorerPath
        };

        var window2 = new WindowSource
        {
            Id = "win_exp_2",
            Title = "Documents",
            WindowHandle = 0x5679,
            ProcessId = 100,
            ProcessName = "explorer",
            ProcessPath = explorerPath
        };

        if (System.IO.File.Exists(explorerPath))
        {
            var icon1 = await service.GetIconForSourceAsync(window1);
            Assert.NotNull(icon1);

            // Second window should hit the process icon cache
            var icon2 = await service.GetIconForSourceAsync(window2);
            Assert.NotNull(icon2);
            Assert.Same(icon1, icon2);
        }
    }

    [Fact]
    public async Task GetIconForSourceAsync_CancellationRequested_ReturnsNullSafely()
    {
        using var service = new Win32WindowIconService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var window = new WindowSource
        {
            Id = "win_cancel",
            Title = "Cancel App",
            WindowHandle = 0x9999,
            ProcessPath = "C:\\dummy\\app.exe"
        };

        var icon = await service.GetIconForSourceAsync(window, cts.Token);
        Assert.Null(icon);
    }
}

