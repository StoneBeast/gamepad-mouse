namespace GamepadMouse;

/// <summary>极简文件日志：%APPDATA%\GamepadMouse\log.txt</summary>
internal static class Log
{
    private static readonly object _lock = new();
    public static string LogPath => Path.Combine(MappingConfig.ConfigDir, "log.txt");

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", ex == null ? msg : $"{msg} :: {ex}");

    private static void Write(string level, string msg)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(MappingConfig.ConfigDir);
                // 日志超过 1MB 时轮转，避免无限增长
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
                    File.Move(LogPath, LogPath + ".old", overwrite: true);

                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
