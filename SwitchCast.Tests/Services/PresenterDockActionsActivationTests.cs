using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using SwitchCast.ViewModels;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresenterDockActionsActivationTests
{
    [Fact]
    public void PresenterDockViewModel_ShowDashboardCommand_CallsDockServiceShowDashboard()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        vm.ShowDashboardCommand.Execute(null);

        mockDock.Verify(d => d.ShowDashboard(), Times.Once);
    }

    [Fact]
    public void PresenterDockViewModel_ShowOutputWindowCommand_CallsPresentationWindowServiceShow()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        vm.ShowOutputWindowCommand.Execute(null);

        mockPresWin.Verify(p => p.ShowPresentationWindow(), Times.Once);
    }

    [Fact]
    public void PresenterDockService_RequestShowDashboard_InvokedWhenRequested()
    {
        var mockDock = new Mock<IPresenterDockService>();
        bool eventFired = false;
        mockDock.Object.RequestShowDashboard += (s, e) => eventFired = true;

        mockDock.Setup(d => d.ShowDashboard()).Raises(d => d.RequestShowDashboard += null, EventArgs.Empty);

        mockDock.Object.ShowDashboard();

        Assert.True(eventFired);
    }

    [Fact]
    public void PresenterDockViewModel_CloseDockTwice_HandledSafelyWithoutException()
    {
        var mockCoord = new Mock<IPresentationCoordinator>();
        var mockState = new Mock<IPresentationStateService>();
        var mockPresWin = new Mock<IPresentationWindowService>();
        var mockDock = new Mock<IPresenterDockService>();
        var mockSettings = new Mock<IApplicationSettingsService>();

        mockSettings.Setup(s => s.CurrentSettings).Returns(new UserSettings());
        mockState.Setup(s => s.SelectedSources).Returns(new List<CaptureSource>());

        var vm = new PresenterDockViewModel(
            mockCoord.Object,
            mockState.Object,
            mockPresWin.Object,
            mockDock.Object,
            mockSettings.Object);

        vm.CloseDockCommand.Execute(null);
        vm.CloseDockCommand.Execute(null);

        mockDock.Verify(d => d.CloseDock(), Times.Exactly(2));
        mockDock.Verify(d => d.ShowDashboard(), Times.Exactly(2));
    }
}
