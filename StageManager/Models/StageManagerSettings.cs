namespace StageManager.Models;

public enum DisplayMode
{
    Fixed,
    Floating
}

public sealed record StageManagerSettings(
    DisplayMode DisplayMode,
    double DisplayWidth,
    int CardCount)
{
    public const double DefaultPreviewOpacity = 0.82;
    public const int MaximumPinnedWindows = 4;

    public double PreviewOpacity { get; init; } = DefaultPreviewOpacity;

    /// <summary>Individual windows currently pinned in the StageBar.</summary>
    public IReadOnlyList<PinnedWindow> PinnedWindows { get; init; } = [];

    /// <summary>
    /// Legacy application-wide pins. They are migrated to the best matching
    /// running window the next time StageBar starts, then cleared.
    /// </summary>
    public IReadOnlyList<string> PinnedApplications { get; init; } = [];

    /// <summary>
    /// Applications deliberately hidden from the StageBar. This is kept separately
    /// from pinned applications so an app can be restored without losing any of
    /// the user's other display preferences.
    /// </summary>
    public IReadOnlyList<string> HiddenApplications { get; init; } = [];

    public static StageManagerSettings Default { get; } = new(DisplayMode.Fixed, 286, 3);
}
