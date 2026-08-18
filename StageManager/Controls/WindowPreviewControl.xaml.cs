using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using StageManager.Models;
using StageManager.Services;

namespace StageManager.Controls;

public partial class WindowPreviewControl : System.Windows.Controls.UserControl
{
    private readonly System.Windows.Controls.MenuItem _pinMenuItem = new();
    private readonly System.Windows.Controls.MenuItem _hideMenuItem = new System.Windows.Controls.MenuItem { Header = "Don't show in StageBar" };
    private readonly System.Windows.Controls.MenuItem _closeMenuItem = new System.Windows.Controls.MenuItem { Header = "Close window" };

    internal WindowPreviewControl(
        WindowSnapshot snapshot,
        ImageSource? icon,
        CardLayout layout,
        double previewOpacity,
        bool isPinned,
        bool canPin)
    {
        InitializeComponent();
        Snapshot = snapshot;
        ItemExtent = layout.ItemExtent;
        Width = layout.PreviewWidth + 20;
        Height = layout.ItemExtent;
        PreviewVisual.Width = layout.PreviewWidth;
        PreviewVisual.Height = layout.PreviewHeight;
        PreviewSurface.Width = layout.PreviewWidth;
        PreviewSurface.Height = layout.PreviewHeight;
        AppIcon = icon;
        AppIconAnchor.Visibility = icon is null ? Visibility.Collapsed : Visibility.Visible;

        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(_pinMenuItem);
        menu.Items.Add(_hideMenuItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(_closeMenuItem);
        ContextMenu = menu;
        _pinMenuItem.Click += (_, _) => PinToggled?.Invoke(this, Snapshot);
        _hideMenuItem.Click += (_, _) => ApplicationHidden?.Invoke(this, Snapshot);
        _closeMenuItem.Click += (_, _) => WindowCloseRequested?.Invoke(this, Snapshot);
        ToolTip = "Click: activate • Ctrl+click: close • Right-click: more options";
        UpdateState(snapshot, previewOpacity, isPinned, canPin);
    }

    public WindowSnapshot Snapshot { get; private set; }

    public double ItemExtent { get; }

    public ImageSource? AppIcon { get; }

    public double PreviewOpacity { get; private set; }

    public bool IsPinned { get; private set; }

    public FrameworkElement IconAnchor => AppIconAnchor;

    public FrameworkElement ThumbnailSurface => PreviewSurface;

    internal void UpdateState(
        WindowSnapshot snapshot,
        double previewOpacity,
        bool isPinned,
        bool canPin)
    {
        Snapshot = snapshot;
        PreviewOpacity = Math.Clamp(previewOpacity, 0.25, 1);
        IsPinned = isPinned;
        AutomationProperties.SetName(this, $"Window preview: {snapshot.Title}{(isPinned ? " (pinned)" : string.Empty)}");
        AutomationProperties.SetHelpText(this, snapshot.ProcessName);
        _pinMenuItem.Header = isPinned ? "Unpin window" : "Pin window";
        _pinMenuItem.IsEnabled = isPinned || canPin;
        _pinMenuItem.ToolTip = !isPinned && !canPin ? "A maximum of four windows can be pinned" : null;
    }

    public void SetCarouselPosition(double distanceFromCenter, double maximumVisibleDistance)
    {
        var absoluteDistance = Math.Abs(distanceFromCenter);
        var isVisible = absoluteDistance <= maximumVisibleDistance + 0.58;

        CardScale.ScaleX = 1;
        CardScale.ScaleY = 1;
        Opacity = isVisible ? 1 : 0;
        IsHitTestVisible = isVisible;
        System.Windows.Controls.Panel.SetZIndex(this, 100 - (int)Math.Round(absoluteDistance * 10));
    }

    internal void DisposeInteractionState()
    {
        ContextMenu = null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var action = WindowPreviewGesture.Resolve(
            e.ChangedButton is MouseButton.Left,
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        e.Handled = WindowPreviewGesture.Execute(
            action,
            () => WindowSelected?.Invoke(this, Snapshot),
            () => WindowCloseRequested?.Invoke(this, Snapshot),
            () => PinToggled?.Invoke(this, Snapshot));
    }

    public event EventHandler<WindowSnapshot>? WindowSelected;

    public event EventHandler<WindowSnapshot>? PinToggled;

    public event EventHandler<WindowSnapshot>? ApplicationHidden;

    public event EventHandler<WindowSnapshot>? WindowCloseRequested;
}
