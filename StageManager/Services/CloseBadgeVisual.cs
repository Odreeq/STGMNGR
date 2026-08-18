namespace StageManager.Services;

internal readonly record struct CloseBadgeAppearance(
    uint BackgroundArgb,
    uint BorderArgb,
    double IconOpacity,
    bool ShowCloseGlyph);

internal static class CloseBadgeVisual
{
    private static readonly CloseBadgeAppearance Normal = new(
        0xFF11161D,
        0xFF344154,
        1,
        false);

    private static readonly CloseBadgeAppearance Armed = new(
        0xFFC62828,
        0xFFFF6B6B,
        0.25,
        true);

    internal static CloseBadgeAppearance Resolve(bool isArmed) => isArmed ? Armed : Normal;
}
