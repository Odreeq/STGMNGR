namespace StageManager.Models;

public sealed record WindowSnapshot(
    nint Handle,
    string Title,
    string ProcessName,
    bool IsMinimized,
    bool IsForeground)
{
    public uint ProcessId { get; init; }

    public long ProcessStartedAtUtcTicks { get; init; }

    /// <summary>
    /// Small always-on-top helper windows (such as Chrome Picture-in-Picture)
    /// should not replace the application's regular window in a pinned slot.
    /// </summary>
    public bool IsAlwaysOnTop { get; init; }

    public long WindowArea { get; init; }
}
