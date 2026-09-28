using System.Drawing.Drawing2D;
using GamepadMouse.Ui;

namespace GamepadMouse;

/// <summary>
/// 设置窗口（深色现代风格）：映射总开关、组合键录制、按键映射表、摇杆参数、行为开关。
/// </summary>
internal class SettingsForm : Form
{
    private readonly MappingConfig _working; // 编辑中的副本
    private readonly Mapper _mapper;

    // 控件引用
    private StatusPill _pill = null!;
    private ToggleSwitch _swMapping = null!;
    private FlowLayoutPanel _chips = null!;
    private DataGridView _grid = null!;
    private Segmented _segMove = null!;
    private Segmented _segScroll = null!;
    private ModernSlider _sldSens = null!;
    private ModernSlider _sldScroll = null!;
    private ModernSlider _sldDeadzone = null!;
    private ModernSlider _sldCurve = null!;
    private ToggleSwitch _swAutostart = null!;
    private ToggleSwitch _swStartEnabled = null!;
    private ToggleSwitch _swVibrate = null!;
    private readonly Label _padStatus = new();
    private Panel _header = null!;
    private Icon _headerIcon;

    private readonly System.Windows.Forms.Timer _padTimer;

    public bool Saved { get; private set; }

    public SettingsForm(Mapper mapper)
    {
        _mapper = mapper;
        _working = CloneConfig(mapper.Config);

        Text = "GamepadMouse 设置";
        Font = UiTheme.FontUi;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(660, 802);
        BackColor = UiTheme.WindowBg;
        AutoScroll = true;
        Icon = _headerIcon = AppIcon.Create(mapper.Enabled);

        _header = BuildHeader();
        Controls.Add(_header);

        BuildChordCard();
        BuildMappingCard();
        BuildStickCard();
        BuildBehaviorCard();
        BuildFooter();

        // 手柄状态轮询（头部副标题）
        _padTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _padTimer.Tick += (_, _) => UpdatePadStatus();
        _padTimer.Start();
        UpdatePadStatus();

        // 映射状态变化（托盘/组合键触发）同步到界面
        mapper.EnabledChanged += on =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(() =>
            {
                _swMapping.SetChecked(on);
                _pill.Set(on ? "映射已开启" : "映射已关闭", on);
                _headerIcon = AppIcon.Create(on);
                Icon = _headerIcon;
                _header.Invalidate();
            });
        };
    }

    private static MappingConfig CloneConfig(MappingConfig src) => new()
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

    // ---------------- 头部 ----------------

    private Panel BuildHeader()
    {
        var p = new UiPanel { Location = new Point(0, 0), Size = new Size(660, 60), BackColor = UiTheme.WindowBg };

        _pill = new StatusPill(new Point(474, 18));
        _pill.Set(_mapper.Enabled ? "映射已开启" : "映射已关闭", _mapper.Enabled);
        _swMapping = new ToggleSwitch(_mapper.Enabled, new Point(600, 18));
        _swMapping.CheckedChanged += (_, _) => _mapper.SetEnabled(_swMapping.Checked);

        p.Controls.AddRange([_pill, _swMapping]);
        p.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawIcon(_headerIcon, new Rectangle(16, 13, 34, 34));
            TextRenderer.DrawText(g, "GamepadMouse", UiTheme.FontTitle, new Point(62, 10), UiTheme.TextPrimary);
            TextRenderer.DrawText(g, _padStatus.Text.Length == 0 ? "手柄映射鼠标 v1.1" : _padStatus.Text,
                UiTheme.FontTitleSub, new Point(64, 36), UiTheme.TextSecondary);
        };
        return p;
    }

    private void UpdatePadStatus()
    {
        var scratch = new XInput.State();
        bool connected = XInput.Available && XInput.GetState(0, ref scratch);
        string text = !XInput.Available ? "手柄映射鼠标 v1.1 · 未找到 XInput 驱动"
            : connected ? "手柄映射鼠标 v1.1 · 手柄已连接"
            : "手柄映射鼠标 v1.1 · 手柄未连接，等待中…";
        _padStatus.Text = text;
        _header.Invalidate();
    }

    // ---------------- 卡片 1：组合键 ----------------

    private void BuildChordCard()
    {
        var card = new Card("开关映射的组合键", new Point(12, 68), new Size(636, 100));

        var lbl = new Label { Text = "组合键", Location = new Point(24, 46), Size = new Size(56, 24), ForeColor = UiTheme.TextPrimary };
        _chips = new FlowLayoutPanel
        {
            Location = new Point(88, 42),
            Size = new Size(360, 32),
            BackColor = UiTheme.CardBg,
            Padding = new Padding(0),
        };
        var btnCapture = new ModernButton("录制组合键…", new Point(462, 40), new Size(94, 32), ModernButton.Style.Primary);
        btnCapture.Click += (_, _) => CaptureChord();
        var btnClear = new ModernButton("清除", new Point(562, 40), new Size(56, 32));
        btnClear.Click += (_, _) => { _working.ToggleChord.Clear(); RebuildChips(); };

        var hint = new Label
        {
            Text = "同时按住组合键即可开关映射（映射关闭时也有效）。建议选择不映射其他功能的按键。",
            Location = new Point(24, 76),
            Size = new Size(596, 18),
            ForeColor = UiTheme.TextSecondary,
            Font = UiTheme.FontSmall,
        };

        card.Controls.AddRange([lbl, _chips, btnCapture, btnClear, hint]);
        Controls.Add(card);
        RebuildChips();
    }

    private void RebuildChips()
    {
        foreach (Control old in _chips.Controls) old.Dispose();
        _chips.Controls.Clear();

        if (_working.ToggleChord.Count == 0)
        {
            _chips.Controls.Add(new Label
            {
                Text = "（未设置）",
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.FontUi,
                BackColor = UiTheme.CardBg,
            });
            return;
        }

        for (int i = 0; i < _working.ToggleChord.Count; i++)
        {
            if (i > 0)
            {
                _chips.Controls.Add(new Label
                {
                    Text = "+",
                    AutoSize = true,
                    ForeColor = UiTheme.TextSecondary,
                    BackColor = UiTheme.CardBg,
                    Margin = new Padding(4, 7, 4, 0),
                });
            }
            _chips.Controls.Add(new Chip(_working.ToggleChord[i]) { Margin = new Padding(2, 2, 2, 0) });
        }
    }

    // ---------------- 卡片 2：按键映射 ----------------

    private void BuildMappingCard()
    {
        var card = new Card("按键映射", new Point(12, 176), new Size(636, 288));

        _grid = new DataGridView
        {
            Location = new Point(20, 42),
            Size = new Size(596, 240),
            BorderStyle = BorderStyle.None,
            BackgroundColor = UiTheme.CardBg,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };
        _grid.EnableHeadersVisualStyles = false;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(43, 47, 57);
        _grid.RowHeadersVisible = false;
        _grid.ColumnHeadersHeight = 28;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = 26;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(38, 42, 51);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = UiTheme.TextSecondary;
        _grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.FontSmall;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 42, 51);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
        _grid.DefaultCellStyle.BackColor = UiTheme.CardBg;
        _grid.DefaultCellStyle.ForeColor = UiTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(44, 49, 64);
        _grid.DefaultCellStyle.SelectionForeColor = UiTheme.TextPrimary;
        _grid.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(35, 38, 46);

        var colB1 = ButtonColumn();
        var colA1 = ActionColumn();
        var colB2 = ButtonColumn();
        var colA2 = ActionColumn();
        _grid.Columns.AddRange([colB1, colA1, colB2, colA2]);
        _grid.DataError += (_, _) => { /* 忽略下拉项匹配异常 */ };

        // 自绘下拉格：深色背景 + 自定义箭头，替换系统白色 ComboBox 按钮
        _grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 1 && e.ColumnIndex != 3)) return;
            e.PaintBackground(e.CellBounds, false);
            using (var bg = new SolidBrush(e.State.HasFlag(DataGridViewElementStates.Selected)
                ? Color.FromArgb(44, 49, 64) : UiTheme.CardBg))
                e.Graphics.FillRectangle(bg, e.CellBounds);
            var textRect = new Rectangle(e.CellBounds.X + 8, e.CellBounds.Y,
                e.CellBounds.Width - 28, e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, e.FormattedValue?.ToString() ?? "", UiTheme.FontUi,
                textRect, UiTheme.TextPrimary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            // 下拉箭头（小雪佛龙）
            int cx = e.CellBounds.Right - 13;
            int cy = e.CellBounds.Y + e.CellBounds.Height / 2;
            using var pen = new Pen(UiTheme.TextSecondary, 1.6f);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
            e.Graphics.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
            e.Handled = true;
        };

        // 编辑态下拉框贴合深色
        _grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is DataGridViewComboBoxEditingControl cmb)
            {
                cmb.DropDownStyle = ComboBoxStyle.DropDownList;
                cmb.FlatStyle = FlatStyle.Flat;
                cmb.BackColor = UiTheme.CardBg;
                cmb.ForeColor = UiTheme.TextPrimary;
            }
        };

        // 16 个按键拆两列 8 行
        for (int i = 0; i < 8; i++)
        {
            string b1 = MappingConfig.AllButtons[i];
            string b2 = MappingConfig.AllButtons[i + 8];
            _grid.Rows.Add(b1, MappingConfig.ActionDisplay[_working.ButtonMappings[b1]],
                           b2, MappingConfig.ActionDisplay[_working.ButtonMappings[b2]]);
            _grid.Rows[i].Cells[1].Tag = _working.ButtonMappings[b1];
            _grid.Rows[i].Cells[3].Tag = _working.ButtonMappings[b2];
        }

        _grid.CellFormatting += (_, e) =>
        {
            if ((e.ColumnIndex == 0 || e.ColumnIndex == 2) && e.Value is string s)
                e.Value = ButtonDisplay(s);
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 1 && e.ColumnIndex != 3)) return;
            int btnCol = e.ColumnIndex - 1;
            var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var display = cell.Value as string;
            var act = MappingConfig.ActionDisplay.FirstOrDefault(kv => kv.Value == display).Key
                      ?? MappingConfig.ActNone;
            cell.Tag = act;
            _working.ButtonMappings[_grid.Rows[e.RowIndex].Cells[btnCol].Value!.ToString()!] = act;
        };

        _grid.ClearSelection();
        _grid.CurrentCell = null;
        card.Controls.Add(_grid);
        Controls.Add(card);
    }

    private static DataGridViewTextBoxColumn ButtonColumn() => new()
    {
        HeaderText = "手柄按键",
        ReadOnly = true,
        SortMode = DataGridViewColumnSortMode.NotSortable,
        FillWeight = 15,
    };

    private static DataGridViewComboBoxColumn ActionColumn()
    {
        var col = new DataGridViewComboBoxColumn
        {
            HeaderText = "鼠标动作",
            SortMode = DataGridViewColumnSortMode.NotSortable,
            FlatStyle = FlatStyle.Flat,
            FillWeight = 35,
        };
        foreach (var a in MappingConfig.AllActions) col.Items.Add(MappingConfig.ActionDisplay[a]);
        return col;
    }

    private static string ButtonDisplay(string b) => b switch
    {
        "LT" => "LT 左扳机",
        "RT" => "RT 右扳机",
        "LB" => "LB 左肩键",
        "RB" => "RB 右肩键",
        "LSB" => "LSB 左摇杆按下",
        "RSB" => "RSB 右摇杆按下",
        "DPadUp" => "十字键 上",
        "DPadDown" => "十字键 下",
        "DPadLeft" => "十字键 左",
        "DPadRight" => "十字键 右",
        _ => b
    };

    // ---------------- 卡片 3：摇杆与手感 ----------------

    private void BuildStickCard()
    {
        var card = new Card("摇杆与手感", new Point(12, 472), new Size(636, 192));

        var lblMove = new Label { Text = "移动摇杆", Location = new Point(24, 48), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _segMove = new Segmented(["左摇杆", "右摇杆"], _working.MoveStick == "Left" ? 0 : 1, new Point(100, 44), new Size(160, 28));

        var lblScroll = new Label { Text = "滚动摇杆", Location = new Point(300, 48), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _segScroll = new Segmented(["左摇杆", "右摇杆", "不使用"],
            _working.ScrollStick switch { "Left" => 0, "Right" => 1, _ => 2 }, new Point(376, 44), new Size(232, 28));

        var lblSens = new Label { Text = "光标速度", Location = new Point(24, 86), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _sldSens = new ModernSlider(200, 8000, 100, _working.Sensitivity, new Point(100, 80), new Size(508, 30))
        {
            Format = v => $"{v:0} px/s",
        };

        var lblScrollSpeed = new Label { Text = "滚轮速度", Location = new Point(24, 124), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _sldScroll = new ModernSlider(0.5, 20, 0.5, _working.ScrollSensitivity, new Point(100, 118), new Size(508, 30))
        {
            Format = v => $"{v:0.#} 格/s",
        };

        var lblDead = new Label { Text = "摇杆死区", Location = new Point(24, 162), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _sldDeadzone = new ModernSlider(0, 0.5, 0.01, _working.Deadzone, new Point(100, 156), new Size(240, 30))
        {
            Format = v => $"{v * 100:0}%",
        };

        var lblCurve = new Label { Text = "响应曲线", Location = new Point(370, 162), Size = new Size(70, 24), ForeColor = UiTheme.TextPrimary };
        _sldCurve = new ModernSlider(1, 3, 0.05, _working.Curve, new Point(446, 156), new Size(162, 30))
        {
            Format = v => $"{v:0.00}",
        };

        card.Controls.AddRange(
        [
            lblMove, _segMove, lblScroll, _segScroll,
            lblSens, _sldSens, lblScrollSpeed, _sldScroll,
            lblDead, _sldDeadzone, lblCurve, _sldCurve,
        ]);
        Controls.Add(card);
    }

    // ---------------- 卡片 4：行为 ----------------

    private void BuildBehaviorCard()
    {
        var card = new Card("行为", new Point(12, 672), new Size(636, 80));

        var lblAuto = new Label { Text = "开机自启", Location = new Point(24, 46), Size = new Size(68, 24), ForeColor = UiTheme.TextPrimary };
        _swAutostart = new ToggleSwitch(Autostart.IsEnabled(), new Point(96, 44));

        var lblStart = new Label { Text = "启动时开启映射", Location = new Point(210, 46), Size = new Size(112, 24), ForeColor = UiTheme.TextPrimary };
        _swStartEnabled = new ToggleSwitch(_working.StartEnabled, new Point(326, 44));

        var lblVib = new Label { Text = "开关时震动反馈", Location = new Point(430, 46), Size = new Size(112, 24), ForeColor = UiTheme.TextPrimary };
        _swVibrate = new ToggleSwitch(_working.VibrateOnToggle, new Point(546, 44));

        card.Controls.AddRange([lblAuto, _swAutostart, lblStart, _swStartEnabled, lblVib, _swVibrate]);
        Controls.Add(card);
    }

    // ---------------- 底部按钮 ----------------

    private void BuildFooter()
    {
        var btnDefaults = new ModernButton("恢复默认", new Point(16, 760), new Size(96, 34));
        btnDefaults.Click += (_, _) => ResetDefaults();
        var btnCancel = new ModernButton("取消", new Point(446, 760), new Size(90, 34));
        btnCancel.Click += (_, _) => Close();
        var btnOk = new ModernButton("保存并应用", new Point(544, 760), new Size(104, 34), ModernButton.Style.Primary);
        btnOk.Click += (_, _) => SaveAndClose();
        Controls.AddRange([btnDefaults, btnCancel, btnOk]);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

    }

    // ---------------- 手柄实时状态 / 组合键录制 ----------------

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

    private void CaptureChord()
    {
        using var dlg = new Form
        {
            Text = "录制组合键",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(420, 190),
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = UiTheme.FontUi,
            BackColor = UiTheme.WindowBg,
        };

        var card = new Card("录制组合键", new Point(12, 12), new Size(396, 120));
        var hint = new Label
        {
            Text = "请同时按住想使用的按键，然后点击「使用当前按键」。",
            Location = new Point(24, 42),
            Size = new Size(350, 20),
            ForeColor = UiTheme.TextSecondary,
            Font = UiTheme.FontSmall,
        };
        var live = new FlowLayoutPanel
        {
            Location = new Point(24, 68),
            Size = new Size(350, 40),
            BackColor = UiTheme.CardBg,
        };

        var t = new System.Windows.Forms.Timer { Interval = 60 };
        HashSet<string> current = new();
        t.Tick += (_, _) =>
        {
            var held = CurrentHeld();
            if (held.Count == current.Count && held.SetEquals(current)) return;
            current = held;
            foreach (Control old in live.Controls) old.Dispose();
            live.Controls.Clear();
            if (current.Count == 0)
            {
                live.Controls.Add(new Label
                {
                    Text = "当前按住：（无）",
                    AutoSize = true,
                    ForeColor = UiTheme.TextSecondary,
                    BackColor = UiTheme.CardBg,
                });
            }
            else
            {
                int i = 0;
                foreach (var b in current)
                {
                    if (i++ > 0) live.Controls.Add(new Label { Text = "+", AutoSize = true, ForeColor = UiTheme.TextSecondary, BackColor = UiTheme.CardBg, Margin = new Padding(4, 7, 4, 0) });
                    live.Controls.Add(new Chip(b) { Margin = new Padding(2, 2, 2, 0) });
                }
            }
        };
        t.Start();

        var btnOk = new ModernButton("使用当前按键", new Point(186, 142), new Size(120, 34), ModernButton.Style.Primary);
        var btnCancel = new ModernButton("取消", new Point(314, 142), new Size(80, 34));
        btnOk.Click += (_, _) => { dlg.Tag = true; dlg.Close(); };
        btnCancel.Click += (_, _) => { dlg.Tag = false; dlg.Close(); };

        dlg.Controls.AddRange([card, hint, live, btnOk, btnCancel]);
        dlg.FormClosed += (_, _) => { t.Stop(); t.Dispose(); };
        dlg.ShowDialog(this);

        if (dlg.Tag is true)
        {
            if (current.Count > 0)
            {
                _working.ToggleChord = [.. current];
                RebuildChips();
            }
            else
            {
                MessageBox.Show(this, "没有检测到按下的按键。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
        _working.StartEnabled = def.StartEnabled;

        RebuildChips();
        _segMove.SetSelected(1);
        _segScroll.SetSelected(0);
        _sldSens.Value = _working.Sensitivity;
        _sldScroll.Value = _working.ScrollSensitivity;
        _sldDeadzone.Value = _working.Deadzone;
        _sldCurve.Value = _working.Curve;
        _swStartEnabled.SetChecked(true);
        _swVibrate.SetChecked(true);

        for (int i = 0; i < 8; i++)
        {
            string b1 = MappingConfig.AllButtons[i];
            string b2 = MappingConfig.AllButtons[i + 8];
            _grid.Rows[i].Cells[1].Value = MappingConfig.ActionDisplay[def.ButtonMappings[b1]];
            _grid.Rows[i].Cells[1].Tag = def.ButtonMappings[b1];
            _grid.Rows[i].Cells[3].Value = MappingConfig.ActionDisplay[def.ButtonMappings[b2]];
            _grid.Rows[i].Cells[3].Tag = def.ButtonMappings[b2];
        }
    }

    private void SaveAndClose()
    {
        _working.MoveStick = _segMove.Selected == 0 ? "Left" : "Right";
        _working.ScrollStick = _segScroll.Selected switch { 0 => "Left", 1 => "Right", _ => "None" };
        _working.Sensitivity = _sldSens.Value;
        _working.ScrollSensitivity = _sldScroll.Value;
        _working.Deadzone = _sldDeadzone.Value;
        _working.Curve = _sldCurve.Value;
        _working.StartEnabled = _swStartEnabled.Checked;
        _working.VibrateOnToggle = _swVibrate.Checked;
        _working.Normalize();

        _mapper.ApplyConfig(_working);

        bool wantAuto = _swAutostart.Checked;
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
