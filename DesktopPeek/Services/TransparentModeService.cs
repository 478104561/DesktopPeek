using DesktopPeek.Models;
using DesktopPeek.Native;

namespace DesktopPeek.Services;

internal enum FadeDirection
{
    None,
    ToPeek,
    ToRestore
}

internal sealed class TransparentModeService : IDisposable
{
    private const int FadeDurationMs = 250;
    private const int FadeIntervalMs = 16;

    private readonly object _sync = new();
    private readonly Dictionary<nint, WindowStateInfo> _saved = new();
    private readonly System.Windows.Forms.Timer _fadeTimer;

    private IntPtr _selfHwnd;
    private byte _opacity = 50;
    private bool _active;
    private FadeDirection _direction = FadeDirection.None;
    private DateTime _animStartUtc;
    private double _animStartT;

    public TransparentModeService()
    {
        _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeIntervalMs };
        _fadeTimer.Tick += OnFadeTick;
    }

    public bool IsActive
    {
        get { lock (_sync) return _active; }
    }

    public byte Opacity
    {
        get => _opacity;
        set
        {
            _opacity = AppConfig.ClampOpacity(value);
            lock (_sync)
            {
                if (!_active || _saved.Count == 0)
                    return;

                if (_direction == FadeDirection.ToRestore)
                    return;

                foreach (var hwnd in _saved.Keys.ToList())
                    SetAlphaOnly(hwnd, _opacity);

                if (_direction == FadeDirection.ToPeek)
                    StopAnimation();
            }
        }
    }

    public void SetSelfWindow(IntPtr hwnd) => _selfHwnd = hwnd;

    public void Enter()
    {
        lock (_sync)
        {
            if (_active && _direction == FadeDirection.ToRestore)
            {
                StartAnimation(FadeDirection.ToPeek, reverse: true);
                return;
            }

            if (_active)
                return;

            var windows = WindowEnumerator.GetTargetWindows(_selfHwnd);
            foreach (var hwnd in windows)
            {
                try
                {
                    if (!NativeMethods.IsWindow(hwnd))
                        continue;

                    if (_saved.ContainsKey(hwnd))
                        continue;

                    var exStyle = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE);
                    var hadLayered = (exStyle & NativeConstants.WS_EX_LAYERED) != 0;
                    var hadTransparent = (exStyle & NativeConstants.WS_EX_TRANSPARENT) != 0;

                    byte originalAlpha = 255;
                    bool hadAlpha = false;
                    if (hadLayered)
                    {
                        if (NativeMethods.GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out uint flags)
                            && (flags & NativeConstants.LWA_ALPHA) != 0)
                        {
                            originalAlpha = alpha;
                            hadAlpha = true;
                        }
                    }

                    _saved[hwnd] = new WindowStateInfo
                    {
                        Handle = hwnd,
                        OriginalExStyle = exStyle,
                        HadLayered = hadLayered,
                        HadTransparent = hadTransparent,
                        OriginalAlpha = originalAlpha,
                        HadAlpha = hadAlpha
                    };

                    ApplyPeekStyles(hwnd, originalAlpha);
                }
                catch
                {
                    // elevated / inaccessible window
                }
            }

            _active = true;
            StartAnimation(FadeDirection.ToPeek, reverse: false);
        }
    }

    public void Exit()
    {
        lock (_sync)
        {
            if (!_active && _saved.Count == 0)
                return;

            if (_direction == FadeDirection.ToRestore)
                return;

            if (_direction == FadeDirection.ToPeek)
            {
                StartAnimation(FadeDirection.ToRestore, reverse: true);
                return;
            }

            StartAnimation(FadeDirection.ToRestore, reverse: false);
        }
    }

    public void ExitImmediate()
    {
        lock (_sync)
        {
            StopAnimation();
            FinishRestoreLocked();
        }
    }

    public void Toggle()
    {
        if (IsActive) Exit();
        else Enter();
    }

    private void StartAnimation(FadeDirection direction, bool reverse)
    {
        if (reverse && _direction != FadeDirection.None)
        {
            var elapsed = (DateTime.UtcNow - _animStartUtc).TotalMilliseconds;
            var t = Math.Clamp(_animStartT + elapsed / FadeDurationMs, 0.0, 1.0);
            _animStartT = 1.0 - t;
        }
        else
        {
            _animStartT = 0;
        }

        _direction = direction;
        _animStartUtc = DateTime.UtcNow;
        if (!_fadeTimer.Enabled)
            _fadeTimer.Start();
    }

    private void StopAnimation()
    {
        _fadeTimer.Stop();
        _direction = FadeDirection.None;
        _animStartT = 0;
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (_direction == FadeDirection.None || _saved.Count == 0)
            {
                StopAnimation();
                return;
            }

            var elapsed = (DateTime.UtcNow - _animStartUtc).TotalMilliseconds;
            var t = Math.Clamp(_animStartT + elapsed / FadeDurationMs, 0.0, 1.0);

            foreach (var state in _saved.Values.ToList())
            {
                try
                {
                    if (!NativeMethods.IsWindow(state.Handle))
                        continue;

                    byte from;
                    byte to;
                    if (_direction == FadeDirection.ToPeek)
                    {
                        from = state.OriginalAlpha;
                        to = _opacity;
                    }
                    else
                    {
                        from = _opacity;
                        to = state.OriginalAlpha;
                    }

                    var alpha = (byte)Math.Round(from + (to - from) * t);
                    SetAlphaOnly(state.Handle, alpha);
                }
                catch
                {
                    // skip dead hwnd
                }
            }

            if (t >= 1.0)
            {
                if (_direction == FadeDirection.ToRestore)
                {
                    StopAnimation();
                    FinishRestoreLocked();
                }
                else
                {
                    foreach (var hwnd in _saved.Keys.ToList())
                        SetAlphaOnly(hwnd, _opacity);
                    StopAnimation();
                }
            }
        }
    }

    private void FinishRestoreLocked()
    {
        foreach (var kv in _saved.ToList())
        {
            try
            {
                RestoreWindow(kv.Value);
            }
            catch
            {
                // handle may be gone
            }
        }

        _saved.Clear();
        _active = false;
        _direction = FadeDirection.None;
    }

    private static void ApplyPeekStyles(IntPtr hwnd, byte opacity)
    {
        if (!NativeMethods.IsWindow(hwnd))
            return;

        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE);
        var newEx = exStyle | NativeConstants.WS_EX_LAYERED | NativeConstants.WS_EX_TRANSPARENT;
        if (newEx != exStyle)
            NativeMethods.SetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE, newEx);

        NativeMethods.SetLayeredWindowAttributes(hwnd, 0, opacity, NativeConstants.LWA_ALPHA);

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE |
            NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE |
            NativeConstants.SWP_FRAMECHANGED);
    }

    private static void SetAlphaOnly(IntPtr hwnd, byte opacity)
    {
        if (!NativeMethods.IsWindow(hwnd))
            return;
        NativeMethods.SetLayeredWindowAttributes(hwnd, 0, opacity, NativeConstants.LWA_ALPHA);
    }

    private static void RestoreWindow(WindowStateInfo state)
    {
        var hwnd = state.Handle;
        if (!NativeMethods.IsWindow(hwnd))
            return;

        if (state.HadLayered && state.HadAlpha)
        {
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, state.OriginalAlpha, NativeConstants.LWA_ALPHA);
        }
        else if (state.HadLayered)
        {
            // Originally layered but without alpha — leave attributes alone
        }
        else
        {
            NativeMethods.SetLayeredWindowAttributes(hwnd, 0, 255, NativeConstants.LWA_ALPHA);
        }

        var currentEx = NativeMethods.GetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE);
        var restored = currentEx;

        restored &= ~NativeConstants.WS_EX_TRANSPARENT;

        if (!state.HadLayered)
            restored &= ~NativeConstants.WS_EX_LAYERED;

        if (state.HadTransparent)
            restored |= NativeConstants.WS_EX_TRANSPARENT;

        if (restored != currentEx)
            NativeMethods.SetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE, restored);

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE |
            NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE |
            NativeConstants.SWP_FRAMECHANGED);
    }

    public void Dispose()
    {
        ExitImmediate();
        _fadeTimer.Tick -= OnFadeTick;
        _fadeTimer.Dispose();
    }
}
