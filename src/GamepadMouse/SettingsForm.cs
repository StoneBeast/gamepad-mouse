namespace GamepadMouse;

/// <summary>
/// 设置窗口：组合键录制、按键映射表格、摇杆与灵敏度参数、开机自启。
/// </summary>
internal class SettingsForm : Form
{
    private readonly MappingConfig _working; // 编辑中的副本
    private readonly Mapper _mapper;

    private Label _chordLabel = null!;
    private System.Windows.Forms.Timer _padTimer = null!;
    private DataGridView _grid = null!;
    private NumericUpDown _numSens = null!;
    private NumericUpDown _numScroll = null!;
    private NumericUpDown _numDeadzone = null!;
    private NumericUpDown _numCurve = null!;
    private NumericUpDown _numPoll = null!;
    private RadioButton _radioMoveRight = null!;
    private RadioButton _radioMoveLeft = null!;
    private ComboBox _comboScrollStick = null!;
    private CheckBox _chkVibrate = null!;
    private CheckBox _chkAutostart = null!;
    private Label _livePadLabel = null!;

    public bool Saved { get; private set; }

    public SettingsForm(Mapper mapper)
    {
        _mapper = mapper;
        _working = CloneConfig(mapper.Config);

        Text = "GamepadMouse 设置";
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 640);

        BuildUi();

        _padTimer = new System.Windows.Forms.Timer { Interval = 60 };
        _padTimer.Tick += (_, _) => PollPadForLiveView();
        _padTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _padTimer?.Stop();
        _padTimer?.Dispose();
        base.OnFormClosed(e);
    }

    private static MappingConfig CloneConfig(MappingConfig src)
    {
        var c = new MappingConfig
        {
            ToggleChord = [.. src.ToggleChord],
            MoveStick = src.MoveStick,
            ScrollStick = src.ScrollStick,
            Sensitivity = src.Sensitivity,
            ScrollSensitivity = src.ScrollSensitivity,
            Deadzone = src.Deadzone,
            Curve = src.Curve,
            PollRateMs = src.PollRateMs,
            TriggerThreshold = src.TriggerThreshold,
            StartEnabled = src.StartEnabled,
            VibrateOnToggle = src.VibrateOnToggle,
            ButtonMappings = new Dictionary<string, string>(src.ButtonMappings),
        };
        return c;
    }

    // ---------------- UI 构建 ----------------

    private void BuildUi()
    {
        int y = 12;

        // 组合键开关
        var grpChord = new GroupBox
        {
            Text = "开关映射的组合键（在手柄上同时按住）",
            Location = new Point(12, y),
            Size = new Size(536, 74),
        };
        _chordLabel = new Label
        {
            Text = ChordText(),
            Location = new Point(12, 30),
            Size = new Size(300, 24),
        };
        var btnCapture = new Button { Text = "录制组合键…", Location = new Point(330, 26), Size = new Size(96, 30) };
        btnCapture.Click += (_, _) => CaptureChord();
        var btnClear = new Button { Text = "清除", Location = new Point(432, 26), Size = new Size(64, 30) };
        btnClear.Click += (_, _) => { _working.ToggleChord.Clear(); _chordLabel.Text = "（未设置）"; };
        grpChord.Controls.AddRange([_chordLabel, btnCapture, btnClear]);
        Controls.Add(grpChord);
        y += 86;

        // 按键映射表格
        var grpMap = new GroupBox
        {
            Text = "按键映射（手柄按键 → 鼠标动作）",
            Location = new Point(12, y),
            Size = new Size(536, 300),
        };
        _grid = new DataGridView
        {
            Location = new Point(10, 22),
            Size = new Size(516, 268),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        var colBtn = new DataGridViewTextBoxColumn
        {
            Name = "Button",
            HeaderText = "手柄按键",
            FillWeight = 35,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        var colAct = new DataGridViewComboBoxColumn
        {
            Name = "Action",
            HeaderText = "鼠标动作",
            FillWeight = 65,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var a in MappingConfig.AllActions) colAct.Items.Add(MappingConfig.ActionDisplay[a]);
        _grid.Columns.Add(colBtn);
        _grid.Columns.Add(colAct);

        foreach (var b in MappingConfig.AllButtons)
        {
            int idx = _grid.Rows.Add(b, MappingConfig.ActionDisplay[_working.ButtonMappings[b]]);
            _grid.Rows[idx].Cells[1].Tag = _working.ButtonMappings[b];
        }
        _grid.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == 0 && e.Value is string s)
                e.Value = ButtonDisplay(s);
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 1)
            {
                var cell = _grid.Rows[e.RowIndex].Cells[1];
                var display = cell.Value as string;
                var act = MappingConfig.ActionDisplay.FirstOrDefault(kv => kv.Value == display).Key
                          ?? MappingConfig.ActNone;
                cell.Tag = act;
                _working.ButtonMappings[_grid.Rows[e.RowIndex].Cells[0].Value!.ToString()!] = act;
            }
        };
        grpMap.Controls.Add(_grid);
        Controls.Add(grpMap);
        y += 312;

        // 摇杆与参数
        var grpParam = new GroupBox
        {
            Text = "摇杆与参数",
            Location = new Point(12, y),
            Size = new Size(536, 160),
        };

        _radioMoveRight = new RadioButton { Text = "右摇杆移动光标", Location = new Point(14, 24), Size = new Size(130, 24), Checked = true };
        _radioMoveLeft = new RadioButton { Text = "左摇杆移动光标", Location = new Point(150, 24), Size = new Size(130, 24) };
        if (_working.MoveStick == "Left") { _radioMoveLeft.Checked = true; _radioMoveRight.Checked = false; }

        var lblScroll = new Label { Text = "滚动摇杆：", Location = new Point(300, 24), Size = new Size(80, 24) };
        _comboScrollStick = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(382, 21),
            Size = new Size(130, 26),
        };
        _comboScrollStick.Items.AddRange(["左摇杆", "右摇杆", "不使用"]);
        _comboScrollStick.SelectedIndex = _working.ScrollStick switch
        {
            "Left" => 0, "Right" => 1, _ => 2
        };

        AddNumeric(grpParam, "光标速度（像素/秒）", 14, 58, _numSens = new NumericUpDown(), (decimal)_working.Sensitivity, 200, 20000, 100);
        AddNumeric(grpParam, "滚轮速度（格/秒）", 278, 58, _numScroll = new NumericUpDown(), (decimal)_working.ScrollSensitivity, 0.5m, 30m, 0.5m);
        AddNumeric(grpParam, "摇杆死区（0~0.5）", 14, 92, _numDeadzone = new NumericUpDown(), (decimal)_working.Deadzone, 0m, 0.5m, 0.01m);
        AddNumeric(grpParam, "响应曲线（1=线性）", 278, 92, _numCurve = new NumericUpDown(), (decimal)_working.Curve, 1m, 3m, 0.1m);
        AddNumeric(grpParam, "轮询间隔（毫秒）", 14, 126, _numPoll = new NumericUpDown(), _working.PollRateMs, 4, 50, 1);

        _chkVibrate = new CheckBox { Text = "开关映射时手柄震动提示", Location = new Point(278, 126), Size = new Size(240, 24), Checked = _working.VibrateOnToggle };
        grpParam.Controls.AddRange([_radioMoveRight, _radioMoveLeft, lblScroll, _comboScrollStick, _chkVibrate]);
        Controls.Add(grpParam);
        y += 172;

        // 手柄实时状态 + 自启
        _livePadLabel = new Label { Text = "手柄状态：检测中…", Location = new Point(14, y), Size = new Size(300, 24) };
        _chkAutostart = new CheckBox { Text = "开机自动启动", Location = new Point(330, y), Size = new Size(200, 24), Checked = Autostart.IsEnabled() };
        Controls.Add(_livePadLabel);
        Controls.Add(_chkAutostart);
        y += 36;

        // 按钮
        var btnOk = new Button { Text = "保存并应用", Location = new Point(270, y), Size = new Size(120, 34) };
        btnOk.Click += (_, _) => SaveAndClose();
        var btnCancel = new Button { Text = "取消", Location = new Point(400, y), Size = new Size(80, 34) };
        btnCancel.Click += (_, _) => Close();
        var btnDefaults = new Button { Text = "恢复默认", Location = new Point(12, y), Size = new Size(96, 34) };
        btnDefaults.Click += (_, _) => ResetDefaults();
        Controls.AddRange([btnOk, btnCancel, btnDefaults]);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void AddNumeric(GroupBox parent, string label, int x, int y, NumericUpDown num,
        decimal value, decimal min, decimal max, decimal step)
    {
        var lbl = new Label { Text = label, Location = new Point(x, y + 4), Size = new Size(150, 20) };
        num.Location = new Point(x + 155, y);
        num.Size = new Size(90, 26);
        num.Minimum = min;
        num.Maximum = max;
        num.Increment = step;
        num.Value = value;
        num.DecimalPlaces = step < 1 ? 2 : 0;
        parent.Controls.Add(lbl);
        parent.Controls.Add(num);
    }

    private static string ButtonDisplay(string b) => b switch
    {
        "LT" => "LT（左扳机）",
        "RT" => "RT（右扳机）",
        "LB" => "LB（左肩键）",
        "RB" => "RB（右肩键）",
        "LSB" => "LSB（左摇杆按下）",
        "RSB" => "RSB（右摇杆按下）",
        _ => b
    };

    private string ChordText() =>
        _working.ToggleChord.Count == 0 ? "（未设置）" : string.Join(" + ", _working.ToggleChord);

    // ---------------- 手柄实时显示 / 组合键录制 ----------------

    private HashSet<string> CurrentHeld()
    {
        var set = new HashSet<string>();
        if (!XInput.Available) return set;
        var state = new XInput.State();
        if (!XInput.GetState(0, ref state)) return set;

        var g = state.Gamepad;
        ushort w = g.wButtons;
        if ((w & XInput.A) != 0) set.Add("A");
        if ((w & XInput.B) != 0) set.Add("B");
        if ((w & XInput.X) != 0) set.Add("X");
        if ((w & XInput.Y) != 0) set.Add("Y");
        if ((w & XInput.LeftShoulder) != 0) set.Add("LB");
        if ((w & XInput.RightShoulder) != 0) set.Add("RB");
        if (g.bLeftTrigger >= _working.TriggerThreshold) set.Add("LT");
        if (g.bRightTrigger >= _working.TriggerThreshold) set.Add("RT");
        if ((w & XInput.Back) != 0) set.Add("Back");
        if ((w & XInput.Start) != 0) set.Add("Start");
        if ((w & XInput.LeftThumb) != 0) set.Add("LSB");
        if ((w & XInput.RightThumb) != 0) set.Add("RSB");
        if ((w & XInput.DPadUp) != 0) set.Add("DPadUp");
        if ((w & XInput.DPadDown) != 0) set.Add("DPadDown");
        if ((w & XInput.DPadLeft) != 0) set.Add("DPadLeft");
        if ((w & XInput.DPadRight) != 0) set.Add("DPadRight");
        return set;
    }

    private void PollPadForLiveView()
    {
        try
        {
            var held = CurrentHeld();
            if (!XInput.Available)
                _livePadLabel.Text = "手柄状态：未找到 XInput 驱动";
            else if (held.Count == 0 && MapperPadDisconnected())
                _livePadLabel.Text = "手柄状态：未连接";
            else
                _livePadLabel.Text = held.Count == 0
                    ? "手柄状态：已连接"
                    : "当前按下：" + string.Join(" + ", held);
        }
        catch { /* 忽略 */ }
    }

    private bool _lastSeenPad;
    private bool MapperPadDisconnected()
    {
        if (!XInput.Available) return true;
        var s = new XInput.State();
        bool connected = XInput.GetState(0, ref s);
        _lastSeenPad = connected;
        return !connected && !_lastSeenPad;
    }

    private void CaptureChord()
    {
        using var dlg = new Form
        {
            Text = "录制组合键",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(380, 140),
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = Font,
        };
        var lbl = new Label
        {
            Text = "请同时按住想使用的按键，然后点击「使用当前按键」。\n当前按住：（无）",
            Location = new Point(14, 14),
            Size = new Size(352, 50),
        };
        var t = new System.Windows.Forms.Timer { Interval = 60 };
        HashSet<string> current = new();
        t.Tick += (_, _) =>
        {
            var held = CurrentHeld();
            current = held;
            lbl.Text = "请同时按住想使用的按键，然后点击「使用当前按键」。\n当前按住：" +
                       (held.Count == 0 ? "（无）" : string.Join(" + ", held));
        };
        t.Start();
        var btnOk = new Button { Text = "使用当前按键", Location = new Point(100, 88), Size = new Size(120, 30) };
        var btnCancel = new Button { Text = "取消", Location = new Point(230, 88), Size = new Size(80, 30) };
        btnOk.Click += (_, _) => { dlg.Tag = true; dlg.Close(); };
        btnCancel.Click += (_, _) => { dlg.Tag = false; dlg.Close(); };
        dlg.Controls.AddRange([lbl, btnOk, btnCancel]);
        dlg.FormClosed += (_, _) => { t.Stop(); t.Dispose(); };

        dlg.ShowDialog(this);

        if (dlg.Tag is true && current.Count > 0)
        {
            _working.ToggleChord = [.. current];
            _chordLabel.Text = ChordText();
        }
        else if (dlg.Tag is true)
        {
            MessageBox.Show(this, "没有检测到按下的按键。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ---------------- 保存 / 恢复默认 ----------------

    private void ResetDefaults()
    {
        var def = new MappingConfig();
        _working.ToggleChord = [.. def.ToggleChord];
        _working.MoveStick = def.MoveStick;
        _working.ScrollStick = def.ScrollStick;
        _working.Sensitivity = def.Sensitivity;
        _working.ScrollSensitivity = def.ScrollSensitivity;
        _working.Deadzone = def.Deadzone;
        _working.Curve = def.Curve;
        _working.PollRateMs = def.PollRateMs;
        _working.TriggerThreshold = def.TriggerThreshold;
        _working.VibrateOnToggle = def.VibrateOnToggle;

        _chordLabel.Text = ChordText();
        _radioMoveRight.Checked = true;
        _comboScrollStick.SelectedIndex = 0;
        _numSens.Value = (decimal)_working.Sensitivity;
        _numScroll.Value = (decimal)_working.ScrollSensitivity;
        _numDeadzone.Value = (decimal)_working.Deadzone;
        _numCurve.Value = (decimal)_working.Curve;
        _numPoll.Value = _working.PollRateMs;
        _chkVibrate.Checked = true;

        for (int i = 0; i < MappingConfig.AllButtons.Length; i++)
        {
            string b = MappingConfig.AllButtons[i];
            _grid.Rows[i].Cells[1].Value = MappingConfig.ActionDisplay[def.ButtonMappings[b]];
            _grid.Rows[i].Cells[1].Tag = def.ButtonMappings[b];
        }
    }

    private void SaveAndClose()
    {
        _working.MoveStick = _radioMoveLeft.Checked ? "Left" : "Right";
        _working.ScrollStick = _comboScrollStick.SelectedIndex switch { 0 => "Left", 1 => "Right", _ => "None" };
        _working.Sensitivity = (double)_numSens.Value;
        _working.ScrollSensitivity = (double)_numScroll.Value;
        _working.Deadzone = (double)_numDeadzone.Value;
        _working.Curve = (double)_numCurve.Value;
        _working.PollRateMs = (int)_numPoll.Value;
        _working.VibrateOnToggle = _chkVibrate.Checked;
        _working.Normalize();

        _mapper.ApplyConfig(_working);

        bool wantAuto = _chkAutostart.Checked;
        if (wantAuto != Autostart.IsEnabled())
        {
            try { Autostart.SetEnabled(wantAuto); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置开机自启失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        Saved = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}
