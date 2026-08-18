using System.Drawing;
using System.Text.Json;
using StageManager.Interop;
using StageManager.Models;
using StageManager.Services;

namespace StageManager.Tests;

internal static class Program
{
    private static readonly (string Name, Action Test)[] Tests =
    [
        (nameof(DefaultSettingsRemainCompatible), DefaultSettingsRemainCompatible),
        (nameof(ExistingSettingsJsonRemainsCompatible), ExistingSettingsJsonRemainsCompatible),
        (nameof(SettingsNormalizationClampsValuesAndPreservesMode), SettingsNormalizationClampsValuesAndPreservesMode),
        (nameof(MissingSettingsFileUsesDefaults), MissingSettingsFileUsesDefaults),
        (nameof(NewSettingsNormalizePinsAndLegacyApplications), NewSettingsNormalizePinsAndLegacyApplications),
        (nameof(HiddenApplicationsAreRemovedFromTheDisplayList), HiddenApplicationsAreRemovedFromTheDisplayList),
        (nameof(DebounceDefaultsMatchRefreshPolicy), DebounceDefaultsMatchRefreshPolicy),
        (nameof(BurstSignalsCoalesceAtFirstDeadline), BurstSignalsCoalesceAtFirstDeadline),
        (nameof(OnlyRelevantTopLevelWindowEventsRequestRefresh), OnlyRelevantTopLevelWindowEventsRequestRefresh),
        (nameof(NativeEventIngressCoalescesStormAndPreservesInvalidations), NativeEventIngressCoalescesStormAndPreservesInvalidations),
        (nameof(NativeEventMonitorCoversResizeAndClearsQueuedShutdownWork), NativeEventMonitorCoversResizeAndClearsQueuedShutdownWork),
        (nameof(PartialEventHookRegistrationIsObservableAndFallbackMaintainsGeometry), PartialEventHookRegistrationIsObservableAndFallbackMaintainsGeometry),
        (nameof(WindowStateComparisonDetectsDisplayedChanges), WindowStateComparisonDetectsDisplayedChanges),
        (nameof(WindowInstanceIdentityRejectsReusedHandlesAndProcesses), WindowInstanceIdentityRejectsReusedHandlesAndProcesses),
        (nameof(PinnedWindowsStayFirstAndRemainderKeepsMruOrder), PinnedWindowsStayFirstAndRemainderKeepsMruOrder),
        (nameof(PinnedWindowKeepsPictureInPictureIndependent), PinnedWindowKeepsPictureInPictureIndependent),
        (nameof(WindowCloseCommandPostsWmCloseOnlyForValidWindows), WindowCloseCommandPostsWmCloseOnlyForValidWindows),
        (nameof(InvisibleWindowCloseGestureRemainsAvailable), InvisibleWindowCloseGestureRemainsAvailable),
        (nameof(NoSeparateBadgeOverlayCanBypassPreviewInput), NoSeparateBadgeOverlayCanBypassPreviewInput),
        (nameof(PreviewCardsProvideAnInvisibleNativeHitSurface), PreviewCardsProvideAnInvisibleNativeHitSurface),
        (nameof(InvalidAppBarRectangleFallsBackBeforeReachingWpf), InvalidAppBarRectangleFallsBackBeforeReachingWpf),
        (nameof(LightweightRenderingAvoidsContinuousCompositionLoop), LightweightRenderingAvoidsContinuousCompositionLoop),
        (nameof(VisibleSidebarSurfacesRemainShadowFree), VisibleSidebarSurfacesRemainShadowFree),
        (nameof(DwmFailuresRemainObservable), DwmFailuresRemainObservable),
        (nameof(DwmVisibilityFailuresPropagateThroughEveryHidePath), DwmVisibilityFailuresPropagateThroughEveryHidePath),
        (nameof(IconBadgeRegistryDoesNotRetainClosedWindowChurn), IconBadgeRegistryDoesNotRetainClosedWindowChurn),
        (nameof(CiRunsFocusedRegressionTests), CiRunsFocusedRegressionTests),
        (nameof(CustomBuildVersionIsConsistent), CustomBuildVersionIsConsistent)
    ];

    private static int Main()
    {
        var failures = 0;
        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{Tests.Length - failures} passed, {failures} failed, {Tests.Length} total");
        return failures == 0 ? 0 : 1;
    }

    private static void DefaultSettingsRemainCompatible()
    {
        Equal(DisplayMode.Fixed, StageManagerSettings.Default.DisplayMode);
        Equal(286d, StageManagerSettings.Default.DisplayWidth);
        Equal(3, StageManagerSettings.Default.CardCount);
    }

    private static void ExistingSettingsJsonRemainsCompatible()
    {
        const string json = """
            {
              "DisplayMode": 1,
              "DisplayWidth": 320,
              "CardCount": 4
            }
            """;

        var settings = JsonSerializer.Deserialize<StageManagerSettings>(json);
        NotNull(settings);
        Equal(DisplayMode.Floating, settings!.DisplayMode);
        Equal(320d, settings.DisplayWidth);
        Equal(4, settings.CardCount);
        Equal(0.82d, settings.PreviewOpacity);
        Equal(0, settings.PinnedApplications.Count);
    }

    private static void SettingsNormalizationClampsValuesAndPreservesMode()
    {
        var tooSmall = SettingsService.Normalize(new StageManagerSettings(DisplayMode.Floating, 12, -4));
        Equal(DisplayMode.Floating, tooSmall.DisplayMode);
        Equal(200d, tooSmall.DisplayWidth);
        Equal(1, tooSmall.CardCount);

        var tooLarge = SettingsService.Normalize(new StageManagerSettings(DisplayMode.Fixed, 900, 99));
        Equal(DisplayMode.Fixed, tooLarge.DisplayMode);
        Equal(420d, tooLarge.DisplayWidth);
        Equal(8, tooLarge.CardCount);
    }

    private static void MissingSettingsFileUsesDefaults()
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"missing-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        Equal(StageManagerSettings.Default, service.Load());
    }

    private static void NewSettingsNormalizePinsAndLegacyApplications()
    {
        var settings = new StageManagerSettings(DisplayMode.Fixed, 320, 6)
        {
            PreviewOpacity = 0.1,
            PinnedApplications = [" chrome ", "CHROME", "Obsidian", "Rhino", "Telegram", "Extra"],
            PinnedWindows =
            [
                new PinnedWindow { Handle = 12, ProcessId = 1, ProcessStartedAtUtcTicks = 2 },
                new PinnedWindow { Handle = 12, ProcessId = 1, ProcessStartedAtUtcTicks = 2 },
                new PinnedWindow { Handle = 0, ProcessId = 3, ProcessStartedAtUtcTicks = 4 }
            ],
            HiddenApplications = [" Discord ", "discord", " Slack "]
        };

        var normalized = SettingsService.Normalize(settings);
        Equal(0.25d, normalized.PreviewOpacity);
        SequenceEqual(["chrome", "Obsidian", "Rhino", "Telegram"], normalized.PinnedApplications);
        Equal(1, normalized.PinnedWindows.Count);
        SequenceEqual(["Discord", "Slack"], normalized.HiddenApplications);
        Equal(1d, SettingsService.Normalize(settings with { PreviewOpacity = 4 }).PreviewOpacity);
    }

    private static void HiddenApplicationsAreRemovedFromTheDisplayList()
    {
        WindowSnapshot[] windows =
        [
            new((nint)1, "Visible", "chrome", false, true),
            new((nint)2, "Hidden", "discord", false, false),
            new((nint)3, "Pinned", "obsidian", false, false)
        ];

        var ordered = WindowDisplayOrder.Apply(windows, [PinnedWindow.From(windows[2])], ["DISCORD"]);
        SequenceEqual([(nint)3, (nint)1], ordered.Select(static item => item.Handle));
    }

    private static void DebounceDefaultsMatchRefreshPolicy()
    {
        var policy = new WindowRefreshPolicy();
        Equal(TimeSpan.FromMilliseconds(20), policy.DebounceInterval);
        Equal(TimeSpan.FromSeconds(5), WindowRefreshPolicy.ReconciliationInterval);
    }

    private static void BurstSignalsCoalesceAtFirstDeadline()
    {
        var policy = new WindowRefreshPolicy();
        var startedAt = new DateTimeOffset(2026, 8, 14, 1, 0, 0, TimeSpan.Zero);

        policy.Signal(startedAt);
        policy.Signal(startedAt.AddMilliseconds(15));

        False(policy.TryConsume(startedAt.AddMilliseconds(19)));
        True(policy.TryConsume(startedAt.AddMilliseconds(20)));
        False(policy.TryConsume(startedAt.AddMilliseconds(100)));
    }

    private static void OnlyRelevantTopLevelWindowEventsRequestRefresh()
    {
        True(WindowRefreshPolicy.IsRelevantEvent(0x0003, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x0016, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x0017, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8000, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8001, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8002, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8003, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x800C, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8017, 0, 0));
        True(WindowRefreshPolicy.IsRelevantEvent(0x8018, 0, 0));

        True(WindowRefreshPolicy.IsRelevantEvent(0x800B, 0, 0));
        False(WindowRefreshPolicy.IsRelevantEvent(0x8002, -4, 0));
        False(WindowRefreshPolicy.IsRelevantEvent(0x8002, 0, 1));
    }

    private static void NativeEventIngressCoalescesStormAndPreservesInvalidations()
    {
        var batcher = new WindowEventBatcher();
        var firstObservedAt = new DateTimeOffset(2026, 8, 14, 2, 0, 0, TimeSpan.Zero);
        var dispatcherRequests = 0;

        for (var index = 0; index < 100; index++)
        {
            if (batcher.Record(NativeMethods.EventObjectLocationChange, (nint)42, firstObservedAt.AddMilliseconds(index)))
            {
                dispatcherRequests++;
            }
        }

        if (batcher.Record(NativeMethods.EventObjectDestroy, (nint)73, firstObservedAt.AddMilliseconds(100)))
        {
            dispatcherRequests++;
        }

        Equal(1, dispatcherRequests);
        var batch = batcher.Drain();
        Equal(firstObservedAt, batch.FirstObservedAt);
        True(batch.RefreshThumbnailGeometry);
        SequenceEqual([(nint)73], batch.InvalidatedHandles);
        True(batcher.Record(NativeMethods.EventObjectShow, (nint)84, firstObservedAt.AddMilliseconds(101)));
    }

    private static void NativeEventMonitorCoversResizeAndClearsQueuedShutdownWork()
    {
        True(WindowEventMonitor.RegisteredEventRanges.Contains(
            (NativeMethods.EventObjectLocationChange, NativeMethods.EventObjectLocationChange)));

        var batcher = new WindowEventBatcher();
        True(batcher.Record(NativeMethods.EventObjectDestroy, (nint)42, DateTimeOffset.UtcNow));
        batcher.Clear();
        False(batcher.TryDrain(out _));
    }

    private static void PartialEventHookRegistrationIsObservableAndFallbackMaintainsGeometry()
    {
        var unhooked = new List<nint>();
        var nextHook = 0;
        var monitor = new WindowEventMonitor(
            System.Windows.Threading.Dispatcher.CurrentDispatcher,
            _ => { },
            (minimum, maximum, callback) => minimum == NativeMethods.EventObjectLocationChange
                ? 0
                : (nint)(++nextHook),
            hook =>
            {
                unhooked.Add(hook);
                return true;
            });

        True(monitor.FailedEventRanges.Contains(
            (NativeMethods.EventObjectLocationChange, NativeMethods.EventObjectLocationChange)));
        Equal(WindowEventMonitor.RegisteredEventRanges.Count - 1, monitor.ActiveHookCount);
        monitor.Dispose();
        Equal(WindowEventMonitor.RegisteredEventRanges.Count - 1, unhooked.Count);

        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));
        True(mainWindow.Contains("RefreshWindowList(refreshThumbnailGeometry: true)", StringComparison.Ordinal));
        True(mainWindow.Contains("ReportWindowEventHookFailure", StringComparison.Ordinal));
    }

    private static void WindowStateComparisonDetectsDisplayedChanges()
    {
        WindowSnapshot[] baseline =
        [
            new((nint)1, "Alpha", "alpha", false, true),
            new((nint)2, "Beta", "beta", true, false)
        ];

        True(WindowListState.Matches(baseline, baseline.ToArray()));
        False(WindowListState.Matches(baseline, baseline.Reverse().ToArray()));
        False(WindowListState.Matches(baseline, [baseline[0] with { Title = "Renamed" }, baseline[1]]));
        False(WindowListState.Matches(baseline, [baseline[0] with { IsForeground = false }, baseline[1]]));
        False(WindowListState.Matches(baseline, [baseline[0], baseline[1] with { IsMinimized = false }]));
        False(WindowListState.Matches(baseline, [baseline[0], baseline[1] with { ProcessName = "other" }]));
        False(WindowListState.Matches(baseline, [baseline[0]]));
    }

    private static void WindowInstanceIdentityRejectsReusedHandlesAndProcesses()
    {
        var original = new WindowSnapshot((nint)42, "Original", "alpha", false, false)
        {
            ProcessId = 700,
            ProcessStartedAtUtcTicks = 1000
        };
        var sameInstance = original with { Title = "Renamed" };
        var reusedProcess = original with { ProcessStartedAtUtcTicks = 2000, ProcessName = "beta" };
        var reusedHandle = original with { ProcessId = 701, ProcessStartedAtUtcTicks = 3000 };

        True(WindowInstanceIdentity.Matches(original, sameInstance));
        False(WindowInstanceIdentity.Matches(original, reusedProcess));
        False(WindowInstanceIdentity.Matches(original, reusedHandle));
        False(WindowListState.Matches([original], [reusedProcess]));
    }

    private static void PinnedWindowsStayFirstAndRemainderKeepsMruOrder()
    {
        WindowSnapshot[] mruWindows =
        [
            new((nint)1, "Latest", "chrome", false, true),
            new((nint)2, "Second", "telegram", false, false),
            new((nint)3, "Pinned Rhino", "rhino", false, false),
            new((nint)4, "Pinned Obsidian", "obsidian", false, false),
            new((nint)5, "Older", "explorer", false, false),
            new((nint)6, "Second Chrome Window", "chrome", false, false)
        ];

        var ordered = WindowDisplayOrder.Apply(
            mruWindows,
            [
                PinnedWindow.From(mruWindows[3]),
                PinnedWindow.From(mruWindows[2]),
                PinnedWindow.From(mruWindows[0])
            ]);

        SequenceEqual([(nint)4, (nint)3, (nint)1, (nint)2, (nint)5, (nint)6], ordered.Select(static item => item.Handle));
    }

    private static void PinnedWindowKeepsPictureInPictureIndependent()
    {
        WindowSnapshot[] windows =
        [
            new((nint)1, "youtube.com", "chrome", false, false)
            {
                IsAlwaysOnTop = true,
                WindowArea = 640L * 360
            },
            new((nint)2, "Arabic lesson - Google Chrome", "chrome", false, true)
            {
                WindowArea = 2560L * 1440
            },
            new((nint)3, "Messages", "telegram", false, false)
            {
                WindowArea = 1200L * 800
            }
        ];

        var ordered = WindowDisplayOrder.Apply(windows, [PinnedWindow.From(windows[1])]);
        SequenceEqual([(nint)2, (nint)1, (nint)3], ordered.Select(static item => item.Handle));
    }

    private static void WindowCloseCommandPostsWmCloseOnlyForValidWindows()
    {
        var posted = new List<(nint Window, uint Message, nint WParam, nint LParam)>();
        var commands = new WindowCommands(
            window => window == (nint)42,
            (window, message, wParam, lParam) =>
            {
                posted.Add((window, message, wParam, lParam));
                return true;
            });

        False(commands.RequestClose((nint)7));
        True(commands.RequestClose((nint)42));
        Equal(1, posted.Count);
        Equal(((nint)42, NativeMethods.WmClose, (nint)0, (nint)0), posted[0]);
    }

    private static void ModifiedLeftClickGesturesUseTheReliableInputPath()
    {
        Equal(PreviewPointerAction.Activate, WindowPreviewGesture.Resolve(true, false, false));
        Equal(PreviewPointerAction.Close, WindowPreviewGesture.Resolve(true, true, false));
        Equal(PreviewPointerAction.TogglePin, WindowPreviewGesture.Resolve(true, false, true));
        Equal(PreviewPointerAction.Close, WindowPreviewGesture.Resolve(true, true, true));
        Equal(PreviewPointerAction.None, WindowPreviewGesture.Resolve(false, true, false));

        var activated = 0;
        var closed = 0;
        var pinned = 0;
        Action activate = () => activated++;
        Action close = () => closed++;
        Action togglePin = () => pinned++;

        False(WindowPreviewGesture.Execute(PreviewPointerAction.Activate, activate, close, togglePin));
        Equal((1, 0, 0), (activated, closed, pinned));
        True(WindowPreviewGesture.Execute(PreviewPointerAction.Close, activate, close, togglePin));
        Equal((1, 1, 0), (activated, closed, pinned));
        True(WindowPreviewGesture.Execute(PreviewPointerAction.TogglePin, activate, close, togglePin));
        Equal((1, 1, 1), (activated, closed, pinned));
    }

    private static void CloseBadgeRequiresDeliberateHoverBeforeClose()
    {
        var arm = new CloseBadgeArm();

        False(arm.IsRevealed);
        False(arm.CanBeginPress);
        arm.Enter();
        False(arm.IsRevealed);
        arm.Reveal();
        True(arm.IsRevealed);
        False(arm.CanBeginPress);
        arm.ConfirmVisualPresented();
        True(arm.CanBeginPress);
        arm.Leave();
        False(arm.IsRevealed);
        False(arm.CanBeginPress);
    }

    private static void CloseBadgeRequiresFreshPressAfterRedIsPresented()
    {
        var arm = new CloseBadgeArm();

        arm.Enter();
        False(arm.BeginPress());
        arm.Reveal();
        arm.ConfirmVisualPresented();
        False(arm.TryRelease());

        True(arm.BeginPress());
        True(arm.TryRelease());
        False(arm.IsRevealed);
        False(arm.CanBeginPress);
    }

    private static void HidingPanelExplicitlyDisarmsEveryCloseBadge()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));
        var preview = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "Controls", "WindowPreviewControl.xaml.cs"));

        var normalizedMainWindow = mainWindow.Replace("\r\n", "\n", StringComparison.Ordinal);
        True(normalizedMainWindow.Contains("private void ResetCloseInteractions()", StringComparison.Ordinal));
        True(normalizedMainWindow.Contains("ResetCloseInteractions();\n        _isPanelVisible = false;", StringComparison.Ordinal));
        True(preview.Contains("internal void ResetCloseInteractionState()", StringComparison.Ordinal));
    }

    private static void CloseBadgeHitTargetDoesNotConsumeTheEmptyRail()
    {
        True(CloseBadgeHitTest.Contains(10, 20, 10, 20, 30, 30));
        True(CloseBadgeHitTest.Contains(6, 16, 10, 20, 30, 30));
        True(CloseBadgeHitTest.Contains(44, 54, 10, 20, 30, 30));
        False(CloseBadgeHitTest.Contains(5.9, 16, 10, 20, 30, 30));
        False(CloseBadgeHitTest.Contains(10, 100, 10, 20, 30, 30));
    }

    private static void CloseBadgeArmedAppearanceIsRedAndExplicit()
    {
        var normal = CloseBadgeVisual.Resolve(false);
        var armed = CloseBadgeVisual.Resolve(true);

        Equal(0xFFC62828u, armed.BackgroundArgb);
        Equal(0xFFFF6B6Bu, armed.BorderArgb);
        Equal(0.25d, armed.IconOpacity);
        True(armed.ShowCloseGlyph);
        Equal(1d, normal.IconOpacity);
        False(normal.ShowCloseGlyph);
        False(normal.BackgroundArgb == armed.BackgroundArgb);
    }

    private static void IconOverlayClaimsOnlyVisibleBadgeHitAreas()
    {
        var badges = new[]
        {
            new OverlayBadgeBounds((nint)42, 10, 20, 30, 30),
            new OverlayBadgeBounds((nint)84, 10, 90, 30, 30)
        };

        Equal((nint)42, IconOverlayHitTest.FindBadge(6, 16, badges));
        Equal((nint)42, IconOverlayHitTest.FindBadge(44, 54, badges));
        Equal((nint)84, IconOverlayHitTest.FindBadge(20, 100, badges));
        Equal((nint)0, IconOverlayHitTest.FindBadge(100, 100, badges));
        Equal((nint)0, IconOverlayHitTest.FindBadge(double.NaN, 20, badges));
    }

    private static void VisibleBadgeOverlayOwnsHoverAndCloseInput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overlay = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "IconOverlayWindow.xaml.cs"));
        var overlayXaml = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "IconOverlayWindow.xaml"));
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));

        True(overlay.Contains("style &= ~NativeMethods.WsExTransparent", StringComparison.Ordinal));
        True(overlay.Contains("NativeMethods.WmNcHitTest", StringComparison.Ordinal));
        True(overlay.Contains("NativeMethods.HtTransparent", StringComparison.Ordinal));
        True(overlay.Contains("NativeMethods.HtClient", StringComparison.Ordinal));
        True(overlay.Contains("private readonly CloseBadgeArm _closeBadgeArm", StringComparison.Ordinal));
        True(overlay.Contains("WindowCloseRequested?.Invoke", StringComparison.Ordinal));
        True(overlayXaml.Contains("IsHitTestVisible=\"True\"", StringComparison.Ordinal));
        True(mainWindow.Contains("_iconOverlay.WindowCloseRequested +=", StringComparison.Ordinal));
    }

    private static void BadgeDragOutsideRetainsOwnershipThroughButtonUp()
    {
        var ownership = new OverlayBadgePressOwnership();

        ownership.BeginBadgePress();
        True(ownership.OwnsButtonUp);

        ownership.CancelCloseEligibility();
        True(ownership.OwnsButtonUp);
        False(ownership.CloseEligible);

        True(ownership.CompleteButtonUp());
        False(ownership.OwnsButtonUp);
        False(ownership.CompleteButtonUp());
    }

    private static void InvisibleWindowCloseGestureRemainsAvailable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var preview = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "Controls", "WindowPreviewControl.xaml.cs"));
        var overlay = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "IconOverlayWindow.xaml.cs"));
        var overlayXaml = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "IconOverlayWindow.xaml"));
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));

        True(preview.Contains("Keyboard.Modifiers.HasFlag(ModifierKeys.Control)", StringComparison.Ordinal));
        True(preview.Contains("WindowCloseRequested", StringComparison.Ordinal));
        True(preview.Contains("Close window", StringComparison.Ordinal));
        False(preview.Contains("CloseBadge", StringComparison.Ordinal));
        False(preview.Contains("MouseButton.Middle", StringComparison.Ordinal));
        True(mainWindow.Contains("RequestClose", StringComparison.Ordinal));
        True(mainWindow.Contains("WindowCloseRequested", StringComparison.Ordinal));
        False(overlay.Contains("WindowCloseRequested", StringComparison.Ordinal));
        False(overlay.Contains("CloseBadge", StringComparison.Ordinal));
        True(overlay.Contains("NativeMethods.WsExTransparent", StringComparison.Ordinal));
        True(overlayXaml.Contains("IsHitTestVisible=\"False\"", StringComparison.Ordinal));
    }

    private static void NoSeparateBadgeOverlayCanBypassPreviewInput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));

        False(mainWindow.Contains("IconOverlayWindow", StringComparison.Ordinal));
        False(mainWindow.Contains("UpdateIconOverlay", StringComparison.Ordinal));
        False(mainWindow.Contains("UpdateBadges", StringComparison.Ordinal));
    }

    private static void PreviewCardsProvideAnInvisibleNativeHitSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var previewXaml = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "Controls", "WindowPreviewControl.xaml"));
        var mainXaml = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml"));

        True(previewXaml.Contains("Background=\"#01000000\"", StringComparison.Ordinal));
        True(mainXaml.Contains("Background=\"Transparent\"", StringComparison.Ordinal));
    }

    private static void InvalidAppBarRectangleFallsBackBeforeReachingWpf()
    {
        var requested = Rectangle.FromLTRB(0, 0, 410, 2160);
        var transientInvalid = Rectangle.FromLTRB(0, 0, 410, -48);

        Equal(requested, AppBarService.NormalizeReservedRectangle(requested, transientInvalid));

        var valid = Rectangle.FromLTRB(0, 0, 410, 2160);
        Equal(valid, AppBarService.NormalizeReservedRectangle(requested, valid));
    }

    private static void LightweightRenderingAvoidsContinuousCompositionLoop()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml"));

        False(mainWindow.Contains("CompositionTarget.Rendering", StringComparison.Ordinal));
        False(mainWindowXaml.Contains("GlassFrameThickness=\"-1\"", StringComparison.Ordinal));
    }

    private static void VisibleSidebarSurfacesRemainShadowFree()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overlay = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "IconOverlayWindow.xaml.cs"));
        var preview = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "Controls", "WindowPreviewControl.xaml"));

        False(overlay.Contains("DropShadowEffect", StringComparison.Ordinal));
        False(preview.Contains("DropShadowEffect", StringComparison.Ordinal));
    }

    private static void DwmFailuresRemainObservable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));
        var thumbnail = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "Services", "DwmThumbnail.cs"));

        True(mainWindow.Contains("ReportDwmFailure", StringComparison.Ordinal));
        True(mainWindow.Contains("ShowBalloonTip", StringComparison.Ordinal));
        True(thumbnail.Contains("LastError", StringComparison.Ordinal));
    }

    private static void DwmVisibilityFailuresPropagateThroughEveryHidePath()
    {
        const int expectedError = unchecked((int)0x80004005);
        var updateCalls = 0;
        var unregistered = 0;
        using (var thumbnail = new DwmThumbnail(
                   (nint)99,
                   (nint handle, ref NativeMethods.DwmThumbnailProperties properties) =>
                   {
                       updateCalls++;
                       return expectedError;
                   },
                   handle =>
                   {
                       unregistered++;
                       return 0;
                   }))
        {
            Equal(expectedError, thumbnail.SetVisible(false));
            Equal(expectedError, thumbnail.LastError);
            Equal(1, updateCalls);
        }

        Equal(1, unregistered);
        var repositoryRoot = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "MainWindow.xaml.cs"));
        True(mainWindow.Contains("HideThumbnail(thumbnail)", StringComparison.Ordinal));
        True(mainWindow.Contains("control.Opacity <= 0", StringComparison.Ordinal));
        Equal(1, mainWindow.Split("thumbnail.SetVisible(false);", StringSplitOptions.None).Length - 1);
        Equal(2, mainWindow.Split("HideThumbnail(thumbnail);", StringSplitOptions.None).Length - 1);
    }

    private static void IconBadgeRegistryDoesNotRetainClosedWindowChurn()
    {
        var registry = new IconBadgeRegistry<object>();
        var removed = 0;

        for (var value = 1; value <= 1000; value++)
        {
            var handle = (nint)value;
            registry.RemoveObsolete([handle], _ => removed++);
            if (!registry.TryGetValue(handle, out _))
            {
                registry.Add(handle, new object());
            }
        }

        Equal(1, registry.Count);
        Equal(999, removed);
        registry.Clear(_ => removed++);
        Equal(0, registry.Count);
        Equal(1000, removed);
    }

    private static void CiRunsFocusedRegressionTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        var buildWorkflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "build.yml"));
        var releaseWorkflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "release.yml"));
        const string testCommand = "dotnet test .\\StageManager.Tests\\StageManager.Tests.csproj";

        True(buildWorkflow.Contains(testCommand, StringComparison.Ordinal));
        True(releaseWorkflow.Contains(testCommand, StringComparison.Ordinal));
    }

    private static void CustomBuildVersionIsConsistent()
    {
        Equal(new Version(0, 0, 3, 0), typeof(StageManagerSettings).Assembly.GetName().Version!);

        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "StageManager.csproj"));
        var applicationManifest = File.ReadAllText(Path.Combine(repositoryRoot, "StageManager", "app.manifest"));
        var installerScript = File.ReadAllText(Path.Combine(repositoryRoot, "installer", "build-installer.ps1"));
        var innoSetup = File.ReadAllText(Path.Combine(repositoryRoot, "installer", "StageManager.iss"));
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        var chineseReadme = File.ReadAllText(Path.Combine(repositoryRoot, "README.zh-CN.md"));

        True(project.Contains("<Version>0.0.3</Version>", StringComparison.Ordinal));
        True(project.Contains("<AssemblyVersion>0.0.3.0</AssemblyVersion>", StringComparison.Ordinal));
        True(project.Contains("<FileVersion>0.0.3.0</FileVersion>", StringComparison.Ordinal));
        True(applicationManifest.Contains("assemblyIdentity version=\"0.0.3.0\"", StringComparison.Ordinal));
        True(installerScript.Contains("[string]$Version = '0.0.3'", StringComparison.Ordinal));
        True(innoSetup.Contains("#define MyAppVersion \"0.0.3\"", StringComparison.Ordinal));
        True(innoSetup.Contains("#define MyVersionInfoVersion \"0.0.3.0\"", StringComparison.Ordinal));
        False(readme.Contains("0.0.2-x64", StringComparison.Ordinal));
        False(readme.Contains("Version `0.0.2`", StringComparison.Ordinal));
        False(chineseReadme.Contains("0.0.2-x64", StringComparison.Ordinal));
        False(chineseReadme.Contains("`0.0.2` 版本", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StageManager", "StageManager.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the StageBar repository root.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, got {actual}.");
        }
    }

    private static void NotNull(object? actual)
    {
        if (actual is null)
        {
            throw new InvalidOperationException("Expected a non-null value.");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
        }
    }

    private static void True(bool actual)
    {
        if (!actual)
        {
            throw new InvalidOperationException("Expected true, got false.");
        }
    }

    private static void False(bool actual)
    {
        if (actual)
        {
            throw new InvalidOperationException("Expected false, got true.");
        }
    }
}
