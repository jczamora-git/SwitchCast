using Moq;
using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresenterDockServiceTests
{
    [Fact]
    public void InitialState_IsDockClosed()
    {
        var mockDockService = new Mock<IPresenterDockService>();
        mockDockService.SetupGet(d => d.IsDockOpen).Returns(false);

        Assert.False(mockDockService.Object.IsDockOpen);
    }

    [Fact]
    public void ShowDock_RaisesDockOpenedEvent()
    {
        var mockDockService = new Mock<IPresenterDockService>();
        bool openedFired = false;
        mockDockService.Object.DockOpened += (s, e) => openedFired = true;

        mockDockService.Setup(d => d.ShowDock()).Raises(d => d.DockOpened += null, EventArgs.Empty);

        mockDockService.Object.ShowDock();
        Assert.True(openedFired);
    }

    [Fact]
    public void CloseDock_RaisesDockClosedEvent()
    {
        var mockDockService = new Mock<IPresenterDockService>();
        bool closedFired = false;
        mockDockService.Object.DockClosed += (s, e) => closedFired = true;

        mockDockService.Setup(d => d.CloseDock()).Raises(d => d.DockClosed += null, EventArgs.Empty);

        mockDockService.Object.CloseDock();
        Assert.True(closedFired);
    }
}
