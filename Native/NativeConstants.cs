namespace DesktopPeek.Native;

internal static class NativeConstants
{
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;

    public const int WS_VISIBLE = 0x10000000;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;

    public const uint LWA_ALPHA = 0x00000002;
    public const uint LWA_COLORKEY = 0x00000001;

    public const int GA_ROOT = 2;
    public const int GA_ROOTOWNER = 3;

    public const uint GW_OWNER = 4;

    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public const int HSHELL_WINDOWCREATED = 1;
    public const int HSHELL_WINDOWDESTROYED = 2;
    public const int HSHELL_WINDOWACTIVATED = 4;
    public const int HSHELL_RUDEAPPACTIVATED = 32772;

    public const int WM_HOTKEY = 0x0312;
    public const int WM_CLOSE = 0x0010;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int VK_ESCAPE = 0x1B;
    public const int VK_OEM_3 = 0xC0; // ` ~ key

    public const int HOTKEY_ID_TOGGLE = 1;
    public const int HOTKEY_ID_EMERGENCY = 2;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const uint RdwInvalidate = 0x0001;
    public const uint RdwUpdateNow = 0x0100;
    public const uint RdwAllChildren = 0x0080;
}
