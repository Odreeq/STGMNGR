using StageManager.Models;

namespace StageManager.Services;

internal static class WindowDisplayOrder
{
    internal static IReadOnlyList<WindowSnapshot> Apply(
        IReadOnlyList<WindowSnapshot> windows,
        IReadOnlyList<PinnedWindow>? pinnedWindows,
        IReadOnlyList<string>? hiddenApplications = null)
    {
        var visibleWindows = windows
            .Where(window => !(hiddenApplications ?? []).Any(application =>
                string.Equals(application, window.ProcessName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (visibleWindows.Length == 0)
        {
            return [];
        }

        var ordered = new List<WindowSnapshot>(visibleWindows.Length);
        var pinnedHandles = new HashSet<nint>();
        foreach (var pinnedWindow in (pinnedWindows ?? []).Take(StageManagerSettings.MaximumPinnedWindows))
        {
            var match = visibleWindows.FirstOrDefault(window =>
                !pinnedHandles.Contains(window.Handle) && pinnedWindow.Matches(window));
            if (match is null)
            {
                continue;
            }

            ordered.Add(match);
            pinnedHandles.Add(match.Handle);
        }

        foreach (var window in visibleWindows)
        {
            if (pinnedHandles.Add(window.Handle))
            {
                ordered.Add(window);
            }
        }

        return ordered;
    }

    internal static bool IsPinned(WindowSnapshot window, IReadOnlyList<PinnedWindow>? pinnedWindows) =>
        (pinnedWindows ?? []).Any(pinnedWindow => pinnedWindow.Matches(window));

    internal static WindowSnapshot? FindPreferredWindow(IEnumerable<WindowSnapshot> windows) =>
        windows
            .OrderBy(static window => window.IsAlwaysOnTop)
            .ThenByDescending(static window => window.WindowArea)
            .ThenByDescending(static window => window.IsForeground)
            .FirstOrDefault();
}
