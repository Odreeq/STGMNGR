using System.Windows.Threading;
using StageManager.Interop;

namespace StageManager.Services;

internal sealed class WindowEventMonitor : IDisposable
{
    private static readonly (uint Minimum, uint Maximum)[] EventRanges =
    [
        (NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground),
        (NativeMethods.EventSystemMinimizeStart, NativeMethods.EventSystemMinimizeEnd),
        (NativeMethods.EventObjectCreate, NativeMethods.EventObjectHide),
        (NativeMethods.EventObjectLocationChange, NativeMethods.EventObjectLocationChange),
        (NativeMethods.EventObjectNameChange, NativeMethods.EventObjectNameChange),
        (NativeMethods.EventObjectCloaked, NativeMethods.EventObjectUncloaked)
    ];

    internal static IReadOnlyList<(uint Minimum, uint Maximum)> RegisteredEventRanges => EventRanges;

    private readonly Dispatcher _dispatcher;
    private readonly Action<WindowEventBatch> _catalogChanged;
    private readonly Func<nint, bool> _unhook;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly WindowEventBatcher _batcher = new();
    private readonly List<nint> _hooks = [];
    private readonly List<(uint Minimum, uint Maximum)> _failedEventRanges = [];
    private int _disposed;

    internal WindowEventMonitor(Dispatcher dispatcher, Action<WindowEventBatch> catalogChanged)
        : this(
            dispatcher,
            catalogChanged,
            static (minimum, maximum, callback) => NativeMethods.SetWinEventHook(
                minimum,
                maximum,
                0,
                callback,
                0,
                0,
                NativeMethods.WinEventOutOfContext | NativeMethods.WinEventSkipOwnProcess),
            NativeMethods.UnhookWinEvent)
    {
    }

    internal WindowEventMonitor(
        Dispatcher dispatcher,
        Action<WindowEventBatch> catalogChanged,
        Func<uint, uint, NativeMethods.WinEventDelegate, nint> register,
        Func<nint, bool> unhook)
    {
        _dispatcher = dispatcher;
        _catalogChanged = catalogChanged;
        _unhook = unhook;
        _callback = OnWinEvent;

        foreach (var (minimum, maximum) in EventRanges)
        {
            var hook = register(minimum, maximum, _callback);
            if (hook != 0)
            {
                _hooks.Add(hook);
            }
            else
            {
                _failedEventRanges.Add((minimum, maximum));
            }
        }
    }

    internal IReadOnlyList<(uint Minimum, uint Maximum)> FailedEventRanges => _failedEventRanges;

    internal int ActiveHookCount => _hooks.Count;

    private void OnWinEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            window == 0 ||
            !WindowRefreshPolicy.IsRelevantEvent(eventType, objectId, childId))
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == _currentProcessId)
        {
            return;
        }

        if (eventType != NativeMethods.EventObjectDestroy &&
            NativeMethods.GetAncestor(window, NativeMethods.GaRoot) != window)
        {
            return;
        }

        if (!_batcher.Record(eventType, window, DateTimeOffset.UtcNow))
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(DrainPendingBatch, DispatcherPriority.Input);
        }
        catch (InvalidOperationException)
        {
            // The WPF dispatcher can begin shutting down before native hooks are removed.
        }
    }

    private void DrainPendingBatch()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (_batcher.TryDrain(out var batch) && Volatile.Read(ref _disposed) == 0)
        {
            _catalogChanged(batch);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        foreach (var hook in _hooks)
        {
            _unhook(hook);
        }

        _hooks.Clear();
        _batcher.Clear();
        GC.KeepAlive(_callback);
        GC.SuppressFinalize(this);
    }
}
