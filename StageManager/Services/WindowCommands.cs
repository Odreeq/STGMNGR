using StageManager.Interop;

namespace StageManager.Services;

internal sealed class WindowCommands
{
    private readonly Func<nint, bool> _isWindow;
    private readonly Func<nint, uint, nint, nint, bool> _postMessage;

    internal WindowCommands()
        : this(NativeMethods.IsWindow, NativeMethods.PostMessage)
    {
    }

    internal WindowCommands(
        Func<nint, bool> isWindow,
        Func<nint, uint, nint, nint, bool> postMessage)
    {
        _isWindow = isWindow;
        _postMessage = postMessage;
    }

    internal bool RequestClose(nint window) =>
        window != 0 &&
        _isWindow(window) &&
        _postMessage(window, NativeMethods.WmClose, 0, 0);
}
