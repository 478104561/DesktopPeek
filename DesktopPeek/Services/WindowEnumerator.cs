using System.Diagnostics;
using DesktopPeek.Native;

namespace DesktopPeek.Services;

internal static class WindowEnumerator
{
    private static readonly HashSet<string> ExcludedClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Windows.UI.Core.CoreWindow",
        "ForegroundStaging",
        "Shell_Flyout",
        "XamlExplorerHostIslandWindow",
        "Windows.Internal.Shell.TabProxyWindow",
        "DummyDWMListenerWindow",
        "EdgeUiInputTopWndClass",
        "ImmersiveLauncher",
        "SearchPane",
        "Windows.UI.Input.InputSite.WindowClass",
    };

    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm",
        "csrss",
        "winlogon",
        "services",
        "lsass",
        "smss",
        "fontdrvhost",
        "sihost",
        "taskhostw",
        "RuntimeBroker",
        "SearchHost",
        "SearchApp",
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "TextInputHost",
        "LockApp",
        "SystemSettings",
    };

    public static List<IntPtr> GetTargetWindows(IntPtr selfHwnd)
    {
        var result = new List<IntPtr>();
        var selfPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!ShouldProcess(hWnd, selfHwnd, selfPid))
                    return true;

                result.Add(hWnd);
            }
            catch
            {
                // skip inaccessible windows
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    public static bool ShouldProcess(IntPtr hWnd, IntPtr selfHwnd, uint selfPid)
    {
        if (hWnd == IntPtr.Zero || hWnd == selfHwnd)
            return false;

        if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Owned popups without app window style are often tooltips/menus
        var owner = NativeMethods.GetWindow(hWnd, NativeConstants.GW_OWNER);
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_EXSTYLE);
        if (owner != IntPtr.Zero && (exStyle & NativeConstants.WS_EX_TOOLWINDOW) != 0
            && (exStyle & NativeConstants.WS_EX_APPWINDOW) == 0)
            return false;

        var className = NativeMethods.GetClassName(hWnd);
        if (ExcludedClassNames.Contains(className))
            return false;

        // Cloaked UWP / virtual desktop hidden windows
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeConstants.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
            return false;

        if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            return false;

        // Zero-size or off-screen-ish tiny windows
        if (rect.Right - rect.Left <= 1 || rect.Bottom - rect.Top <= 1)
            return false;

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0 || pid == selfPid)
            return false;

        try
        {
            using var proc = Process.GetProcessById((int)pid);
            var name = proc.ProcessName;
            if (ExcludedProcessNames.Contains(name))
                return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static CursorSurface HitTestCursor()
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return CursorSurface.Unknown;

        var hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
            return CursorSurface.Desktop;

        var root = NativeMethods.GetAncestor(hwnd, NativeConstants.GA_ROOT);
        if (root == IntPtr.Zero)
            root = hwnd;

        var className = NativeMethods.GetClassName(root);

        if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return CursorSurface.Taskbar;

        if (className is "Progman" or "WorkerW")
            return CursorSurface.Desktop;

        // DefView child under WorkerW/Progman
        var leafClass = NativeMethods.GetClassName(hwnd);
        if (leafClass is "SysListView32" or "SHELLDLL_DefView")
        {
            var parentClass = NativeMethods.GetClassName(NativeMethods.GetAncestor(hwnd, NativeConstants.GA_ROOT));
            if (parentClass is "Progman" or "WorkerW" || className is "Progman" or "WorkerW")
                return CursorSurface.Desktop;
        }

        // Notify area / start button area sometimes reports differently
        if (className.Contains("Tray", StringComparison.OrdinalIgnoreCase)
            || className.Contains("TaskList", StringComparison.OrdinalIgnoreCase))
            return CursorSurface.Taskbar;

        return CursorSurface.Window;
    }
}

internal enum CursorSurface
{
    Unknown,
    Desktop,
    Taskbar,
    Window
}
