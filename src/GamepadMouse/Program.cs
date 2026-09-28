namespace GamepadMouse;

internal static class Program
{
    private const string MutexName = "Local\\GamepadMouse_SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("GamepadMouse 已经在运行（请查看系统托盘）。", "GamepadMouse",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        try
        {
            var config = MappingConfig.Load();
            var mapper = new Mapper(config);
            Application.Run(new TrayContext(mapper));
        }
        catch (Exception ex)
        {
            Log.Error("未处理的致命异常", ex);
            MessageBox.Show("程序发生致命异常，详情见日志：\n" + Log.LogPath + "\n\n" + ex.Message,
                "GamepadMouse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
