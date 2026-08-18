using StageManager.Models;

namespace StageManager.Services;

internal static class WindowListState
{
    internal static bool Matches(
        IReadOnlyList<WindowSnapshot> expected,
        IReadOnlyList<WindowSnapshot> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (expected[index] != actual[index])
            {
                return false;
            }
        }

        return true;
    }
}
