using System.Runtime.InteropServices;
using System.Text;

namespace StageManager.Interop;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int WmNcHitTest = 0x0084;
    internal const int HtClient = 1;
    internal const int HtTransparent = -1;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExTopmost = 0x00000008L;
    internal const long WsExTransparent = 0x00000020L;
    internal const long WsExAppWindow = 0x00040000L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const uint GwOwner = 4;
    internal const uint GaRoot = 2;
    internal const int SwRestore = 9;
    internal const int SwShowNoActivate = 4;
    internal const int WmHotKey = 0x0312;
    internal const uint WmClose = 0x0010;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint VkSpace = 0x20;
    internal const int DwmwaCloaked = 14;
    internal const int DwmwaWindowCornerPreference = 33;
    internal const int DwmwcpRound = 2;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventSystemMinimizeStart = 0x0016;
    internal const uint EventSystemMinimizeEnd = 0x0017;
    internal const uint EventObjectCreate = 0x8000;
    internal const uint EventObjectDestroy = 0x8001;
    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;
    internal const uint EventObjectLocationChange = 0x800B;
    internal const uint EventObjectNameChange = 0x800C;
    internal const uint EventObjectCloaked = 0x8017;
    internal const uint EventObjectUncloaked = 0x8018;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WinEventSkipOwnProcess = 0x0002;

    [Flags]
    internal enum DwmThumbnailFlags : uint
    {
        RectDestination = 0x00000001,
        RectSource = 0x00000002,
        Opacity = 0x00000004,
        Visible = 0x00000008,
        SourceClientAreaOnly = 0x00000010
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        internal readonly int Width => Right - Left;
        internal readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        internal int Width;
        internal int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DwmThumbnailProperties
    {
        internal DwmThumbnailFlags Flags;
        internal Rect Destination;
        internal Rect Source;
        internal byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] internal bool Visible;
        [MarshalAs(UnmanagedType.Bool)] internal bool SourceClientAreaOnly;
    }

    internal delegate bool EnumWindowsProc(nint window, nint parameter);

    internal delegate void WinEventDelegate(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint eventHookModule,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(nint window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    internal static extern int GetWindowTextLength(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern nint GetWindow(nint window, uint command);

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    internal static extern nint SetWindowLongPtr(nint window, int index, nint newValue);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindowAsync(nint window, int command);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUnregisterThumbnail(nint thumbnail);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out Size size);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmUpdateThumbnailProperties(
        nint thumbnail,
        ref DwmThumbnailProperties properties);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(
        nint window,
        int attribute,
        out int value,
        int valueSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
