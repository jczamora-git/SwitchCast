using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class WindowPositioningHelperTests
{
    [Fact]
    public void CalculateCenteredPosition_PrimaryMonitor1080p_TaskbarBottom_CalculatesCorrectCenter()
    {
        // 1920x1080 with 40px taskbar at bottom: WorkArea 1920x1040
        int workAreaLeft = 0;
        int workAreaTop = 0;
        int workAreaWidth = 1920;
        int workAreaHeight = 1040;
        int windowWidth = 1280;
        int windowHeight = 720;

        var (x, y, w, h) = WindowPositioningHelper.CalculateCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(320, x);  // 0 + (1920 - 1280) / 2
        Assert.Equal(160, y);  // 0 + (1040 - 720) / 2
        Assert.Equal(1280, w);
        Assert.Equal(720, h);

        // Verification of boundary containment
        Assert.True(x >= workAreaLeft);
        Assert.True(y >= workAreaTop);
        Assert.True(x + w <= workAreaLeft + workAreaWidth);
        Assert.True(y + h <= workAreaTop + workAreaHeight);
    }

    [Fact]
    public void CalculateCenteredPosition_SecondaryMonitorLeft_NegativeCoordinates_CentersCorrectly()
    {
        // Secondary monitor positioned to the left: Left=-1920, Top=0, Width=1920, Height=1080
        int workAreaLeft = -1920;
        int workAreaTop = 0;
        int workAreaWidth = 1920;
        int workAreaHeight = 1080;
        int windowWidth = 1280;
        int windowHeight = 720;

        var (x, y, w, h) = WindowPositioningHelper.CalculateCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(-1600, x); // -1920 + (1920 - 1280) / 2 = -1920 + 320 = -1600
        Assert.Equal(180, y);   // 0 + (1080 - 720) / 2 = 180
        Assert.Equal(1280, w);
        Assert.Equal(720, h);

        Assert.True(x >= workAreaLeft);
        Assert.True(y >= workAreaTop);
        Assert.True(x + w <= workAreaLeft + workAreaWidth);
        Assert.True(y + h <= workAreaTop + workAreaHeight);
    }

    [Fact]
    public void CalculateCenteredPosition_SecondaryMonitorAbove_NegativeTop_CentersCorrectly()
    {
        // Secondary monitor positioned above: Left=0, Top=-1080, Width=1920, Height=1080
        int workAreaLeft = 0;
        int workAreaTop = -1080;
        int workAreaWidth = 1920;
        int workAreaHeight = 1080;
        int windowWidth = 1280;
        int windowHeight = 720;

        var (x, y, w, h) = WindowPositioningHelper.CalculateCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(320, x);  // 0 + (1920 - 1280) / 2
        Assert.Equal(-900, y); // -1080 + (1080 - 720) / 2 = -1080 + 180 = -900
        Assert.Equal(1280, w);
        Assert.Equal(720, h);

        Assert.True(x >= workAreaLeft);
        Assert.True(y >= workAreaTop);
        Assert.True(x + w <= workAreaLeft + workAreaWidth);
        Assert.True(y + h <= workAreaTop + workAreaHeight);
    }

    [Fact]
    public void CalculateCenteredPosition_LowResolution1366x768_FitsCorrectly()
    {
        int workAreaLeft = 0;
        int workAreaTop = 0;
        int workAreaWidth = 1366;
        int workAreaHeight = 728; // Taskbar at 40px
        int windowWidth = 1280;
        int windowHeight = 720;

        var (x, y, w, h) = WindowPositioningHelper.CalculateCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(43, x); // (1366 - 1280) / 2 = 86 / 2 = 43
        Assert.Equal(4, y);  // (728 - 720) / 2 = 8 / 2 = 4
        Assert.Equal(1280, w);
        Assert.Equal(720, h);

        Assert.True(x >= workAreaLeft);
        Assert.True(y >= workAreaTop);
        Assert.True(x + w <= workAreaLeft + workAreaWidth);
        Assert.True(y + h <= workAreaTop + workAreaHeight);
    }

    [Fact]
    public void CalculateCenteredPosition_WhenWindowLargerThanWorkArea_ClampsToWorkArea()
    {
        // Work area smaller than requested size (e.g. 1024x600)
        int workAreaLeft = 0;
        int workAreaTop = 0;
        int workAreaWidth = 1024;
        int workAreaHeight = 600;
        int windowWidth = 1280;
        int windowHeight = 720;

        var (x, y, w, h) = WindowPositioningHelper.CalculateCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(0, x);
        Assert.Equal(0, y);
        Assert.Equal(1024, w);
        Assert.Equal(600, h);
    }

    [Theory]
    [InlineData(1.0, 1280, 720, 320, 160, 1280, 720)]
    [InlineData(1.25, 1600, 900, 160, 70, 1600, 900)]
    public void CalculateDpiScaledCenteredPosition_AppliesDpiScalingCorrectly(
        double scale,
        int expectedPixelWidth,
        int expectedPixelHeight,
        int expectedX,
        int expectedY,
        int expectedW,
        int expectedH)
    {
        int workAreaLeft = 0;
        int workAreaTop = 0;
        int workAreaWidth = 1920;
        int workAreaHeight = 1040;

        var (x, y, w, h) = WindowPositioningHelper.CalculateDpiScaledCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, 1280.0, 720.0, scale);

        Assert.Equal(expectedPixelWidth, w);
        Assert.Equal(expectedPixelHeight, h);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
        Assert.Equal(expectedW, w);
        Assert.Equal(expectedH, h);
    }

    [Fact]
    public void CalculateDpiScaledCenteredPosition_On1440pWith125Dpi_CentersProperly()
    {
        // 2560x1440 with 40px taskbar -> 2560x1400 work area at 125% DPI scale
        int workAreaLeft = 0;
        int workAreaTop = 0;
        int workAreaWidth = 2560;
        int workAreaHeight = 1400;

        var (x, y, w, h) = WindowPositioningHelper.CalculateDpiScaledCenteredPosition(
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, 1280.0, 720.0, 1.25);

        // 1280 * 1.25 = 1600, 720 * 1.25 = 900
        Assert.Equal(1600, w);
        Assert.Equal(900, h);
        Assert.Equal(480, x); // (2560 - 1600) / 2 = 960 / 2 = 480
        Assert.Equal(250, y); // (1400 - 900) / 2 = 500 / 2 = 250
    }
}
