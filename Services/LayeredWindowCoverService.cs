using DesktopPeek.Native;

namespace DesktopPeek.Services;

/// <summary>
/// Parks Snipaste / PureRef / UU Remote / desktop-pet layered windows off the virtual desktop
/// during peek so the real desktop (and icons) remain visible and clickable.
/// Does not use wallpaper covers or SetLayeredWindowAttributes.
/// </summary>
internal sealed class LayeredWindowCoverService : IDisposable
{
    private readonly List<ParkedWindow> _parked = [];
    private bool _disposed;

    public int ActiveCount => _parked.Count;

    public void ShowCovers(IReadOnlyList<LayeredCoverTarget> targets)
    {
        Clear();
        if (targets.Count == 0)
            return;

        var vs = SystemInformation.VirtualScreen;
        var offset = Math.Max(10_000, vs.Width + 2_000);

        foreach (var t in targets)
        {
            try
            {
                if (!NativeMethods.IsWindow(t.Handle) || !NativeMethods.IsWindowVisible(t.Handle))
                    continue;

                if (!NativeMethods.GetWindowRect(t.Handle, out var rect))
                    continue;

                var left = rect.Left;
                var top = rect.Top;
                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (w <= 1 || h <= 1)
                    continue;

                NativeMethods.SetWindowPos(
                    t.Handle,
                    IntPtr.Zero,
                    left + offset,
                    top,
                    0, 0,
                    NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER |
                    NativeConstants.SWP_NOACTIVATE);

                _parked.Add(new ParkedWindow(t.Handle, t.ProcessName, left, top));
            }
            catch
            {
                // skip inaccessible targets
            }
        }
    }

    public void Clear()
    {
        foreach (var entry in _parked)
        {
            try
            {
                if (!NativeMethods.IsWindow(entry.Handle))
                    continue;

                NativeMethods.SetWindowPos(
                    entry.Handle,
                    IntPtr.Zero,
                    entry.OriginalLeft,
                    entry.OriginalTop,
                    0, 0,
                    NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER |
                    NativeConstants.SWP_NOACTIVATE);

                NativeMethods.RedrawWindow(
                    entry.Handle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    NativeConstants.RdwInvalidate | NativeConstants.RdwAllChildren | NativeConstants.RdwUpdateNow);
            }
            catch
            {
                // ignore
            }
        }

        _parked.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
    }

    private sealed class ParkedWindow
    {
        public IntPtr Handle { get; }
        public string ProcessName { get; }
        public int OriginalLeft { get; }
        public int OriginalTop { get; }

        public ParkedWindow(IntPtr handle, string processName, int originalLeft, int originalTop)
        {
            Handle = handle;
            ProcessName = processName;
            OriginalLeft = originalLeft;
            OriginalTop = originalTop;
        }
    }
}

internal readonly record struct LayeredCoverTarget(
    IntPtr Handle,
    string ProcessName,
    int Left,
    int Top,
    int Width,
    int Height);
