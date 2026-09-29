using System.Diagnostics;
using Microsoft.Win32;

namespace GamepadMouse;

/// <summary>
/// 开机自启管理：注册计划任务（登录时触发、最高权限运行）。
/// 相比 HKCU Run 注册表项，计划任务以最高权限启动不会弹出 UAC 确认框。
/// </summary>
internal static class Autostart
{
    private const string TaskName = "GamepadMouse";
    // 旧版本通过 HKCU Run 注册表项自启，新版本不再使用，遇到时清理
    private const string LegacyRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyValueName = "GamepadMouse";

    public static bool IsEnabled()
    {
        CleanupLegacy();
        try
        {
            using var p = StartSchtasks($"/Query /TN \"{TaskName}\"");
            p!.WaitForExit(10_000);
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Warn($"查询自启计划任务失败: {ex.Message}");
            return false;
        }
    }

    public static void SetEnabled(bool on)
    {
        CleanupLegacy();
        if (on)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            // /RL HIGHEST：以最高权限运行，登录后即拥有管理员权限且不弹 UAC
            // /F：同名任务已存在时强制覆盖，保证任务指向最新的 exe 路径
            using (var p = StartSchtasks(
                       $"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F"))
            {
                p!.WaitForExit(10_000);
                if (p.ExitCode != 0)
                {
                    var err = p.StandardError.ReadToEnd().Trim();
                    throw new InvalidOperationException(string.IsNullOrEmpty(err)
                        ? $"schtasks 退出码 {p.ExitCode}"
                        : err);
                }
            }

            Log.Info($"已创建自启计划任务（最高权限）: {exe}");
        }
        else
        {
            using var p = StartSchtasks($"/Delete /TN \"{TaskName}\" /F");
            p!.WaitForExit(10_000);
            if (p.ExitCode != 0)
            {
                var err = p.StandardError.ReadToEnd().Trim();
                // 任务不存在视为已关闭，不算失败
                if (!err.Contains("不存在", StringComparison.Ordinal)
                    && !err.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(string.IsNullOrEmpty(err)
                        ? $"schtasks 退出码 {p.ExitCode}"
                        : err);
            }

            Log.Info("已删除自启计划任务");
        }
    }

    /// <summary>删除旧版遗留的 HKCU Run 注册表自启项（若存在）。</summary>
    private static void CleanupLegacy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
            if (key?.GetValue(LegacyValueName) != null)
            {
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
                Log.Info("已清理旧版注册表自启项");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"清理旧版自启项失败: {ex.Message}");
        }
    }

    private static Process StartSchtasks(string arguments) => Process.Start(new ProcessStartInfo
    {
        FileName = "schtasks",
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    }) ?? throw new InvalidOperationException("无法启动 schtasks 进程");
}
