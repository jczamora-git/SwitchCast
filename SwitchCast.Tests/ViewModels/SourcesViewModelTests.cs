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
        Assert.Equal(2, vm.DisplayedSources.Count);
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

        Assert.Equal(2, vm.DisplayedSources.Count); // Windows category

        vm.SelectedCategoryIndex = 1; // Displays category

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
}
