namespace SwitchCast.Services;

/// <summary>
/// Calculates DPI-aware screen positioning and monitor work-area clamping for external presenter dock popup menus.
/// </summary>
public static class PresenterDockMenuPositioner
{
    /// <summary>
    /// Calculates the target screen pixel coordinates (X, Y) for a popup menu anchored to a UI control.
    /// </summary>
    /// <param name="anchorScreenX">Anchor left coordinate in physical screen pixels.</param>
    /// <param name="anchorScreenY">Anchor top coordinate in physical screen pixels.</param>
    /// <param name="anchorWidth">Anchor control width in physical screen pixels.</param>
    /// <param name="anchorHeight">Anchor control height in physical screen pixels.</param>
    /// <param name="menuWidth">Popup menu width in physical screen pixels.</param>
    /// <param name="menuHeight">Popup menu height in physical screen pixels.</param>
    /// <param name="workAreaLeft">Monitor work area left boundary in physical screen pixels.</param>
    /// <param name="workAreaTop">Monitor work area top boundary in physical screen pixels.</param>
    /// <param name="workAreaRight">Monitor work area right boundary in physical screen pixels.</param>
    /// <param name="workAreaBottom">Monitor work area bottom boundary in physical screen pixels.</param>
    /// <param name="spacing">Spacing between anchor and menu in physical screen pixels.</param>
    /// <param name="margin">Minimum margin from screen boundaries in physical screen pixels.</param>
    /// <returns>The calculated (TargetX, TargetY) top-left screen position.</returns>
    public static (int TargetX, int TargetY) CalculatePosition(
        int anchorScreenX,
        int anchorScreenY,
        int anchorWidth,
        int anchorHeight,
        int menuWidth,
        int menuHeight,
        int workAreaLeft,
        int workAreaTop,
        int workAreaRight,
        int workAreaBottom,
        int spacing = 4,
        int margin = 8)
    {
        // 1. Calculate X position (default aligned with left of anchor button)
        int targetX = anchorScreenX;

        // Shift left if overflowing right edge of monitor
        if (targetX + menuWidth > workAreaRight - margin)
        {
            targetX = workAreaRight - margin - menuWidth;
        }

        // Clamp to left edge of monitor
        if (targetX < workAreaLeft + margin)
        {
            targetX = workAreaLeft + margin;
        }

        // 2. Calculate Y position (default below anchor button)
        int targetY = anchorScreenY + anchorHeight + spacing;

        // If overflowing bottom edge of monitor, flip above anchor button
        if (targetY + menuHeight > workAreaBottom - margin)
        {
            targetY = anchorScreenY - menuHeight - spacing;
        }

        // Clamp to top edge of monitor if necessary
        if (targetY < workAreaTop + margin)
        {
            targetY = workAreaTop + margin;
        }

        return (targetX, targetY);
    }
}
