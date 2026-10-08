using Microsoft.UI.Xaml.Media;
using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SourcesViewModelIconTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IWindowDiscoveryService> _mockWindowDiscoveryService;
    private readonly Mock<IMonitorDiscoveryService> _mockMonitorDiscoveryService;
    private readonly Mock<IWindowIconService> _mockIconService;

    public SourcesViewModelIconTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowDiscoveryService = new Mock<IWindowDiscoveryService>();
        _mockMonitorDiscoveryService = new Mock<IMonitorDiscoveryService>();
        _mockIconService = new Mock<IWindowIconService>();

        _mockStateService.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.Setup(s => s.SelectedSourceCount).Returns(0);
        _mockStateService.Setup(s => s.IsSourceSelected(It.IsAny<string>())).Returns(false);
    }

    [Fact]
    public void SelectableSourceItem_HasIconSource_ReflectsNegation()
    {
        var window = new WindowSource
        {
            Id = "win_1",
            Title = "Visual Studio",
            WindowHandle = 0x1000
        };

        var item = new SelectableSourceItem(window);
        Assert.False(item.HasIconSource);
        Assert.True(item.HasNoIconSource);
        Assert.Null(item.IconSource);

        item.IconSource = new ImageSource();
        item.HasIconSource = true;

        Assert.True(item.HasIconSource);
        Assert.False(item.HasNoIconSource);
        Assert.NotNull(item.IconSource);
    }

    [Fact]
    public async Task RefreshSourcesAsync_LoadsIconsForDiscoveredWindows()
    {
        var win1 = new WindowSource { Id = "win_chrome", Title = "Google Chrome", WindowHandle = 0x2000, ProcessName = "chrome" };
        var win2 = new WindowSource { Id = "win_vs", Title = "Visual Studio", WindowHandle = 0x3000, ProcessName = "devenv" };

        _mockWindowDiscoveryService.Setup(w => w.EnumerateWindowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WindowSource> { win1, win2 });

        _mockMonitorDiscoveryService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorSource>());

        var dummyImage = new ImageSource();
        _mockIconService.Setup(i => i.GetIconForSourceAsync(It.Is<CaptureSource>(s => s.Id == "win_chrome"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyImage);
        _mockIconService.Setup(i => i.GetIconForSourceAsync(It.Is<CaptureSource>(s => s.Id == "win_vs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImageSource?)null);

        var viewModel = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowDiscoveryService.Object,
            _mockMonitorDiscoveryService.Object,
            _mockIconService.Object);

        await viewModel.RefreshSourcesAsync();

        // Give the async task a moment to populate
        await Task.Delay(50);

        Assert.Equal(2, viewModel.DisplayedSources.Count);

        var chromeItem = viewModel.DisplayedSources.First(s => s.Id == "win_chrome");
        Assert.True(chromeItem.HasIconSource);
        Assert.Equal(dummyImage, chromeItem.IconSource);

        var vsItem = viewModel.DisplayedSources.First(s => s.Id == "win_vs");
        Assert.False(vsItem.HasIconSource);
        Assert.True(vsItem.HasNoIconSource);
    }

    [Fact]
    public async Task RefreshSourcesAsync_WhenIconServiceThrows_ContinuesDisplayingSourcesWithFallback()
    {
        var win = new WindowSource { Id = "win_err", Title = "Problem App", WindowHandle = 0x4000 };

        _mockWindowDiscoveryService.Setup(w => w.EnumerateWindowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WindowSource> { win });
        _mockMonitorDiscoveryService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitorSource>());

        _mockIconService.Setup(i => i.GetIconForSourceAsync(It.IsAny<CaptureSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Icon extraction fault"));

        var viewModel = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowDiscoveryService.Object,
            _mockMonitorDiscoveryService.Object,
            _mockIconService.Object);

        await viewModel.RefreshSourcesAsync();
        await Task.Delay(50);

        Assert.Single(viewModel.DisplayedSources);
        var item = viewModel.DisplayedSources[0];
        Assert.False(item.HasIconSource);
        Assert.True(item.HasNoIconSource);
        Assert.False(viewModel.HasError);
    }
}
