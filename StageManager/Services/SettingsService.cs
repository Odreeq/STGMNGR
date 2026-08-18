using System.Text.Json;
using System.IO;
using StageManager.Models;

namespace StageManager.Services;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    internal SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StageManager",
            "settings.json"))
    {
    }

    internal SettingsService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = settingsPath;
    }

    internal StageManagerSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return StageManagerSettings.Default;
            }

            var settings = JsonSerializer.Deserialize<StageManagerSettings>(
                File.ReadAllText(_settingsPath),
                SerializerOptions);
            return Normalize(settings ?? StageManagerSettings.Default);
        }
        catch (JsonException)
        {
            return StageManagerSettings.Default;
        }
        catch (IOException)
        {
            return StageManagerSettings.Default;
        }
    }

    internal void Save(StageManagerSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            _settingsPath,
            JsonSerializer.Serialize(Normalize(settings), SerializerOptions));
    }

    internal static StageManagerSettings Normalize(StageManagerSettings settings) => settings with
    {
        DisplayWidth = Math.Clamp(
            settings.DisplayWidth,
            LayoutMetrics.MinimumWidth,
            LayoutMetrics.MaximumWidth),
        CardCount = Math.Clamp(settings.CardCount, 1, LayoutMetrics.AbsoluteMaximumCardCount),
        PreviewOpacity = Math.Clamp(settings.PreviewOpacity, 0.25, 1),
        PinnedWindows = NormalizePinnedWindows(settings.PinnedWindows),
        PinnedApplications = NormalizeLegacyPinnedApplications(settings.PinnedApplications),
        HiddenApplications = NormalizeApplicationNames(settings.HiddenApplications)
    };

    internal static bool AreEquivalent(StageManagerSettings first, StageManagerSettings second) =>
        first.DisplayMode == second.DisplayMode &&
        first.DisplayWidth.Equals(second.DisplayWidth) &&
        first.CardCount == second.CardCount &&
        first.PreviewOpacity.Equals(second.PreviewOpacity) &&
        first.PinnedWindows.SequenceEqual(second.PinnedWindows) &&
        first.PinnedApplications.SequenceEqual(second.PinnedApplications, StringComparer.OrdinalIgnoreCase) &&
        first.HiddenApplications.SequenceEqual(second.HiddenApplications, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<PinnedWindow> NormalizePinnedWindows(IReadOnlyList<PinnedWindow>? windows) =>
        (windows ?? [])
        .Where(static window => window.Handle != 0)
        .Distinct()
        .Take(StageManagerSettings.MaximumPinnedWindows)
        .ToArray();

    private static IReadOnlyList<string> NormalizeLegacyPinnedApplications(IReadOnlyList<string>? applications) =>
        NormalizeApplicationNames(applications)
        .Take(StageManagerSettings.MaximumPinnedWindows)
        .ToArray();

    private static IReadOnlyList<string> NormalizeApplicationNames(IReadOnlyList<string>? applications) =>
        (applications ?? [])
        .Select(static application => application.Trim())
        .Where(static application => !string.IsNullOrWhiteSpace(application))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
