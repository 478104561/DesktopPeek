using DesktopPeek.Models;
using DesktopPeek.Native;
using DesktopPeek.Services;

namespace DesktopPeek.UI;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly AppConfig _config;
    private readonly TransparentModeService _transparent;
    private readonly MouseMonitorService _mouseMonitor;
    private readonly MessageForm _messageForm;
    private readonly System.Windows.Forms.Timer _restoreDelayTimer;
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _opacityItem;
    private ToolStripMenuItem? _hoverDelayItem;
    private ToolStripMenuItem? _autoStartItem;
    private bool _exiting;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();
        if (_config.AutoStart != AutostartService.IsEnabled())
            _config.AutoStart = AutostartService.IsEnabled();

        _transparent = new TransparentModeService { Opacity = _config.Opacity };

        _restoreDelayTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _restoreDelayTimer.Tick += (_, _) =>
        {
            _restoreDelayTimer.Stop();
            RestoreFromShellEvent();
        };

        _messageForm = new MessageForm(OnShellHook, OnHotkey);
        // Force handle creation for shell hook / hotkeys
        _ = _messageForm.Handle;
        _transparent.SetSelfWindow(_messageForm.Handle);

        _mouseMonitor = new MouseMonitorService(_transparent, ExitPeek, _config.HoverDelayMs);
        _mouseMonitor.Start();

        _tray = new NotifyIcon
        {
            Text = "Desktop Peek",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => TogglePeek();

        _tray.ShowBalloonTip(
            3000,
            "Desktop Peek",
            $"已在后台运行。悬停桌面空白处启用透视。\n{AdminHelper.StatusText()}\n热键: Ctrl+` 切换 / Win+Esc 紧急恢复",
            ToolTipIcon.Info);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("状态: 待命") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        _opacityItem = new ToolStripMenuItem($"透明度: {_config.Opacity}");
        var trackHost = new ToolStripControlHost(CreateOpacityTrackBar())
        {
            AutoSize = false,
            Size = new Size(180, 40)
        };
        _opacityItem.DropDownItems.Add(trackHost);
        menu.Items.Add(_opacityItem);

        _hoverDelayItem = new ToolStripMenuItem($"悬停延迟: {_config.HoverDelayMs}ms");
        var delayHost = new ToolStripControlHost(CreateHoverDelayTrackBar())
        {
            AutoSize = false,
            Size = new Size(180, 40)
        };
        _hoverDelayItem.DropDownItems.Add(delayHost);
        menu.Items.Add(_hoverDelayItem);

        menu.Items.Add(new ToolStripMenuItem("手动切换透视 (Ctrl+`)", null, (_, _) => TogglePeek()));
        menu.Items.Add(new ToolStripMenuItem("立即恢复 (Win+Esc)", null, (_, _) => ExitPeekImmediate()));
        menu.Items.Add(new ToolStripSeparator());

        _autoStartItem = new ToolStripMenuItem("开机自启", null, OnToggleAutostart)
        {
            Checked = AutostartService.IsEnabled()
        };
        menu.Items.Add(_autoStartItem);

        menu.Items.Add(new ToolStripMenuItem(AdminHelper.StatusText()) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApp()));

        menu.Opening += (_, _) => RefreshStatus();
        return menu;
    }

    private TrackBar CreateOpacityTrackBar()
    {
        var bar = new TrackBar
        {
            Minimum = 0,
            Maximum = 200,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 20,
            Value = _config.Opacity,
            Width = 160,
            Height = 36
        };

        bar.ValueChanged += (_, _) =>
        {
            var value = AppConfig.ClampOpacity(bar.Value);
            _config.Opacity = value;
            _transparent.Opacity = value;
            _config.Save();
            if (_opacityItem is not null)
                _opacityItem.Text = $"透明度: {value}";
        };

        return bar;
    }

    private TrackBar CreateHoverDelayTrackBar()
    {
        var bar = new TrackBar
        {
            Minimum = 0,
            Maximum = 3000,
            TickFrequency = 500,
            SmallChange = 50,
            LargeChange = 100,
            Value = AppConfig.ClampHoverDelay(_config.HoverDelayMs),
            Width = 160,
            Height = 36
        };

        bar.ValueChanged += (_, _) =>
        {
            var value = AppConfig.ClampHoverDelay(bar.Value);
            // Snap to 50ms steps for cleaner values
            value = (value / 50) * 50;
            if (bar.Value != value)
            {
                bar.Value = value;
                return;
            }

            _config.HoverDelayMs = value;
            _mouseMonitor.HoverDelayMs = value;
            _config.Save();
            if (_hoverDelayItem is not null)
                _hoverDelayItem.Text = $"悬停延迟: {value}ms";
        };

        return bar;
    }

    private void RefreshStatus()
    {
        if (_statusItem is null) return;
        _statusItem.Text = _transparent.IsActive ? "状态: 透视中" : "状态: 待命";
        if (_autoStartItem is not null)
            _autoStartItem.Checked = AutostartService.IsEnabled();
    }

    private void OnToggleAutostart(object? sender, EventArgs e)
    {
        var enabled = !AutostartService.IsEnabled();
        try
        {
            AutostartService.SetEnabled(enabled);
            _config.AutoStart = enabled;
            _config.Save();
            if (_autoStartItem is not null)
                _autoStartItem.Checked = enabled;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法设置开机自启: {ex.Message}", "Desktop Peek",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnShellHook(int code, IntPtr hwnd)
    {
        if (!_transparent.IsActive || _exiting)
            return;

        if (hwnd == IntPtr.Zero || hwnd == _messageForm.Handle)
            return;

        // Desktop / taskbar activation (e.g. selecting icons) must not exit peek
        try
        {
            var cls = NativeMethods.GetClassName(hwnd);
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
                or "NotifyIconOverflowWindow")
                return;

            if (!NativeMethods.IsWindowVisible(hwnd) && code != NativeConstants.HSHELL_WINDOWCREATED)
                return;
        }
        catch
        {
            return;
        }

        _restoreDelayTimer.Stop();
        _restoreDelayTimer.Start();
    }

    private void RestoreFromShellEvent()
    {
        if (!_transparent.IsActive)
            return;

        try
        {
            ExitPeek();
        }
        catch
        {
            // ignore
        }
    }

    private void OnHotkey(int id)
    {
        if (id == NativeConstants.HOTKEY_ID_TOGGLE)
            TogglePeek();
        else if (id == NativeConstants.HOTKEY_ID_EMERGENCY)
            ExitPeekImmediate();
    }

    private void TogglePeek()
    {
        if (_transparent.IsActive)
            ExitPeek();
        else
        {
            _mouseMonitor.Enabled = false;
            _transparent.Enter();
            _mouseMonitor.Enabled = true;
            RefreshStatus();
        }
    }

    private void ExitPeek()
    {
        _transparent.Exit();
        RefreshStatus();
    }

    private void ExitPeekImmediate()
    {
        _transparent.ExitImmediate();
        RefreshStatus();
    }

    private void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;

        try
        {
            _mouseMonitor.Stop();
            _transparent.ExitImmediate();
            _config.Save();
        }
        finally
        {
            _tray.Visible = false;
            _tray.Dispose();
            _mouseMonitor.Dispose();
            _restoreDelayTimer.Dispose();
            _messageForm.Dispose();
            _transparent.Dispose();
            ExitThread();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_exiting)
        {
            try { _transparent.ExitImmediate(); } catch { /* ignore */ }
            _mouseMonitor.Dispose();
            _restoreDelayTimer.Dispose();
            _messageForm.Dispose();
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}
