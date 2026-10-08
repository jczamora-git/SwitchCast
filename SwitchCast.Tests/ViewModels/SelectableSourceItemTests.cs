using SwitchCast.Models;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SelectableSourceItemTests
{
    [Fact]
    public void Construction_ExtractsWindowMetadataCorrectly()
    {
        var window = new WindowSource
        {
            Id = "win-1",
            Title = "Visual Studio Code",
            ProcessName = "Code",
            ProcessId = 1234,
            WindowHandle = 0x1A2B
        };

        var item = new SelectableSourceItem(window, isInitiallySelected: true);

        Assert.Equal("win-1", item.Id);
        Assert.Equal("Visual Studio Code", item.Title);
        Assert.Equal(SourceType.Window, item.Type);
        Assert.Equal("Window", item.CategoryLabel);
        Assert.True(item.IsSelected);
        Assert.True(item.IsAvailable);
        Assert.Contains("Process: Code", item.Subtitle);
    }

    [Fact]
    public void Construction_ExtractsMonitorMetadataCorrectly()
    {
        var monitor = new MonitorSource
        {
            Id = "mon-1",
            Title = "Display 1 (Primary)",
            Width = 1920,
            Height = 1080,
            IsPrimary = true
        };

        var item = new SelectableSourceItem(monitor, isInitiallySelected: false);

        Assert.Equal("mon-1", item.Id);
        Assert.Equal("Display 1 (Primary)", item.Title);
        Assert.Equal(SourceType.Display, item.Type);
        Assert.Equal("Display", item.CategoryLabel);
        Assert.False(item.IsSelected);
        Assert.Contains("1920 × 1080", item.Subtitle);
        Assert.Contains("Primary Display", item.Subtitle);
    }

    [Fact]
    public void IsSelected_Change_InvokesCallback()
    {
        var window = new WindowSource { Id = "win-1", Title = "Window 1" };
        var callbackInvoked = false;
        var callbackValue = false;

        var item = new SelectableSourceItem(window, isInitiallySelected: false, (src, sel) =>
        {
            callbackInvoked = true;
            callbackValue = sel;
        });

        item.IsSelected = true;

        Assert.True(callbackInvoked);
        Assert.True(callbackValue);
    }
}
