using StageManager.Models;

namespace StageManager.Services;

internal static class WindowInstanceIdentity
{
    internal static bool Matches(WindowSnapshot first, WindowSnapshot second) =>
        first.Handle == second.Handle &&
        first.ProcessId == second.ProcessId &&
        first.ProcessStartedAtUtcTicks == second.ProcessStartedAtUtcTicks;
}
