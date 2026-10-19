using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class SourcesViewModelTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<IWindowDiscoveryService> _mockWindowService;
    private readonly Mock<IMonitorDiscoveryService> _mockMonitorService;

    public SourcesViewModelTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockWindowService = new Mock<IWindowDiscoveryService>();
        _mockMonitorService = new Mock<IMonitorDiscoveryService>();

        _mockWindowService.Setup(w => w.EnumerateWindowsAsync(default))
            .ReturnsAsync(new List<WindowSource>
            {
                new() { Id = "win-1", Title = "Visual Studio Code", ProcessName = "Code", WindowHandle = 1001, ProcessId = 5001 },
                new() { Id = "win-2", Title = "Google Chrome", ProcessName = "chrome", WindowHandle = 1002, ProcessId = 5002 }
            });

        _mockMonitorService.Setup(m => m.EnumerateMonitorsAsync(default))
            .ReturnsAsync(new List<MonitorSource>
            {
                new() { Id = "mon-1", Title = "Display 1 (Primary)", DeviceName = @"\\.\DISPLAY1", Width = 1920, Height = 1080, IsPrimary = true }
            });
    }

    [Fact]
    public void InitialState_Properties_AreDefault()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);

        Assert.Equal(0, vm.SelectedCategoryIndex);
        Assert.Empty(vm.DisplayedSources);
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.HasError);
        Assert.False(vm.HasDiscoveredSources);
    }

    [Fact]
    public async Task RefreshSourcesAsync_PopulatesDiscoveredSources_AndUpdatesHeaders()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.Equal("Application Windows (2)", vm.WindowCategoryHeader);
        Assert.Equal("Displays & Monitors (1)", vm.DisplayCategoryHeader);
        Assert.Equal(3, vm.DisplayedSources.Count); // All Sources (2 windows + 1 monitor)
        Assert.True(vm.HasDiscoveredSources);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task RefreshSourcesAsync_ReconcilesAvailabilityWithStateService()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        _mockStateService.Verify(s => s.ReconcileAvailability(It.Is<IReadOnlySet<string>>(set =>
            set.Contains("win-1") && set.Contains("win-2") && set.Contains("mon-1"))), Times.Once);
    }

    [Fact]
    public async Task CategorySwitch_UpdatesDisplayedSources()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.DisplayedSources.Count); // All Sources category

        vm.SelectedCategoryIndex = 1; // Windows category
        Assert.Equal(2, vm.DisplayedSources.Count);

        vm.SelectedCategoryIndex = 2; // Displays category

        Assert.Single(vm.DisplayedSources);
        Assert.Equal("Display 1 (Primary)", vm.DisplayedSources[0].Title);
    }

    [Fact]
    public async Task SearchQuery_FiltersSourcesByTitleOrProcessName()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        vm.SearchQuery = "Chrome";

        Assert.Single(vm.DisplayedSources);
        Assert.Equal("Google Chrome", vm.DisplayedSources[0].Title);
    }

    [Fact]
    public async Task ItemSelection_AddsOrRemovesFromStateService()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        var firstItem = vm.DisplayedSources[0];
        firstItem.IsSelected = true;

        _mockStateService.Verify(s => s.AddSelectedSource(firstItem.Source), Times.Once);

        firstItem.IsSelected = false;

        _mockStateService.Verify(s => s.RemoveSelectedSource(firstItem.Id), Times.Once);
    }

    [Fact]
    public async Task RefreshSourcesAsync_OnException_SetsHasError()
    {
        _mockWindowService.Setup(w => w.EnumerateWindowsAsync(default))
            .ThrowsAsync(new InvalidOperationException("Enumeration failed"));

        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("Enumeration failed", vm.ErrorMessage);
    }

    [Fact]
    public async Task SearchQuery_ClearSearch_RestoresFullList()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        vm.SearchQuery = "Chrome";
        Assert.Single(vm.DisplayedSources);
        Assert.True(vm.IsSearchQueryNotEmpty);

        vm.ClearSearchCommand.Execute(null);
        Assert.Empty(vm.SearchQuery);
        Assert.False(vm.IsSearchQueryNotEmpty);
        Assert.Equal(3, vm.DisplayedSources.Count);
    }

    [Fact]
    public async Task CategoryDisplayOptions_ExposesAllSixCategoriesWithCounts()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        var options = vm.CategoryDisplayOptions;
        Assert.Equal(6, options.Count);
        Assert.Contains("All Sources (3)", options[0]);
        Assert.Contains("Application Windows (2)", options[1]);
        Assert.Contains("Displays & Monitors (1)", options[2]);
        Assert.Contains("Media Files (0)", options[3]);
        Assert.Contains("PowerPoint (0)", options[4]);
        Assert.Contains("Cameras (0)", options[5]);
    }

    [Fact]
    public async Task ShowQueuedOnly_FiltersToOnlySelectedSources()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.DisplayedSources.Count);

        // Select the second source
        _mockStateService.Setup(s => s.IsSourceSelected("win-2")).Returns(true);
        _mockStateService.Setup(s => s.SelectedSourceCount).Returns(1);

        vm.ShowQueuedOnly = true;

        Assert.Single(vm.DisplayedSources);
        Assert.Equal("win-2", vm.DisplayedSources[0].Id);

        // Toggling off restores all sources
        vm.ShowQueuedOnly = false;
        Assert.Equal(3, vm.DisplayedSources.Count);
    }

    [Fact]
    public void QueueSummaryText_ReflectsSelectedCountCorrectly()
    {
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(0);
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        Assert.Equal("0 sources queued", vm.QueueSummaryText);

        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(1);
        Assert.Equal("1 source queued", vm.QueueSummaryText);

        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(3);
        Assert.Equal("3 sources queued", vm.QueueSummaryText);
    }

    [Fact]
    public async Task SearchFeedbackText_UpdatesBasedOnSearchQueryAndResults()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.False(vm.HasSearchFeedback);

        vm.SearchQuery = "Visual";
        Assert.True(vm.HasSearchFeedback);
        Assert.Equal("1 result", vm.SearchFeedbackText);

        vm.SearchQuery = "o"; // matches VS Code and Chrome (2 sources)
        Assert.True(vm.HasSearchFeedback);
        Assert.Equal("2 results", vm.SearchFeedbackText);

        vm.ClearSearchCommand.Execute(null);
        Assert.False(vm.HasSearchFeedback);
    }

    [Fact]
    public async Task EmptyState_ProvidesAccurateTextForQueuedOnlyAndSearch()
    {
        var vm = new SourcesViewModel(_mockStateService.Object, _mockWindowService.Object, _mockMonitorService.Object);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        // Search that yields no results
        vm.SearchQuery = "NonExistentTermXYZ";
        Assert.True(vm.IsEmpty);
        Assert.Equal("No matching sources", vm.EmptyStateTitle);
        Assert.Equal("Try another search term or change the category.", vm.EmptyStateSubtitle);

        vm.ClearSearchCommand.Execute(null);
        Assert.False(vm.IsEmpty);

        // Queued only when nothing is queued
        vm.ShowQueuedOnly = true;
        Assert.True(vm.IsEmpty);
        Assert.Equal("No queued sources", vm.EmptyStateTitle);
        Assert.Contains("No sources are currently queued", vm.EmptyStateSubtitle);
    }
}

