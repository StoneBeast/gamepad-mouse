namespace GamepadMouse;

internal static class Program
{
    private const string MutexName = "Local\\GamepadMouse_SingleInstance";

    [STAThread]
    static void Main(string[] args)
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
            GamepadMouse.Ui.UiTheme.Apply(config.Theme == "Light"
                ? GamepadMouse.Ui.Palette.Light
                : GamepadMouse.Ui.Palette.Dark);
            var mapper = new Mapper(config);
            bool openSettings = args.Any(a => a is "--settings" or "-s");
            Application.Run(new TrayContext(mapper, openSettings));
        }
        catch (Exception ex)
        {
            Log.Error("未处理的致命异常", ex);
            MessageBox.Show("程序发生致命异常，详情见日志：\n" + Log.LogPath + "\n\n" + ex.Message,
                "GamepadMouse", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
