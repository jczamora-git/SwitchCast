using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.Services.Media;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SourcesViewModelMediaTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IWindowDiscoveryService> _mockWindowService;
    private readonly Mock<IMonitorDiscoveryService> _mockMonitorService;
    private readonly Mock<IWindowIconService> _mockIconService;
    private readonly Mock<IMediaDiscoveryService> _mockMediaDiscoveryService;
    private readonly Mock<IMediaPickerService> _mockMediaPickerService;
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;

    public SourcesViewModelMediaTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IWindowDiscoveryService>();
        _mockMonitorService = new Mock<IMonitorDiscoveryService>();
        _mockIconService = new Mock<IWindowIconService>();
        _mockMediaDiscoveryService = new Mock<IMediaDiscoveryService>();
        _mockMediaPickerService = new Mock<IMediaPickerService>();
        _mockSettingsService = new Mock<IApplicationSettingsService>();

        _mockWindowService.Setup(w => w.EnumerateWindowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<WindowSource>());
        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<MonitorSource>());
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());
        _mockStateService.Setup(s => s.IsSourceSelected(It.IsAny<string>())).Returns(false);
        _mockSettingsService.SetupGet(s => s.CurrentSettings).Returns(new UserSettings());
    }

    [Fact]
    public void InitialCategory_DefaultsToWindows_CanSwitchToMedia()
    {
        var vm = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockMonitorService.Object,
            _mockIconService.Object,
            _mockMediaDiscoveryService.Object,
            _mockMediaPickerService.Object,
            _mockSettingsService.Object);

        Assert.Equal(0, vm.SelectedCategoryIndex);
        Assert.False(vm.IsMediaCategorySelected);

        vm.SelectedCategoryIndex = 3;
        Assert.True(vm.IsMediaCategorySelected);
    }

    [Fact]
    public async Task AddMediaAsync_PicksFilesAndAddsToMediaListAndSavesSettings()
    {
        var filePath = @"C:\Images\Welcome.png";
        var imageSource = new ImageMediaSource
        {
            Id = $"media:{filePath.ToLowerInvariant()}",
            Title = "Welcome.png",
            FilePath = filePath,
            IsAvailable = true
        };

        _mockMediaPickerService.Setup(p => p.PickMediaFilesAsync())
            .ReturnsAsync(new List<string> { filePath });
        _mockMediaDiscoveryService.Setup(d => d.CreateMediaSourceFromFileAsync(filePath))
            .ReturnsAsync(imageSource);

        var vm = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockMonitorService.Object,
            _mockIconService.Object,
            _mockMediaDiscoveryService.Object,
            _mockMediaPickerService.Object,
            _mockSettingsService.Object)
        {
            SelectedCategoryIndex = 3 // Switch to Media category
        };

        await vm.AddMediaAsync();

        Assert.Single(vm.DisplayedSources);
        Assert.Equal("Welcome.png", vm.DisplayedSources[0].Title);
        Assert.Equal(SourceType.Image, vm.DisplayedSources[0].Source.Type);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveMediaAsync_RemovesItemFromListQueueAndSettings()
    {
        var filePath = @"C:\Videos\Demo.mp4";
        var videoSource = new VideoMediaSource
        {
            Id = $"media:{filePath.ToLowerInvariant()}",
            Title = "Demo.mp4",
            FilePath = filePath,
            IsAvailable = true
        };

        _mockMediaPickerService.Setup(p => p.PickMediaFilesAsync())
            .ReturnsAsync(new List<string> { filePath });
        _mockMediaDiscoveryService.Setup(d => d.CreateMediaSourceFromFileAsync(filePath))
            .ReturnsAsync(videoSource);

        var vm = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockMonitorService.Object,
            _mockIconService.Object,
            _mockMediaDiscoveryService.Object,
            _mockMediaPickerService.Object,
            _mockSettingsService.Object)
        {
            SelectedCategoryIndex = 3
        };

        await vm.AddMediaAsync();
        Assert.Single(vm.DisplayedSources);

        var item = vm.DisplayedSources[0];
        await vm.RemoveMediaAsync(item);

        Assert.Empty(vm.DisplayedSources);
        _mockStateService.Verify(s => s.RemoveSelectedSource(item.Id), Times.Once);
        _mockSettingsService.Verify(s => s.SaveSettingsAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task AddMediaAsync_WhenFilteredOutByCategory_ShowsImportNoticeWithViewAction()
    {
        var filePath = @"C:\Images\Photo.png";
        var imageSource = new ImageMediaSource
        {
            Id = $"media:{filePath.ToLowerInvariant()}",
            Title = "Photo.png",
            FilePath = filePath,
            IsAvailable = true
        };

        _mockMediaPickerService.Setup(p => p.PickMediaFilesAsync())
            .ReturnsAsync(new List<string> { filePath });
        _mockMediaDiscoveryService.Setup(d => d.CreateMediaSourceFromFileAsync(filePath))
            .ReturnsAsync(imageSource);

        var vm = new SourcesViewModel(
            _mockStateService.Object,
            _mockWindowService.Object,
            _mockMonitorService.Object,
            _mockIconService.Object,
            _mockMediaDiscoveryService.Object,
            _mockMediaPickerService.Object,
            _mockSettingsService.Object)
        {
            SelectedCategoryIndex = 1 // Application Windows category
        };

        await vm.AddMediaAsync();

        // The imported media was not added to DisplayedSources because Windows category is selected
        Assert.Empty(vm.DisplayedSources);

        // An import notice with View action was raised
        Assert.True(vm.HasImportNotice);
        Assert.Equal("Photo.png imported successfully", vm.ImportNoticeMessage);

        // Clicking View executes ViewImportedSourceCommand
        vm.ViewImportedSourceCommand.Execute(null);

        // View action switched category to Media (3) and dismissed notice
        Assert.False(vm.HasImportNotice);
        Assert.Equal(3, vm.SelectedCategoryIndex);
        Assert.Single(vm.DisplayedSources);
        Assert.Equal("Photo.png", vm.DisplayedSources[0].Title);
    }
}

