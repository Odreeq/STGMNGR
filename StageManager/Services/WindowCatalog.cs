using System.Diagnostics;
using System.Text;
using StageManager.Interop;
using StageManager.Models;

namespace StageManager.Services;

public sealed class WindowCatalog
{
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow"
    };

    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    public IReadOnlyList<WindowSnapshot> GetWindows()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var windows = new List<WindowSnapshot>();
        var processMetadata = new Dictionary<uint, (string Name, long StartedAtUtcTicks)>();

        NativeMethods.EnumWindows((window, _) =>
        {
            if (TryCreateSnapshot(window, foreground, processMetadata) is { } snapshot)
            {
                windows.Add(snapshot);
            }

            return true;
        }, 0);


        // EnumWindows returns top-level windows in Z order. Preserving that order
        // gives StageBar an MRU-style list without maintaining a separate polling history.
        return windows;
    }

    public static void Activate(nint window)
    {
        if (!NativeMethods.IsWindow(window))
        {
            return;
        }

        if (NativeMethods.IsIconic(window))
        {
            NativeMethods.ShowWindowAsync(window, NativeMethods.SwRestore);
        }

        NativeMethods.SetForegroundWindow(window);
    }

    private WindowSnapshot? TryCreateSnapshot(
        nint window,
        nint foreground,
        Dictionary<uint, (string Name, long StartedAtUtcTicks)> processMetadata)
    {
        if (!NativeMethods.IsWindowVisible(window) || NativeMethods.GetAncestor(window, NativeMethods.GaRoot) != window)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0 || processId == _currentProcessId)
        {
            return null;
        }

        var extendedStyle = NativeMethods.GetWindowLongPtr(window, NativeMethods.GwlExStyle).ToInt64();
        var isToolWindow = (extendedStyle & NativeMethods.WsExToolWindow) != 0;
        var isAppWindow = (extendedStyle & NativeMethods.WsExAppWindow) != 0;
        // Some desktop apps expose their main surface as an owned window. Only
        // discard genuine tool windows; WS_EX_APPWINDOW explicitly opts in.
        if (isToolWindow && !isAppWindow)
        {
            return null;
        }

        if (NativeMethods.DwmGetWindowAttribute(window, NativeMethods.DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return null;
        }

        if (!NativeMethods.GetWindowRect(window, out var rect) || rect.Width < 40 || rect.Height < 30)
        {
            return null;
        }

        var className = GetClassName(window);
        if (ExcludedClasses.Contains(className))
        {
            return null;
        }

        var title = GetWindowTitle(window);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var process = GetProcessMetadata(processId, processMetadata);
        return new WindowSnapshot(
            window,
            title.Trim(),
            process.Name,
            NativeMethods.IsIconic(window),
            window == foreground)
        {
            ProcessId = processId,
            ProcessStartedAtUtcTicks = process.StartedAtUtcTicks,
            IsAlwaysOnTop = (extendedStyle & NativeMethods.WsExTopmost) != 0,
            WindowArea = (long)rect.Width * rect.Height
        };
    }

    private static string GetWindowTitle(nint window)
    {
        var length = NativeMethods.GetWindowTextLength(window);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string GetClassName(nint window)
    {
        var buffer = new StringBuilder(256);
        NativeMethods.GetClassName(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static (string Name, long StartedAtUtcTicks) GetProcessMetadata(
        uint processId,
        Dictionary<uint, (string Name, long StartedAtUtcTicks)> metadata)
    {
        if (metadata.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var value = (process.ProcessName, process.StartTime.ToUniversalTime().Ticks);
            metadata.Add(processId, value);
            return value;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            var value = ("Application", 0L);
            metadata.TryAdd(processId, value);
            return value;
        }
    }
}
