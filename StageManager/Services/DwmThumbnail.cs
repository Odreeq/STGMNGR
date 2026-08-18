using StageManager.Interop;

namespace StageManager.Services;

internal sealed class DwmThumbnail : IDisposable
{
    internal delegate int UpdateProperties(nint thumbnail, ref NativeMethods.DwmThumbnailProperties properties);

    private nint _handle;
    private readonly UpdateProperties _updateProperties;
    private readonly Func<nint, int> _unregister;

    internal int LastError { get; private set; }

    private DwmThumbnail(nint handle)
        : this(handle, NativeMethods.DwmUpdateThumbnailProperties, NativeMethods.DwmUnregisterThumbnail)
    {
    }

    internal DwmThumbnail(nint handle, UpdateProperties updateProperties, Func<nint, int> unregister)
    {
        _handle = handle;
        _updateProperties = updateProperties;
        _unregister = unregister;
    }

    public static DwmThumbnail? TryCreate(nint destination, nint source, out int error)
    {
        error = NativeMethods.DwmRegisterThumbnail(destination, source, out var thumbnail);
        return error == 0 && thumbnail != 0 ? new DwmThumbnail(thumbnail) : null;
    }

    public void Update(NativeMethods.Rect availableBounds, NativeMethods.Rect clipBounds, byte opacity)
    {
        if (_handle == 0 || availableBounds.Width <= 0 || availableBounds.Height <= 0 || opacity == 0)
        {
            SetVisible(false);
            return;
        }

        LastError = NativeMethods.DwmQueryThumbnailSourceSize(_handle, out var sourceSize);
        if (LastError != 0 || sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var scale = Math.Min(
            (double)availableBounds.Width / sourceSize.Width,
            (double)availableBounds.Height / sourceSize.Height);
        var width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
        var height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
        var left = availableBounds.Left + (availableBounds.Width - width) / 2;
        var top = availableBounds.Top + (availableBounds.Height - height) / 2;

        var fullDestination = new NativeMethods.Rect
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height
        };
        var clippedDestination = Intersect(fullDestination, clipBounds);
        if (clippedDestination.Width <= 0 || clippedDestination.Height <= 0)
        {
            SetVisible(false);
            return;
        }

        var source = new NativeMethods.Rect
        {
            Left = Math.Clamp((int)Math.Round((clippedDestination.Left - fullDestination.Left) / scale), 0, sourceSize.Width),
            Top = Math.Clamp((int)Math.Round((clippedDestination.Top - fullDestination.Top) / scale), 0, sourceSize.Height),
            Right = Math.Clamp((int)Math.Round((clippedDestination.Right - fullDestination.Left) / scale), 0, sourceSize.Width),
            Bottom = Math.Clamp((int)Math.Round((clippedDestination.Bottom - fullDestination.Top) / scale), 0, sourceSize.Height)
        };

        var properties = new NativeMethods.DwmThumbnailProperties
        {
            Flags = NativeMethods.DwmThumbnailFlags.RectDestination |
                    NativeMethods.DwmThumbnailFlags.RectSource |
                    NativeMethods.DwmThumbnailFlags.Opacity |
                    NativeMethods.DwmThumbnailFlags.Visible |
                    NativeMethods.DwmThumbnailFlags.SourceClientAreaOnly,
            Destination = clippedDestination,
            Source = source,
            Opacity = opacity,
            Visible = true,
            SourceClientAreaOnly = false
        };

        LastError = _updateProperties(_handle, ref properties);
    }

    public int SetVisible(bool visible)
    {
        if (_handle == 0)
        {
            return 0;
        }

        var properties = new NativeMethods.DwmThumbnailProperties
        {
            Flags = NativeMethods.DwmThumbnailFlags.Visible,
            Visible = visible
        };
        LastError = _updateProperties(_handle, ref properties);
        return LastError;
    }

    private static NativeMethods.Rect Intersect(NativeMethods.Rect first, NativeMethods.Rect second) => new()
    {
        Left = Math.Max(first.Left, second.Left),
        Top = Math.Max(first.Top, second.Top),
        Right = Math.Min(first.Right, second.Right),
        Bottom = Math.Min(first.Bottom, second.Bottom)
    };

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
        {
            _unregister(handle);
        }

        GC.SuppressFinalize(this);
    }
}
