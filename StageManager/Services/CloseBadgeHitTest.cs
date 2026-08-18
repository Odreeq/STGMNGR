namespace StageManager.Services;

internal static class CloseBadgeHitTest
{
    internal const double Padding = 4;

    internal static bool Contains(
        double pointerX,
        double pointerY,
        double badgeLeft,
        double badgeTop,
        double badgeWidth,
        double badgeHeight)
    {
        if (!double.IsFinite(pointerX) ||
            !double.IsFinite(pointerY) ||
            !double.IsFinite(badgeLeft) ||
            !double.IsFinite(badgeTop) ||
            !double.IsFinite(badgeWidth) ||
            !double.IsFinite(badgeHeight) ||
            badgeWidth <= 0 ||
            badgeHeight <= 0)
        {
            return false;
        }

        return pointerX >= badgeLeft - Padding &&
               pointerX <= badgeLeft + badgeWidth + Padding &&
               pointerY >= badgeTop - Padding &&
               pointerY <= badgeTop + badgeHeight + Padding;
    }
}
