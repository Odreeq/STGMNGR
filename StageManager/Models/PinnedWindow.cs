namespace StageManager.Models;

/// <summary>
/// A pin belongs to one running top-level window, rather than every window
/// created by the same application process.
/// </summary>
public sealed record PinnedWindow
{
    public long Handle { get; init; }

    public uint ProcessId { get; init; }

    public long ProcessStartedAtUtcTicks { get; init; }

    internal static PinnedWindow From(WindowSnapshot window) => new()
    {
        Handle = window.Handle.ToInt64(),
        ProcessId = window.ProcessId,
        ProcessStartedAtUtcTicks = window.ProcessStartedAtUtcTicks
    };

    internal bool Matches(WindowSnapshot window) =>
        Handle == window.Handle.ToInt64() &&
        ProcessId == window.ProcessId &&
        ProcessStartedAtUtcTicks == window.ProcessStartedAtUtcTicks;
}
