using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.ViewModels;

public class DashboardViewModelTests
{
    private readonly Mock<IPresentationStateService> _mockStateService;
    private readonly Mock<INavigationService> _mockNavigationService;

    public DashboardViewModelTests()
    {
        _mockStateService = new Mock<IPresentationStateService>();
        _mockNavigationService = new Mock<INavigationService>();
    }

    [Fact]
    public void InitialProperties_ReflectPresentationStateService()
    {
        _mockStateService.SetupGet(s => s.Status).Returns(PresentationStatus.Idle);
        _mockStateService.SetupGet(s => s.ActiveSource).Returns((CaptureSource?)null);
        _mockStateService.SetupGet(s => s.SelectedSourceCount).Returns(0);
        _mockStateService.SetupGet(s => s.SelectedSources).Returns(new List<CaptureSource>());

        var vm = new DashboardViewModel(_mockStateService.Object, _mockNavigationService.Object);

        Assert.Equal(PresentationStatus.Idle, vm.Status);
        Assert.Equal("Not Started", vm.StatusDisplayText);
        Assert.Equal("None", vm.ActiveSourceTitle);
        Assert.Equal(0, vm.SelectedSourceCount);
        Assert.False(vm.HasSelectedSources);
        Assert.False(vm.HasActiveSource);
    }

    [Fact]
    public void NavigateToSourcesCommand_CallsNavigationService()
    {
        var vm = new DashboardViewModel(_mockStateService.Object, _mockNavigationService.Object);

        vm.NavigateToSourcesCommand.Execute(null);

        _mockNavigationService.Verify(n => n.NavigateTo(It.Is<Type>(t => t.Name == "SourcesPage"), null), Times.Once);
    }

    [Fact]
    public void ActionCommands_CanExecute_AreDisabledForPhase1()
    {
        var vm = new DashboardViewModel(_mockStateService.Object, _mockNavigationService.Object);

        Assert.False(vm.StartPresentationCommand.CanExecute(null));
        Assert.False(vm.PausePresentationCommand.CanExecute(null));
        Assert.False(vm.BlackoutCommand.CanExecute(null));
    }
}
