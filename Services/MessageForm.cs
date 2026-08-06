using DesktopPeek.Native;

namespace DesktopPeek.Services;

/// <summary>
/// Invisible form used as a message pump for Shell Hook and global hotkeys.
/// </summary>
internal sealed class MessageForm : Form
{
    private readonly uint _shellHookMsg;
    private readonly Action<int, IntPtr> _onShellHook;
    private readonly Action<int> _onHotkey;
    private bool _shellRegistered;

    public MessageForm(Action<int, IntPtr> onShellHook, Action<int> onHotkey)
    {
        _onShellHook = onShellHook;
        _onHotkey = onHotkey;
        _shellHookMsg = NativeMethods.RegisterWindowMessage("SHELLHOOK");

        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(0, 0);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Text = "DesktopPeek.MessageForm";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeConstants.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _shellRegistered = NativeMethods.RegisterShellHookWindow(Handle);

        NativeMethods.RegisterHotKey(
            Handle,
            NativeConstants.HOTKEY_ID_TOGGLE,
            NativeConstants.MOD_CONTROL | NativeConstants.MOD_NOREPEAT,
            NativeConstants.VK_OEM_3);

        NativeMethods.RegisterHotKey(
            Handle,
            NativeConstants.HOTKEY_ID_EMERGENCY,
            NativeConstants.MOD_WIN | NativeConstants.MOD_NOREPEAT,
            (uint)NativeConstants.VK_ESCAPE);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        try
        {
            NativeMethods.UnregisterHotKey(Handle, NativeConstants.HOTKEY_ID_TOGGLE);
            NativeMethods.UnregisterHotKey(Handle, NativeConstants.HOTKEY_ID_EMERGENCY);
        }
        catch { /* ignore */ }

        if (_shellRegistered)
        {
            try { NativeMethods.DeregisterShellHookWindow(Handle); } catch { /* ignore */ }
            _shellRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void SetVisibleCore(bool value)
    {
        // Never show this helper window
        base.SetVisibleCore(false);
    }

    protected override void WndProc(ref Message m)
    {
        if (_shellHookMsg != 0 && (uint)m.Msg == _shellHookMsg)
        {
            var shellCode = m.WParam.ToInt32() & 0xFFFF;
            if (shellCode is NativeConstants.HSHELL_WINDOWCREATED
                or NativeConstants.HSHELL_WINDOWACTIVATED)
            {
                _onShellHook(shellCode, m.LParam);
            }
        }
        else if (m.Msg == NativeConstants.WM_HOTKEY)
        {
            _onHotkey(m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }
}
