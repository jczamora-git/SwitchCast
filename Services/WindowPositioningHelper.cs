namespace SwitchCast.Services;

/// <summary>
/// Provides pure mathematical helper methods for multi-monitor, DPI-aware window centering and bounding calculations.
/// </summary>
public static class WindowPositioningHelper
{
    /// <summary>
    /// Calculates the centered position and clamped dimensions for a window within a monitor's usable work area.
    /// Supports arbitrary monitor work-area offsets, including negative coordinates.
    /// </summary>
    /// <param name="workAreaLeft">Left coordinate of the monitor work area.</param>
    /// <param name="workAreaTop">Top coordinate of the monitor work area.</param>
    /// <param name="workAreaWidth">Width of the monitor work area.</param>
    /// <param name="workAreaHeight">Height of the monitor work area.</param>
    /// <param name="desiredWidth">Desired window width in physical pixels.</param>
    /// <param name="desiredHeight">Desired window height in physical pixels.</param>
    /// <returns>Calculated X, Y, Width, and Height in physical pixels.</returns>
    public static (int X, int Y, int Width, int Height) CalculateCenteredPosition(
        int workAreaLeft,
        int workAreaTop,
        int workAreaWidth,
        int workAreaHeight,
        int desiredWidth,
        int desiredHeight)
    {
        int clampedWidth = Math.Min(desiredWidth, workAreaWidth);
        int clampedHeight = Math.Min(desiredHeight, workAreaHeight);

        int centerX = workAreaLeft + (workAreaWidth - clampedWidth) / 2;
        int centerY = workAreaTop + (workAreaHeight - clampedHeight) / 2;

        return (centerX, centerY, clampedWidth, clampedHeight);
    }

    /// <summary>
    /// Calculates the centered position and clamped dimensions for a DIP-specified window given a DPI scale factor.
    /// </summary>
    /// <param name="workAreaLeft">Left coordinate of the monitor work area.</param>
    /// <param name="workAreaTop">Top coordinate of the monitor work area.</param>
    /// <param name="workAreaWidth">Width of the monitor work area.</param>
    /// <param name="workAreaHeight">Height of the monitor work area.</param>
    /// <param name="widthDip">Desired window width in device-independent pixels (DIPs).</param>
    /// <param name="heightDip">Desired window height in device-independent pixels (DIPs).</param>
    /// <param name="dpiScale">DPI scale factor (e.g., 1.0 for 100%, 1.25 for 125%, 1.5 for 150%).</param>
    /// <returns>Calculated X, Y, Width, and Height in physical pixels.</returns>
    public static (int X, int Y, int Width, int Height) CalculateDpiScaledCenteredPosition(
        int workAreaLeft,
        int workAreaTop,
        int workAreaWidth,
        int workAreaHeight,
        double widthDip,
        double heightDip,
        double dpiScale)
    {
        double effectiveScale = dpiScale > 0 ? dpiScale : 1.0;
        int pixelWidth = (int)Math.Round(widthDip * effectiveScale);
        int pixelHeight = (int)Math.Round(heightDip * effectiveScale);

        return CalculateCenteredPosition(workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, pixelWidth, pixelHeight);
    }
}
