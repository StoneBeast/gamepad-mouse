using System.Security.Principal;

namespace GamepadMouse;

/// <summary>当前进程提权状态检测。</summary>
internal static class Elevation
{
    public static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
