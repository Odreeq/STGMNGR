namespace StageManager.Services;

internal sealed record WindowEventBatch(
    DateTimeOffset FirstObservedAt,
    IReadOnlyList<nint> InvalidatedHandles,
    bool RefreshThumbnailGeometry);

internal sealed class WindowEventBatcher
{
    private readonly object _sync = new();
    private readonly HashSet<nint> _invalidatedHandles = [];
    private DateTimeOffset? _firstObservedAt;
    private bool _refreshThumbnailGeometry;
    private bool _dispatchPending;

    internal bool Record(uint eventType, nint window, DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            _firstObservedAt ??= observedAt;
            if (eventType == Interop.NativeMethods.EventObjectDestroy)
            {
                _invalidatedHandles.Add(window);
            }

            if (eventType == Interop.NativeMethods.EventObjectLocationChange)
            {
                _refreshThumbnailGeometry = true;
            }

            if (_dispatchPending)
            {
                return false;
            }

            _dispatchPending = true;
            return true;
        }
    }

    internal WindowEventBatch Drain()
    {
        if (!TryDrain(out var batch))
        {
            throw new InvalidOperationException("No native window-event batch is pending.");
        }

        return batch;
    }

    internal bool TryDrain(out WindowEventBatch batch)
    {
        lock (_sync)
        {
            if (!_dispatchPending || _firstObservedAt is not { } firstObservedAt)
            {
                batch = null!;
                return false;
            }

            batch = new WindowEventBatch(
                firstObservedAt,
                _invalidatedHandles.ToArray(),
                _refreshThumbnailGeometry);
            _firstObservedAt = null;
            _invalidatedHandles.Clear();
            _refreshThumbnailGeometry = false;
            _dispatchPending = false;
            return true;
        }
    }

    internal void Clear()
    {
        lock (_sync)
        {
            _firstObservedAt = null;
            _invalidatedHandles.Clear();
            _refreshThumbnailGeometry = false;
            _dispatchPending = false;
        }
    }
}
