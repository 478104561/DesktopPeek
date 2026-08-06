namespace DesktopPeek.Models;

internal sealed class WindowStateInfo
{
    public IntPtr Handle { get; init; }
    public int OriginalExStyle { get; init; }
    public bool HadLayered { get; init; }
    public bool HadTransparent { get; init; }
    public byte OriginalAlpha { get; init; } = 255;
    public bool HadAlpha { get; init; }
}
