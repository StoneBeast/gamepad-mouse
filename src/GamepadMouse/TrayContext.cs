namespace GamepadMouse;

/// <summary>
/// 托盘上下文：NotifyIcon + 菜单（开关映射 / 设置 / 自启 / 退出），图标随映射状态变色。
/// </summary>
internal class TrayContext : ApplicationContext
{
    private readonly Mapper _mapper;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _menuToggle = null!;
    private readonly ToolStripMenuItem _menuAutostart = null!;
    private SettingsForm? _settings;

    public TrayContext(Mapper mapper, bool openSettingsOnStart = false)
    {
        _mapper = mapper;

        _menuToggle = new ToolStripMenuItem("启用鼠标映射", null, (_, _) =>
        {
            _mapper.Toggle();
        })
        { CheckOnClick = true, Checked = mapper.Enabled };

        _menuAutostart = new ToolStripMenuItem("开机自动启动", null, (_, _) =>
        {
            try
            {
                Autostart.SetEnabled(!_menuAutostart.Checked);
                _menuAutostart.Checked = Autostart.IsEnabled();
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        })
        { CheckOnClick = true, Checked = Autostart.IsEnabled() };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_menuToggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
        menu.Items.Add(_menuAutostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Icon = AppIcon.Create(mapper.Enabled),
            Text = TooltipText(),
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        _mapper.EnabledChanged += on =>
        {
            if (_tray.ContextMenuStrip!.InvokeRequired)
                _tray.ContextMenuStrip.BeginInvoke(() => UpdateUiState(on));
            else
                UpdateUiState(on);
        };
        _mapper.StatusChanged += status =>
        {
            void apply()
            {
                _tray.Text = string.IsNullOrEmpty(status) ? TooltipText() : status[..Math.Min(status.Length, 63)];
            }
            if (_tray.ContextMenuStrip!.InvokeRequired)
                _tray.ContextMenuStrip.BeginInvoke(apply);
            else
                apply();
        };

        _mapper.Start();
        UpdateUiState(_mapper.Enabled);

        if (openSettingsOnStart)
            ShowSettings();
    }

    private string TooltipText() => _mapper.Enabled
        ? "GamepadMouse：映射已开启（双击打开设置）"
        : "GamepadMouse：映射已关闭（双击打开设置）";

    private void UpdateUiState(bool on)
    {
        _menuToggle.Checked = on;
        _tray.Icon = AppIcon.Create(on);
        _tray.Text = TooltipText();
    }

    private void ShowSettings()
    {
        if (_settings != null && !_settings.IsDisposed)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsForm(_mapper);
        _settings.Show();
        _settings.Activate();
    }

    private void ExitApp()
    {
        _mapper.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }
}
