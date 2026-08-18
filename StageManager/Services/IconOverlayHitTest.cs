namespace StageManager.Services;

internal readonly record struct OverlayBadgeBounds(
    nint Handle,
    double Left,
    double Top,
    double Width,
    double Height);

internal static class IconOverlayHitTest
{
    internal static nint FindBadge(
        double pointerX,
        double pointerY,
        IReadOnlyCollection<OverlayBadgeBounds> badges)
    {
        if (!double.IsFinite(pointerX) || !double.IsFinite(pointerY))
        {
            return 0;
        }

        foreach (var badge in badges)
        {
            if (CloseBadgeHitTest.Contains(
                    pointerX,
                    pointerY,
                    badge.Left,
                    badge.Top,
                    badge.Width,
                    badge.Height))
            {
                return badge.Handle;
            }
        }

        return 0;
    }
}
