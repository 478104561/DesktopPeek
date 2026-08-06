namespace DesktopPeek.Services;

internal sealed class MouseMonitorService : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly TransparentModeService _transparent;
    private readonly Action _onTaskbarRestore;
    private bool _enabled = true;
    private int _hoverDelayMs = 500;
    private DateTime? _desktopSince;

    public MouseMonitorService(TransparentModeService transparent, Action onTaskbarRestore, int hoverDelayMs = 500)
    {
        _transparent = transparent;
        _onTaskbarRestore = onTaskbarRestore;
        _hoverDelayMs = Math.Clamp(hoverDelayMs, 0, 3000);
        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += OnTick;
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public int HoverDelayMs
    {
        get => _hoverDelayMs;
        set
        {
            _hoverDelayMs = Math.Clamp(value, 0, 3000);
            // Changing delay mid-hover: keep current start so remaining wait uses new value
        }
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_enabled)
            return;

        try
        {
            var surface = WindowEnumerator.HitTestCursor();

            if (surface == CursorSurface.Taskbar)
            {
                _desktopSince = null;
                if (_transparent.IsActive)
                    _onTaskbarRestore();
                return;
            }

            if (surface == CursorSurface.Desktop)
            {
                if (_transparent.IsActive)
                {
                    _desktopSince = null;
                    return;
                }

                _desktopSince ??= DateTime.UtcNow;
                var elapsed = (DateTime.UtcNow - _desktopSince.Value).TotalMilliseconds;
                if (elapsed >= _hoverDelayMs)
                {
                    _transparent.Enter();
                    _desktopSince = null;
                }
            }
            else
            {
                _desktopSince = null;
            }
        }
        catch
        {
            // keep timer alive
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
