using System.Diagnostics;
using DesktopPeek.Native;
using System.Drawing;
using System.Windows.Forms;

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

    /// <summary>
    /// Only these layered apps get wallpaper covers. Fullscreen overlays (TabTip / NVIDIA)
    /// must not be covered — that blanketed the desktop.
    /// </summary>
    private static readonly HashSet<string> LayeredCoverProcessAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Snipaste",
        "PureRef",
    };

    private static readonly HashSet<string> LayeredCoverProcessBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "TabTip",
        "TabTip32",
        "NVIDIA Share",
        "NVIDIA Overlay",
        "nvcontainer",
        "TextInputHost",
        "ShellExperienceHost",
        "ApplicationFrameHost",
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

    /// <summary>
    /// Layered windows we must not mutate; cover overlays hide them visually instead.
    /// </summary>
    public static List<LayeredCoverTarget> GetLayeredCoverTargets(IntPtr selfHwnd)
    {
        var result = new List<LayeredCoverTarget>();
        var selfPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!TryGetLayeredCoverTarget(hWnd, selfHwnd, selfPid, out var target))
                    return true;
                result.Add(target);
            }
            catch
            {
                // skip inaccessible windows
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static bool TryGetLayeredCoverTarget(
        IntPtr hWnd, IntPtr selfHwnd, uint selfPid, out LayeredCoverTarget target)
    {
        target = default;
        if (hWnd == IntPtr.Zero || hWnd == selfHwnd)
            return false;
        if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd))
            return false;

        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_EXSTYLE);
        if ((exStyle & NativeConstants.WS_EX_LAYERED) == 0)
            return false;

        var owner = NativeMethods.GetWindow(hWnd, NativeConstants.GW_OWNER);
        if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            return false;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        // Skip tiny owned tooltips; keep Snipaste-sized paste boards.
        if (owner != IntPtr.Zero
            && (exStyle & NativeConstants.WS_EX_TOOLWINDOW) != 0
            && (exStyle & NativeConstants.WS_EX_APPWINDOW) == 0
            && (width < 48 || height < 48))
            return false;

        var className = NativeMethods.GetClassName(hWnd);
        if (ExcludedClassNames.Contains(className))
            return false;

        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeConstants.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
            return false;

        if (width <= 1 || height <= 1)
            return false;

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0 || pid == selfPid)
            return false;

        string procName;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            procName = proc.ProcessName;
            if (ExcludedProcessNames.Contains(procName))
                return false;
            if (LayeredCoverProcessBlocklist.Contains(procName))
                return false;
            if (!IsLayeredCoverProcess(procName))
                return false;
        }
        catch
        {
            return false;
        }

        // Ignore windows parked far off the virtual desktop (stale displace leftovers).
        var bounds = new Rectangle(rect.Left, rect.Top, width, height);
        if (!SystemInformation.VirtualScreen.IntersectsWith(bounds))
            return false;

        // Fullscreen layered hosts are almost never the apps we want to peek-cover.
        if (IsExactMonitorBounds(bounds) && !LayeredCoverProcessAllowlist.Contains(procName))
            return false;

        target = new LayeredCoverTarget(hWnd, procName, rect.Left, rect.Top, width, height);
        return true;
    }

    private static bool IsLayeredCoverProcess(string procName)
    {
        if (LayeredCoverProcessAllowlist.Contains(procName))
            return true;
        // Desktop pets (e.g. 呆啵宠物)
        if (procName.Contains("宠物", StringComparison.OrdinalIgnoreCase)
            || procName.Contains("pet", StringComparison.OrdinalIgnoreCase)
            || procName.Contains("呆啵", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsExactMonitorBounds(Rectangle bounds)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Bounds == bounds)
                return true;
        }

        return false;
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

        // Already-layered windows (Qt pets / Snipaste / PureRef / UpdateLayeredWindow)
        // cannot be safely peeked from an external process — SLWA, ShowWindow, and
        // SetWindowPos all break their composition/input after restore.
        if ((exStyle & NativeConstants.WS_EX_LAYERED) != 0)
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
