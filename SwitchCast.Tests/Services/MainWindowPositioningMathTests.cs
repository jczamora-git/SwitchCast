using Xunit;

namespace SwitchCast.Tests.Services;

public class MainWindowPositioningMathTests
{
    public record WorkArea(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private static (int CenterX, int CenterY) CalculateCenter(WorkArea workArea, int windowWidth, int windowHeight)
    {
        int centerX = workArea.Left + (workArea.Width - windowWidth) / 2;
        int centerY = workArea.Top + (workArea.Height - windowHeight) / 2;
        return (centerX, centerY);
    }

    private static (int ClampedX, int ClampedY) ClampToWorkArea(WorkArea workArea, int savedX, int savedY, int windowWidth, int windowHeight)
    {
        int maxX = Math.Max(workArea.Left, workArea.Right - windowWidth);
        int maxY = Math.Max(workArea.Top, workArea.Bottom - windowHeight);
        int clampedX = Math.Clamp(savedX, workArea.Left, maxX);
        int clampedY = Math.Clamp(savedY, workArea.Top, maxY);
        return (clampedX, clampedY);
    }

    [Fact]
    public void CalculateCenter_PrimaryMonitor1080p_TaskbarBottom_CalculatesCorrectWorkAreaCenter()
    {
        // 1920x1080 with 40px taskbar at bottom -> WorkArea 1920x1040
        var workArea = new WorkArea(0, 0, 1920, 1040);
        int windowWidth = 1000;
        int windowHeight = 700;

        var (cx, cy) = CalculateCenter(workArea, windowWidth, windowHeight);

        Assert.Equal(460, cx); // 0 + (1920 - 1000)/2
        Assert.Equal(170, cy); // 0 + (1040 - 700)/2

        // Ensure window is fully within work area
        Assert.True(cx >= workArea.Left);
        Assert.True(cy >= workArea.Top);
        Assert.True(cx + windowWidth <= workArea.Right);
        Assert.True(cy + windowHeight <= workArea.Bottom);
    }

    [Fact]
    public void CalculateCenter_SecondaryMonitorLeft_NegativeCoordinates_CentersCorrectly()
    {
        // Secondary monitor positioned to the left: (-1920, 0) to (0, 1080)
        var workArea = new WorkArea(-1920, 0, 0, 1080);
        int windowWidth = 1200;
        int windowHeight = 800;

        var (cx, cy) = CalculateCenter(workArea, windowWidth, windowHeight);

        Assert.Equal(-1560, cx); // -1920 + (1920 - 1200)/2 = -1920 + 360 = -1560
        Assert.Equal(140, cy);   // 0 + (1080 - 800)/2 = 140

        Assert.True(cx >= workArea.Left);
        Assert.True(cy >= workArea.Top);
        Assert.True(cx + windowWidth <= workArea.Right);
        Assert.True(cy + windowHeight <= workArea.Bottom);
    }

    [Fact]
    public void ClampToWorkArea_WhenSavedCoordinatesAreWithinBounds_PreservesPosition()
    {
        var workArea = new WorkArea(0, 0, 2560, 1400);
        int windowWidth = 1200;
        int windowHeight = 800;

        var (x, y) = ClampToWorkArea(workArea, 200, 100, windowWidth, windowHeight);

        Assert.Equal(200, x);
        Assert.Equal(100, y);
    }

    [Fact]
    public void ClampToWorkArea_WhenSavedCoordinatesExceedMonitorBounds_ClampsInside()
    {
        var workArea = new WorkArea(0, 0, 1920, 1040);
        int windowWidth = 1000;
        int windowHeight = 700;

        // Position placed partially off-screen at bottom-right
        var (x, y) = ClampToWorkArea(workArea, 1500, 800, windowWidth, windowHeight);

        Assert.Equal(920, x); // 1920 - 1000
        Assert.Equal(340, y); // 1040 - 700

        Assert.True(x + windowWidth <= workArea.Right);
        Assert.True(y + windowHeight <= workArea.Bottom);
    }
}
