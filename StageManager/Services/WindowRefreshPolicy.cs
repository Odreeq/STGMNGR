namespace StageManager.Services;

internal sealed class WindowRefreshPolicy
{
    private static readonly HashSet<uint> RelevantEvents =
    [
        0x0003, // EVENT_SYSTEM_FOREGROUND
        0x0016, // EVENT_SYSTEM_MINIMIZESTART
        0x0017, // EVENT_SYSTEM_MINIMIZEEND
        0x8000, // EVENT_OBJECT_CREATE
        0x8001, // EVENT_OBJECT_DESTROY
        0x8002, // EVENT_OBJECT_SHOW
        0x8003, // EVENT_OBJECT_HIDE
        0x800B, // EVENT_OBJECT_LOCATIONCHANGE
        0x800C, // EVENT_OBJECT_NAMECHANGE
        0x8017, // EVENT_OBJECT_CLOAKED
        0x8018  // EVENT_OBJECT_UNCLOAKED
    ];

    private DateTimeOffset? _dueAt;

    internal WindowRefreshPolicy()
        : this(TimeSpan.FromMilliseconds(20))
    {
    }

    internal WindowRefreshPolicy(TimeSpan debounceInterval)
    {
        if (debounceInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceInterval));
        }

        DebounceInterval = debounceInterval;
    }

    internal TimeSpan DebounceInterval { get; }

    internal static TimeSpan ReconciliationInterval { get; } = TimeSpan.FromSeconds(5);

    internal void Signal(DateTimeOffset now)
    {
        _dueAt ??= now + DebounceInterval;
    }

    internal bool TryConsume(DateTimeOffset now)
    {
        if (_dueAt is null || now < _dueAt.Value)
        {
            return false;
        }

        _dueAt = null;
        return true;
    }

    internal TimeSpan GetRemainingDelay(DateTimeOffset now) => _dueAt is { } dueAt && dueAt > now
        ? dueAt - now
        : TimeSpan.Zero;

    internal static bool IsRelevantEvent(uint eventType, int objectId, int childId) =>
        objectId == 0 && childId == 0 && RelevantEvents.Contains(eventType);
}
