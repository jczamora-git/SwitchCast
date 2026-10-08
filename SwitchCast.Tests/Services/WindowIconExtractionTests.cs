using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class WindowIconExtractionTests
{
    [Fact]
    public async Task Test_ExplorerExe_ExtractsPixelDataSuccessfully()
    {
        string explorerPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

        Assert.True(System.IO.File.Exists(explorerPath), "explorer.exe must exist");

        var window = new WindowSource
        {
            Id = "test_explorer",
            Title = "File Explorer",
            WindowHandle = 0x1234,
            ProcessName = "explorer",
            ProcessPath = explorerPath,
            ProcessId = 1
        };

        using var service = new Win32WindowIconService();
        var icon = await service.GetIconForSourceAsync(window);

        Assert.NotNull(icon);
    }
}
