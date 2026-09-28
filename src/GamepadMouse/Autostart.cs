using Microsoft.Win32;

namespace GamepadMouse;

/// <summary>HKEY_CURRENT_USER 开机自启管理。</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GamepadMouse";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string _;
    }

    public static void SetEnabled(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            key.SetValue(ValueName, $"\"{exe}\"");
            Log.Info($"已开启开机自启: {exe}");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            Log.Info("已关闭开机自启");
        }
    }
}
