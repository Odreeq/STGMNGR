using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using StageManager.Controls;
using StageManager.Interop;
using StageManager.Models;
using StageManager.Services;
using Application = System.Windows.Application;
using DrawingIcon = System.Drawing.Icon;
using FormsCursor = System.Windows.Forms.Cursor;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Point = System.Windows.Point;

namespace StageManager;

public partial class MainWindow : Window
{
    private const int HotKeyId = 0x534D;
    private static readonly TimeSpan EdgePollInterval = TimeSpan.FromMilliseconds(90);

    private readonly WindowCatalog _windowCatalog = new();
    private readonly WindowIconService _windowIconService = new();
    private readonly WindowCommands _windowCommands = new();
    private readonly SettingsService _settingsService = new();
    private readonly WindowRefreshPolicy _refreshPolicy = new();
    private readonly Dictionary<nint, DwmThumbnail> _thumbnails = [];
    private readonly HashSet<nint> _invalidatedWindowHandles = [];
    private readonly DispatcherTimer _reconciliationTimer = new()
    {
        Interval = WindowRefreshPolicy.ReconciliationInterval
    };
    private readonly DispatcherTimer _windowEventDebounceTimer = new(DispatcherPriority.Input);
    private readonly DispatcherTimer _edgeTimer = new() { Interval = EdgePollInterval };
    private readonly ScrollOffsetAnimator _scrollAnimator;
    private readonly DrawingIcon _applicationIcon;
    private readonly NotifyIcon _trayIcon;
    private StageManagerSettings _settings;
    private SettingsWindow? _settingsWindow;

    private AppBarService? _appBar;
    private WindowEventMonitor? _windowEventMonitor;
    private HwndSource? _source;
    private nint _windowHandle;
    private DateTime _lastInsideAt = DateTime.UtcNow;
    private bool _isPanelVisible;
    private bool _isExiting;
    private bool _dwmFailureReported;
    private bool _eventHookFailureReported;
    private bool _thumbnailGeometryRefreshPending;

    private int _carouselStartIndex;
    private int _visibleCardCount = 3;
    private double _itemExtent = 158;
    private double _shownLeft;
    private Screen? _currentScreen;
    private DateTime _lastAppBarNotification = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _settings = _settingsService.Load();
        _scrollAnimator = new ScrollOffsetAnimator(PreviewScrollViewer);
        _applicationIcon = LoadApplicationIcon();
        _trayIcon = CreateTrayIcon();
        _windowEventDebounceTimer.Interval = _refreshPolicy.DebounceInterval;
        _windowEventDebounceTimer.Tick += WindowEventDebounceTimer_Tick;
        _reconciliationTimer.Tick += (_, _) => RefreshWindowList(refreshThumbnailGeometry: true);
        _edgeTimer.Tick += (_, _) => PollScreenEdge();
        MouseEnter += Panel_MouseEnter;
        MouseLeave += Panel_MouseLeave;
        LocationChanged += (_, _) => Dispatcher.BeginInvoke(UpdateVisualLayers, DispatcherPriority.Render);
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(UpdateVisualLayers, DispatcherPriority.Render);
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowProcedure);
        _appBar = new AppBarService(_windowHandle);
        _windowEventMonitor = new WindowEventMonitor(Dispatcher, QueueWindowCatalogRefresh);
        if (_windowEventMonitor.FailedEventRanges.Count > 0)
        {
            ReportWindowEventHookFailure();
        }

        var style = NativeMethods.GetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GwlExStyle, (nint)style);
        NativeMethods.RegisterHotKey(
            _windowHandle,
            HotKeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt,
            NativeMethods.VkSpace);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {

        _currentScreen = Screen.FromPoint(FormsCursor.Position);
        ApplySettingsLayout(initial: true);
        _reconciliationTimer.Start();
        UpdateEdgeTimerState();
    }

    private void QueueWindowCatalogRefresh(WindowEventBatch batch)
    {
        if (_isExiting)
        {
            return;
        }

        _invalidatedWindowHandles.UnionWith(batch.InvalidatedHandles);
        _thumbnailGeometryRefreshPending |= batch.RefreshThumbnailGeometry;
        _refreshPolicy.Signal(batch.FirstObservedAt);
        if (!_windowEventDebounceTimer.IsEnabled)
        {
            var remaining = _refreshPolicy.GetRemainingDelay(DateTimeOffset.UtcNow);
            _windowEventDebounceTimer.Interval = remaining > TimeSpan.Zero
                ? remaining
                : TimeSpan.FromMilliseconds(1);
            _windowEventDebounceTimer.Start();
        }
    }

    private void WindowEventDebounceTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (!_refreshPolicy.TryConsume(now))
        {
            var remaining = _refreshPolicy.GetRemainingDelay(now);
            if (remaining > TimeSpan.Zero)
            {
                _windowEventDebounceTimer.Stop();
                _windowEventDebounceTimer.Interval = remaining;
                _windowEventDebounceTimer.Start();
            }

            return;
        }

        _windowEventDebounceTimer.Stop();
        _windowEventDebounceTimer.Interval = _refreshPolicy.DebounceInterval;
        RefreshWindowList();
    }

    private void RefreshWindowList(bool force = false, bool refreshThumbnailGeometry = false)
    {
        _thumbnailGeometryRefreshPending |= refreshThumbnailGeometry;
        InvalidateDestroyedWindowInstances();
        var catalogWindows = _windowCatalog.GetWindows();
        MigrateLegacyApplicationPins(catalogWindows);
        PruneClosedPinnedWindows(catalogWindows);
        var windows = WindowDisplayOrder.Apply(
            catalogWindows,
            _settings.PinnedWindows,
            _settings.HiddenApplications);
        var currentWindows = PreviewList.Children
            .OfType<WindowPreviewControl>()
            .Select(static control => control.Snapshot)
            .ToArray();

        if (!force && WindowListState.Matches(windows, currentWindows))
        {
            UpdateControlPresentation();
            if (_thumbnailGeometryRefreshPending)
            {
                _thumbnailGeometryRefreshPending = false;
                UpdateVisualLayers();
            }

            return;
        }

        _thumbnailGeometryRefreshPending = false;

        if (force)
        {
            ResetDisplayedWindows();
        }

        var layout = LayoutMetrics.Calculate(_settings.DisplayWidth);
        _itemExtent = layout.ItemExtent;
        var desiredHandles = windows.Select(static window => window.Handle).ToHashSet();
        foreach (var obsolete in PreviewList.Children
                     .OfType<WindowPreviewControl>()
                     .Where(control => !desiredHandles.Contains(control.Snapshot.Handle))
                     .ToArray())
        {
            RemoveWindowControl(obsolete);
        }

        var existingByHandle = PreviewList.Children
            .OfType<WindowPreviewControl>()
            .ToDictionary(static control => control.Snapshot.Handle);
        var canPin = _settings.PinnedWindows.Count < StageManagerSettings.MaximumPinnedWindows;
        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];
            var isPinned = WindowDisplayOrder.IsPinned(window, _settings.PinnedWindows);
            var canReuse = existingByHandle.TryGetValue(window.Handle, out var control);
            if (canReuse && !WindowInstanceIdentity.Matches(control!.Snapshot, window))
            {
                RemoveWindowControl(control!);
                existingByHandle.Remove(window.Handle);
                canReuse = false;
            }

            if (!canReuse)
            {
                control = new WindowPreviewControl(
                    window,
                    _windowIconService.GetIcon(window.Handle),
                    layout,
                    _settings.PreviewOpacity,
                    isPinned,
                    canPin);
                control.WindowSelected += Preview_WindowSelected;
                control.PinToggled += Preview_PinToggled;
                control.ApplicationHidden += Preview_ApplicationHidden;
                control.WindowCloseRequested += Preview_WindowCloseRequested;
                PreviewList.Children.Insert(Math.Min(index, PreviewList.Children.Count), control);
                existingByHandle.Add(window.Handle, control);
            }
            else
            {
                control!.UpdateState(window, _settings.PreviewOpacity, isPinned, canPin);
                var currentIndex = PreviewList.Children.IndexOf(control);
                if (currentIndex != index)
                {
                    PreviewList.Children.RemoveAt(currentIndex);
                    PreviewList.Children.Insert(index, control);
                }
            }
        }

        _carouselStartIndex = Math.Clamp(_carouselStartIndex, 0, Math.Max(0, windows.Count - _visibleCardCount));
        PreviewScrollViewer.ScrollToVerticalOffset(_carouselStartIndex * _itemExtent);
        UpdateCarouselVisuals();
        EmptyState.Visibility = windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(RegisterThumbnails, DispatcherPriority.Loaded);
    }

    private void InvalidateDestroyedWindowInstances()
    {
        if (_invalidatedWindowHandles.Count == 0)
        {
            return;
        }

        foreach (var control in PreviewList.Children
                     .OfType<WindowPreviewControl>()
                     .Where(control => _invalidatedWindowHandles.Contains(control.Snapshot.Handle))
                     .ToArray())
        {
            RemoveWindowControl(control);
        }

        foreach (var handle in _invalidatedWindowHandles)
        {
            if (_thumbnails.Remove(handle, out var thumbnail))
            {
                thumbnail.Dispose();
            }
        }

        _invalidatedWindowHandles.Clear();
    }

    private void RemoveWindowControl(WindowPreviewControl control)
    {
        control.WindowSelected -= Preview_WindowSelected;
        control.PinToggled -= Preview_PinToggled;
        control.ApplicationHidden -= Preview_ApplicationHidden;
        control.WindowCloseRequested -= Preview_WindowCloseRequested;
        control.DisposeInteractionState();
        PreviewList.Children.Remove(control);
        if (_thumbnails.Remove(control.Snapshot.Handle, out var thumbnail))
        {
            thumbnail.Dispose();
        }
    }

    private void ResetDisplayedWindows()
    {
        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            control.WindowSelected -= Preview_WindowSelected;
            control.PinToggled -= Preview_PinToggled;
            control.ApplicationHidden -= Preview_ApplicationHidden;
            control.WindowCloseRequested -= Preview_WindowCloseRequested;
            control.DisposeInteractionState();
        }

        DisposeThumbnails();
        PreviewList.Children.Clear();
    }

    private void UpdateControlPresentation()
    {
        var canPin = _settings.PinnedWindows.Count < StageManagerSettings.MaximumPinnedWindows;
        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            control.UpdateState(
                control.Snapshot,
                _settings.PreviewOpacity,
                WindowDisplayOrder.IsPinned(control.Snapshot, _settings.PinnedWindows),
                canPin);
        }
    }

    private void RegisterThumbnails()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            if (_thumbnails.ContainsKey(control.Snapshot.Handle))
            {
                continue;
            }

            if (DwmThumbnail.TryCreate(_windowHandle, control.Snapshot.Handle, out var error) is { } thumbnail)
            {
                _thumbnails.Add(control.Snapshot.Handle, thumbnail);
            }
            else
            {
                ReportDwmFailure(error);
            }
        }

        UpdateVisualLayers();
    }

    private void UpdateThumbnailBounds()
    {
        if (!_isPanelVisible ||
            PresentationSource.FromVisual(this)?.CompositionTarget is not { } compositionTarget ||
            PreviewScrollViewer.ViewportWidth <= 0 ||
            PreviewScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var toDevice = compositionTarget.TransformToDevice;
        var viewportOrigin = PreviewScrollViewer.TransformToAncestor(this).Transform(new Point(0, 0));
        var viewportBottomRight = PreviewScrollViewer.TransformToAncestor(this).Transform(new Point(
            PreviewScrollViewer.ViewportWidth,
            PreviewScrollViewer.ViewportHeight));
        var deviceViewportOrigin = toDevice.Transform(viewportOrigin);
        var deviceViewportBottomRight = toDevice.Transform(viewportBottomRight);
        var clipBounds = new NativeMethods.Rect
        {
            Left = (int)Math.Floor(deviceViewportOrigin.X),
            Top = (int)Math.Floor(deviceViewportOrigin.Y),
            Right = (int)Math.Ceiling(deviceViewportBottomRight.X),
            Bottom = (int)Math.Ceiling(deviceViewportBottomRight.Y)
        };

        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            if (!_thumbnails.TryGetValue(control.Snapshot.Handle, out var thumbnail))
            {
                continue;
            }

            if (!control.ThumbnailSurface.IsVisible ||
                control.Opacity <= 0 ||
                control.ThumbnailSurface.ActualWidth <= 0 ||
                control.ThumbnailSurface.ActualHeight <= 0)
            {
                HideThumbnail(thumbnail);
                continue;
            }

            try
            {
                var transform = control.ThumbnailSurface.TransformToAncestor(this);
                var origin = transform.Transform(new Point(0, 0));
                var bottomRight = transform.Transform(new Point(
                    control.ThumbnailSurface.ActualWidth,
                    control.ThumbnailSurface.ActualHeight));
                var deviceOrigin = toDevice.Transform(origin);
                var deviceBottomRight = toDevice.Transform(bottomRight);

                thumbnail.Update(
                    new NativeMethods.Rect
                    {
                        Left = (int)Math.Round(deviceOrigin.X),
                        Top = (int)Math.Round(deviceOrigin.Y),
                        Right = (int)Math.Round(deviceBottomRight.X),
                        Bottom = (int)Math.Round(deviceBottomRight.Y)
                    },
                    clipBounds,
                    (byte)Math.Clamp((int)Math.Round(control.PreviewOpacity * 255), 0, 255));
                if (thumbnail.LastError != 0)
                {
                    ReportDwmFailure(thumbnail.LastError);
                }
            }
            catch (InvalidOperationException)
            {
                HideThumbnail(thumbnail);
            }
        }
    }

    private void UpdateVisualLayers()
    {
        UpdateThumbnailBounds();
    }

    private void ReportDwmFailure(int error)
    {
        if (_dwmFailureReported || _isExiting)
        {
            return;
        }

        _dwmFailureReported = true;
        _trayIcon.BalloonTipTitle = "StageBar live preview unavailable";
        _trayIcon.BalloonTipText = $"Windows DWM could not render a live preview (0x{unchecked((uint)error):X8}).";
        _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _trayIcon.ShowBalloonTip(5000);
    }

    private void HideThumbnail(DwmThumbnail thumbnail)
    {
        var error = thumbnail.SetVisible(false);
        if (error != 0)
        {
            ReportDwmFailure(error);
        }
    }

    private void ReportWindowEventHookFailure()
    {
        if (_eventHookFailureReported || _isExiting)
        {
            return;
        }

        _eventHookFailureReported = true;
        _trayIcon.BalloonTipTitle = "StageBar real-time monitoring limited";
        _trayIcon.BalloonTipText = "A Windows event hook was unavailable. StageBar will keep previews synchronized with its five-second fallback.";
        _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _trayIcon.ShowBalloonTip(5000);
    }

    private void PreviewScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        UpdateCarouselVisuals();
        Dispatcher.BeginInvoke(UpdateVisualLayers, DispatcherPriority.Render);
    }

    private void PreviewScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (PreviewList.Children.Count <= _visibleCardCount)
        {
            return;
        }

        var direction = e.Delta < 0 ? 1 : -1;
        var maxStart = Math.Max(0, PreviewList.Children.Count - _visibleCardCount);
        var nextIndex = Math.Clamp(_carouselStartIndex + direction, 0, maxStart);
        if (nextIndex == _carouselStartIndex)
        {
            return;
        }

        _carouselStartIndex = nextIndex;
        _scrollAnimator.AnimateTo(_carouselStartIndex * _itemExtent);
    }

    private void UpdateCarouselVisuals()
    {
        var count = PreviewList.Children.Count;
        var visibleCount = Math.Min(_visibleCardCount, Math.Max(1, count));
        var centerIndex = count <= 1
            ? 0d
            : PreviewScrollViewer.VerticalOffset / _itemExtent + (visibleCount - 1) / 2d;
        var maximumVisibleDistance = Math.Max(0, (visibleCount - 1) / 2d);

        var index = 0;
        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            control.SetCarouselPosition(index - centerIndex, maximumVisibleDistance);
            index++;
        }
    }

    private void Preview_WindowSelected(object? sender, WindowSnapshot window)
    {
        if (sender is not WindowPreviewControl)
        {
            return;
        }

        WindowCatalog.Activate(window.Handle);
        if (_settings.DisplayMode is DisplayMode.Floating)
        {
            HidePanel();
        }
    }

    private void Preview_PinToggled(object? sender, WindowSnapshot window)
    {
        var pinned = _settings.PinnedWindows.ToList();
        var existingIndex = pinned.FindIndex(pin => pin.Matches(window));
        if (existingIndex >= 0)
        {
            pinned.RemoveAt(existingIndex);
        }
        else if (pinned.Count < StageManagerSettings.MaximumPinnedWindows)
        {
            pinned.Add(PinnedWindow.From(window));
        }
        else
        {
            return;
        }

        _settings = SettingsService.Normalize(_settings with
        {
            PinnedWindows = pinned,
            PinnedApplications = []
        });
        SaveSettings();
        // A pin always gets a stable, immediately visible place at the top.
        // Keeping an old carousel offset made a newly pinned preview appear to
        // jump between card positions as the window list refreshed.
        _carouselStartIndex = 0;
        RefreshWindowList();
    }

    private void MigrateLegacyApplicationPins(IReadOnlyList<WindowSnapshot> windows)
    {
        if (_settings.PinnedApplications.Count == 0)
        {
            return;
        }

        var pinned = _settings.PinnedWindows.ToList();
        foreach (var application in _settings.PinnedApplications)
        {
            if (pinned.Count >= StageManagerSettings.MaximumPinnedWindows)
            {
                break;
            }

            var preferred = WindowDisplayOrder.FindPreferredWindow(windows.Where(window =>
                string.Equals(window.ProcessName, application, StringComparison.OrdinalIgnoreCase) &&
                !_settings.HiddenApplications.Contains(window.ProcessName, StringComparer.OrdinalIgnoreCase)));
            if (preferred is not null && !pinned.Any(pin => pin.Matches(preferred)))
            {
                pinned.Add(PinnedWindow.From(preferred));
            }
        }

        _settings = SettingsService.Normalize(_settings with
        {
            PinnedWindows = pinned,
            PinnedApplications = []
        });
        SaveSettings();
    }

    private void PruneClosedPinnedWindows(IReadOnlyList<WindowSnapshot> windows)
    {
        var pinned = _settings.PinnedWindows
            .Where(pin => windows.Any(pin.Matches))
            .ToArray();
        if (pinned.Length == _settings.PinnedWindows.Count)
        {
            return;
        }

        _settings = SettingsService.Normalize(_settings with { PinnedWindows = pinned });
        SaveSettings();
    }

    private void Preview_ApplicationHidden(object? sender, WindowSnapshot window)
    {
        var hidden = _settings.HiddenApplications.ToList();
        if (hidden.Contains(window.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        hidden.Add(window.ProcessName);
        _settings = SettingsService.Normalize(_settings with
        {
            HiddenApplications = hidden
        });
        SaveSettings();
        _carouselStartIndex = 0;
        RefreshWindowList();
    }

    private void Preview_WindowCloseRequested(object? sender, WindowSnapshot window)
    {
        _windowCommands.RequestClose(window.Handle);
    }

    private void ClearPinnedWindows()
    {
        if (_settings.PinnedWindows.Count == 0)
        {
            return;
        }

        _settings = SettingsService.Normalize(_settings with
        {
            PinnedWindows = [],
            PinnedApplications = []
        });
        SaveSettings();
        RefreshWindowList();
    }

    private void ShowApplicationInStageBar(string application)
    {
        var hidden = _settings.HiddenApplications
            .Where(item => !string.Equals(item, application, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (hidden.Length == _settings.HiddenApplications.Count)
        {
            return;
        }

        _settings = SettingsService.Normalize(_settings with { HiddenApplications = hidden });
        SaveSettings();
        RefreshWindowList();
    }

    private void ShowAllApplicationsInStageBar()
    {
        if (_settings.HiddenApplications.Count == 0)
        {
            return;
        }

        _settings = SettingsService.Normalize(_settings with { HiddenApplications = [] });
        SaveSettings();
        RefreshWindowList();
    }

    private void PollScreenEdge()
    {
        if (_settings.DisplayMode is DisplayMode.Fixed)
        {
            return;
        }

        var cursor = FormsCursor.Position;
        var screen = Screen.FromPoint(cursor);
        var isAtLeftEdge = cursor.X <= screen.Bounds.Left + 2 &&
                           cursor.Y >= screen.Bounds.Top &&
                           cursor.Y <= screen.Bounds.Bottom;
        var isInsidePanel = _isPanelVisible && IsCursorInsideWindow(cursor);

        if (isAtLeftEdge)
        {
            if (_currentScreen?.DeviceName != screen.DeviceName)
            {
                _currentScreen = screen;
                ApplySettingsLayout(initial: false);
            }
            ShowPanel();
            _lastInsideAt = DateTime.UtcNow;
            return;
        }

        if (isInsidePanel)
        {
            _lastInsideAt = DateTime.UtcNow;
        }
        else if (_isPanelVisible && DateTime.UtcNow - _lastInsideAt > TimeSpan.FromMilliseconds(650))
        {
            HidePanel();
        }
    }

    private bool IsCursorInsideWindow(System.Drawing.Point cursor)
    {
        if (_windowHandle != 0 && NativeMethods.GetWindowRect(_windowHandle, out var bounds))
        {
            return cursor.X >= bounds.Left && cursor.X <= bounds.Right &&
                   cursor.Y >= bounds.Top && cursor.Y <= bounds.Bottom;
        }

        return false;
    }

    private void ApplySettingsLayout(bool initial)
    {
        _settings = SettingsService.Normalize(_settings);
        var screen = _currentScreen ?? Screen.FromPoint(FormsCursor.Position);
        _currentScreen = screen;
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var availableHeightDips = screen.WorkingArea.Height / scaleY;
        var maximumCards = LayoutMetrics.CalculateMaximumCardCount(
            _settings.DisplayWidth,
            availableHeightDips);
        _visibleCardCount = Math.Min(_settings.CardCount, maximumCards);
        var layout = LayoutMetrics.Calculate(_settings.DisplayWidth);
        _itemExtent = layout.ItemExtent;
        PreviewScrollViewer.Width = Math.Max(180, _settings.DisplayWidth - 10);
        PreviewScrollViewer.Height = _visibleCardCount * _itemExtent;

        // StageBar is an overlay in both modes.  Do not reserve an AppBar strip:
        // normal windows can now extend behind the translucent previews.
        _appBar?.Unregister();
        var workArea = screen.WorkingArea;
        Width = _settings.DisplayWidth;
        Height = Math.Min(
            availableHeightDips,
            _visibleCardCount * _itemExtent + 24);
        _shownLeft = screen.Bounds.Left / scaleX;
        Left = _shownLeft;
        Top = (workArea.Top + (workArea.Height - Height * scaleY) / 2.0) / scaleY;

        RefreshWindowList(force: true);
        UpdateEdgeTimerState();
        if (_settings.DisplayMode is DisplayMode.Fixed)
        {
            _isPanelVisible = false;
            ShowPanel();
        }
        else
        {
            HidePanel(force: true);
            if (!initial)
            {
                _lastInsideAt = DateTime.UtcNow;
            }
        }
    }

    private void UpdateEdgeTimerState()
    {
        if (_settings.DisplayMode is DisplayMode.Floating)
        {
            _edgeTimer.Start();
        }
        else
        {
            _edgeTimer.Stop();
        }
    }

    private void ShowPanel()
    {
        if (_isPanelVisible)
        {
            return;
        }

        _isPanelVisible = true;
        Visibility = Visibility.Visible;
        if (_windowHandle != 0)
        {
            NativeMethods.ShowWindowAsync(_windowHandle, NativeMethods.SwShowNoActivate);
        }

        Opacity = 1;
        if (_settings.DisplayMode is DisplayMode.Floating)
        {
            Left = _shownLeft;
        }
        Dispatcher.BeginInvoke(UpdateVisualLayers, DispatcherPriority.Render);
    }

    private void HidePanel(bool force = false)
    {
        if (_settings.DisplayMode is DisplayMode.Fixed && !force)
        {
            return;
        }

        if (!_isPanelVisible && !force)
        {
            return;
        }

        _isPanelVisible = false;
        Opacity = 0;
        Visibility = Visibility.Hidden;
        Left = _shownLeft;
    }


    private void TogglePanel()
    {
        if (_settings.DisplayMode is DisplayMode.Fixed)
        {
            ShowPanel();
            return;
        }

        if (_isPanelVisible)
        {
            HidePanel();
        }
        else
        {
            _currentScreen = Screen.FromPoint(FormsCursor.Position);
            ApplyFloatingPosition();
            ShowPanel();
        }
    }

    private void ApplyFloatingPosition()
    {
        if (_settings.DisplayMode is not DisplayMode.Floating || _currentScreen is null)
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        var workArea = _currentScreen.WorkingArea;
        _shownLeft = _currentScreen.Bounds.Left / scaleX;
        Left = _shownLeft;
        Top = (workArea.Top + (workArea.Height - Height * scaleY) / 2.0) / scaleY;
    }


    private void Panel_MouseEnter(object sender, MouseEventArgs e) => _lastInsideAt = DateTime.UtcNow;

    private void Panel_MouseLeave(object sender, MouseEventArgs e) => _lastInsideAt = DateTime.UtcNow;

    private nint WindowProcedure(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            TogglePanel();
            handled = true;
        }
        else if (_appBar is not null &&
                 message == _appBar.CallbackMessage &&
                 _settings.DisplayMode is DisplayMode.Fixed &&
                 DateTime.UtcNow - _lastAppBarNotification > TimeSpan.FromMilliseconds(500))
        {
            _lastAppBarNotification = DateTime.UtcNow;
            Dispatcher.BeginInvoke(() => ApplySettingsLayout(initial: false), DispatcherPriority.Background);
            handled = true;
        }

        return 0;
    }

    private NotifyIcon CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / Hide", null, (_, _) => Dispatcher.Invoke(TogglePanel));
        menu.Items.Add("Settings…", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Clear pinned windows", null, (_, _) => Dispatcher.Invoke(ClearPinnedWindows));
        var hiddenApps = new ToolStripMenuItem("Hidden apps");
        hiddenApps.DropDownOpening += (_, _) => PopulateHiddenApplicationsMenu(hiddenApps);
        menu.Items.Add(hiddenApps);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var icon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "StageBar",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(TogglePanel);
        return icon;
    }

    private void PopulateHiddenApplicationsMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        if (_settings.HiddenApplications.Count == 0)
        {
            menu.DropDownItems.Add(new ToolStripMenuItem("No hidden apps") { Enabled = false });
            return;
        }

        foreach (var application in _settings.HiddenApplications)
        {
            var item = new ToolStripMenuItem(application);
            item.Click += (_, _) => Dispatcher.Invoke(() => ShowApplicationInStageBar(application));
            menu.DropDownItems.Add(item);
        }

        menu.DropDownItems.Add(new ToolStripSeparator());
        menu.DropDownItems.Add("Show all", null, (_, _) => Dispatcher.Invoke(ShowAllApplicationsInStageBar));
    }

    private static DrawingIcon LoadApplicationIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } executablePath &&
                DrawingIcon.ExtractAssociatedIcon(executablePath) is { } icon)
            {
                return icon;
            }
        }
        catch (ArgumentException)
        {
            // Fall back to the system icon when the executable icon cannot be read.
        }

        return (DrawingIcon)SystemIcons.Application.Clone();
    }

    internal void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow(_settings);
        _settingsWindow = window;
        window.SettingsChanged += ApplySettingsFromWindow;
        window.Closed += (_, _) => _settingsWindow = null;
        window.Show();
        window.Activate();
    }

    private void ApplySettingsFromWindow(StageManagerSettings settings)
    {
        var normalized = SettingsService.Normalize(settings with
        {
            PinnedWindows = _settings.PinnedWindows,
            PinnedApplications = _settings.PinnedApplications,
            HiddenApplications = _settings.HiddenApplications
        });
        if (SettingsService.AreEquivalent(normalized, _settings))
        {
            return;
        }

        var layoutChanged = normalized.DisplayMode != _settings.DisplayMode ||
                            !normalized.DisplayWidth.Equals(_settings.DisplayWidth) ||
                            normalized.CardCount != _settings.CardCount;
        _settings = normalized;
        SaveSettings();

        if (layoutChanged)
        {
            ApplySettingsLayout(initial: false);
            return;
        }

        var canPin = _settings.PinnedWindows.Count < StageManagerSettings.MaximumPinnedWindows;
        foreach (var control in PreviewList.Children.OfType<WindowPreviewControl>())
        {
            control.UpdateState(
                control.Snapshot,
                _settings.PreviewOpacity,
                WindowDisplayOrder.IsPinned(control.Snapshot, _settings.PinnedWindows),
                canPin);
        }

        UpdateVisualLayers();
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the valid in-memory settings when persistence is temporarily unavailable.
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            HidePanel();
            return;
        }

        _windowEventDebounceTimer.Stop();
        _reconciliationTimer.Stop();
        _edgeTimer.Stop();
        _windowEventMonitor?.Dispose();
        _windowEventMonitor = null;
        DisposeThumbnails();
        _appBar?.Dispose();
        if (_windowHandle != 0)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HotKeyId);
        }

        _source?.RemoveHook(WindowProcedure);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _applicationIcon.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }

    private void DisposeThumbnails()
    {
        foreach (var thumbnail in _thumbnails.Values)
        {
            thumbnail.Dispose();
        }

        _thumbnails.Clear();
    }
}
