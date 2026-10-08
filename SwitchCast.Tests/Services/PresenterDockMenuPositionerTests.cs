using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresenterDockMenuPositionerTests
{
    [Fact]
    public void CalculatePosition_NormalTopDock_PlacesBelowAnchor()
    {
        // Dock at top of 1920x1080 screen
        // Button at X=500, Y=24, Width=150, Height=32
        // Menu size: 280x200
        // Work area: 0, 0, 1920, 1040
        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            anchorScreenX: 500,
            anchorScreenY: 24,
            anchorWidth: 150,
            anchorHeight: 32,
            menuWidth: 280,
            menuHeight: 200,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1040,
            spacing: 4,
            margin: 8);

        Assert.Equal(500, targetX);
        Assert.Equal(24 + 32 + 4, targetY); // 60
    }

    [Fact]
    public void CalculatePosition_BottomDock_FlipsAboveAnchor()
    {
        // Dock dragged to bottom of screen: Y=980
        // Button at X=500, Y=980, Width=150, Height=32
        // Menu size: 280x200
        // If placed below: 980 + 32 + 4 = 1016 -> 1016 + 200 = 1216 > 1040 (overflows bottom)
        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            anchorScreenX: 500,
            anchorScreenY: 980,
            anchorWidth: 150,
            anchorHeight: 32,
            menuWidth: 280,
            menuHeight: 200,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1040,
            spacing: 4,
            margin: 8);

        Assert.Equal(500, targetX);
        Assert.Equal(980 - 200 - 4, targetY); // 776
    }

    [Fact]
    public void CalculatePosition_RightEdgeDock_ShiftsLeftToStayInWorkArea()
    {
        // Button at X=1800 on a 1920 wide monitor
        // Menu width: 280
        // 1800 + 280 = 2080 > 1920 - 8 = 1912
        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            anchorScreenX: 1800,
            anchorScreenY: 24,
            anchorWidth: 100,
            anchorHeight: 32,
            menuWidth: 280,
            menuHeight: 200,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1040,
            spacing: 4,
            margin: 8);

        Assert.Equal(1920 - 8 - 280, targetX); // 1632
        Assert.Equal(60, targetY);
    }

    [Fact]
    public void CalculatePosition_LeftEdgeDock_ClampsToWorkAreaMargin()
    {
        // Button at X=-10 (partially offscreen left)
        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            anchorScreenX: -10,
            anchorScreenY: 24,
            anchorWidth: 100,
            anchorHeight: 32,
            menuWidth: 280,
            menuHeight: 200,
            workAreaLeft: 0,
            workAreaTop: 0,
            workAreaRight: 1920,
            workAreaBottom: 1040,
            spacing: 4,
            margin: 8);

        Assert.Equal(8, targetX);
        Assert.Equal(60, targetY);
    }

    [Fact]
    public void CalculatePosition_SecondaryMonitor_UsesSecondaryWorkArea()
    {
        // Secondary monitor positioned at X=1920 to 3840, Y=0 to 1080
        var (targetX, targetY) = PresenterDockMenuPositioner.CalculatePosition(
            anchorScreenX: 2500,
            anchorScreenY: 30,
            anchorWidth: 120,
            anchorHeight: 30,
            menuWidth: 250,
            menuHeight: 180,
            workAreaLeft: 1920,
            workAreaTop: 0,
            workAreaRight: 3840,
            workAreaBottom: 1040,
            spacing: 4,
            margin: 8);

        Assert.Equal(2500, targetX);
        Assert.Equal(30 + 30 + 4, targetY); // 64
    }
}
