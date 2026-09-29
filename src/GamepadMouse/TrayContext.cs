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
    private AboutForm? _about;

    public TrayContext(Mapper mapper, bool openSettingsOnStart = false)
    {
        _mapper = mapper;

        _menuToggle = new ToolStripMenuItem("启用鼠标映射", null, (_, _) =>
        {
            _mapper.Toggle();
        })
        { CheckOnClick = true, Checked = mapper.Enabled };

        // 不用 CheckOnClick：它会在 Click 事件前自动翻转 Checked，处理器里再取反
        // 等于双重取反，SetEnabled 永远收到旧状态，开关形同虚设。
        // 勾选状态统一以计划任务为准，点击时取“显示状态的反面”，结束后回读校正。
        _menuAutostart = new ToolStripMenuItem("开机自动启动", null, (_, _) =>
        {
            bool want = !_menuAutostart.Checked;
            try
            {
                Autostart.SetEnabled(want);
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _menuAutostart.Checked = Autostart.IsEnabled();
            }
        })
        { Checked = Autostart.IsEnabled() };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_menuToggle);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
        menu.Items.Add(_menuAutostart);
        menu.Items.Add("关于…", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        // 设置窗口等其它途径也可能改过自启，弹出时以计划任务为准刷新勾选
        menu.Opening += (_, _) => _menuAutostart.Checked = Autostart.IsEnabled();

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

        MouseSimulator.InputDenied += NotifyNotElevated;
        if (!Elevation.IsAdmin())
            NotifyNotElevated();

        if (openSettingsOnStart)
            ShowSettings();
    }

    private string TooltipText() => _mapper.Enabled
        ? "GamepadMouse：映射已开启（双击打开设置）"
        : "GamepadMouse：映射已关闭（双击打开设置）";

    /// <summary>未提权时映射无法作用于管理员权限的窗口（如游戏启动器），气泡提示一次。</summary>
    private void NotifyNotElevated()
    {
        void show() => _tray.ShowBalloonTip(8000, "GamepadMouse 未以管理员身份运行",
            "映射将无法操作管理员权限的窗口（如游戏启动器、任务管理器）。\n请重新以管理员身份运行本程序。",
            ToolTipIcon.Warning);
        if (_tray.ContextMenuStrip!.InvokeRequired)
            _tray.ContextMenuStrip.BeginInvoke(show);
        else
            show();
    }

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
            if (_settings.WindowState == FormWindowState.Minimized)
                _settings.WindowState = FormWindowState.Normal;
            // 短暂置顶确保一定能盖过其它窗口，随后恢复普通层级
            _settings.TopMost = true;
            _settings.Activate();
            _settings.TopMost = false;
            return;
        }
        _settings = new SettingsForm(_mapper);
        _settings.Show();
        _settings.TopMost = true;
        _settings.Activate();
        _settings.TopMost = false;
    }

    private void ShowAbout()
    {
        if (_about != null && !_about.IsDisposed)
        {
            _about.TopMost = true;
            _about.Activate();
            _about.TopMost = false;
            return;
        }
        _about = new AboutForm();
        _about.Show();
        _about.TopMost = true;
        _about.Activate();
        _about.TopMost = false;
    }

    private void ExitApp()
    {
        _mapper.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }
}
