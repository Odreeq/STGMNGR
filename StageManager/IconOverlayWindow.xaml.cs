using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using StageManager.Interop;
using StageManager.Services;
using WpfColor = System.Windows.Media.Color;
using WpfImage = System.Windows.Controls.Image;

namespace StageManager;

public partial class IconOverlayWindow : Window
{
    private const uint BadgeBackgroundArgb = 0xD9202935;
    private const uint BadgeBorderArgb = 0xFF3C4A5A;
    private static readonly SolidColorBrush BadgeBackground = CreateBrush(BadgeBackgroundArgb);
    private static readonly SolidColorBrush BadgeBorder = CreateBrush(BadgeBorderArgb);

    private readonly IconBadgeRegistry<Border> _badges = new();

    internal IconOverlayWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        SourceInitialized += IconOverlayWindow_SourceInitialized;
    }

    internal void SyncToOwner(Window owner)
    {
        if (!double.IsFinite(owner.Left) ||
            !double.IsFinite(owner.Top) ||
            owner.ActualWidth <= 0 ||
            owner.ActualHeight <= 0)
        {
            return;
        }

        Left = owner.Left;
        Top = owner.Top;
        Width = owner.ActualWidth;
        Height = owner.ActualHeight;
        Opacity = owner.Opacity;
    }

    internal void UpdateBadges(IReadOnlyCollection<IconOverlayItem> items)
    {
        var visibleHandles = items.Select(static item => item.Handle).ToHashSet();
        _badges.RemoveObsolete(visibleHandles, ReleaseBadge);

        foreach (var item in items)
        {
            if (!_badges.TryGetValue(item.Handle, out var badge))
            {
                badge = CreateBadge(item.Icon);
                _badges.Add(item.Handle, badge);
                IconCanvas.Children.Add(badge);
            }

            var image = GetBadgeImage(badge);
            if (image is not null && !ReferenceEquals(image.Source, item.Icon))
            {
                image.Source = item.Icon;
            }

            badge.Width = item.Bounds.Width;
            badge.Height = item.Bounds.Height;
            badge.Opacity = item.Opacity;
            badge.Visibility = Visibility.Visible;
            Canvas.SetLeft(badge, item.Bounds.Left);
            Canvas.SetTop(badge, item.Bounds.Top);
        }
    }

    internal void ClearBadges()
    {
        _badges.Clear(ReleaseBadge);
        IconCanvas.Children.Clear();
    }

    private void ReleaseBadge(Border badge)
    {
        if (GetBadgeImage(badge) is { } image)
        {
            image.Source = null;
        }

        IconCanvas.Children.Remove(badge);
    }

    private void IconOverlayWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExToolWindow |
                 NativeMethods.WsExNoActivate |
                 NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)style);
    }

    private static Border CreateBadge(ImageSource icon) =>
        new()
        {
            Padding = new Thickness(2),
            Background = BadgeBackground,
            BorderBrush = BadgeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
            Child = new WpfImage
            {
                Source = icon,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false
            }
        };

    private static WpfImage? GetBadgeImage(Border badge) => badge.Child as WpfImage;

    private static SolidColorBrush CreateBrush(uint argb)
    {
        var brush = new SolidColorBrush(WpfColor.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
        brush.Freeze();
        return brush;
    }
}
