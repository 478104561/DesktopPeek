using DesktopPeek.Native;

namespace DesktopPeek.Services;

internal static class AdminHelper
{
    public static bool IsElevated()
    {
        try
        {
            return NativeMethods.IsUserAnAdmin();
        }
        catch
        {
            return false;
        }
    }

    public static string StatusText() =>
        IsElevated()
            ? "已以管理员身份运行"
            : "普通权限（高权限窗口可能无法透视）";
}
