using System.Diagnostics;

namespace GamepadMouse;

/// <summary>
/// 核心映射引擎：后台线程轮询手柄，将摇杆/按键转换为鼠标动作。
/// 组合键（ToggleChord）在任何状态下都生效，用于开关映射。
/// </summary>
internal class Mapper : IDisposable
{
    private MappingConfig _config;
    private Thread? _thread;
    private volatile bool _stop;
    private readonly object _configLock = new();

    private bool _enabled;
    private bool _chordLatch;           // 组合键防重复触发
    private HashSet<string> _prevHeld = new(); // 上一帧按下的按键（含扳机），用于边沿检测
    private double _moveAccX, _moveAccY; // 亚像素累计
    private double _wheelAcc;            // 滚轮增量累计（单位：1/120 格）
    private double _hWheelAcc;
    private readonly HashSet<string> _heldMouseActions = new(); // 按住中的点击类动作，防止失联时卡键
    private readonly Stopwatch _clock = new();

    /// <summary>映射开关变化（UI 线程自行调度）。</summary>
    public event Action<bool>? EnabledChanged;

    /// <summary>状态文本变化（手柄连接/断开等）。</summary>
    public event Action<string>? StatusChanged;

    public bool Enabled => _enabled;

    public Mapper(MappingConfig config)
    {
        _config = config;
        _enabled = config.StartEnabled;
    }

    public MappingConfig Config { get { lock (_configLock) return _config; } }

    /// <summary>设置界面保存后调用，热更新配置。</summary>
    public void ApplyConfig(MappingConfig newConfig)
    {
        lock (_configLock)
        {
            bool wasEnabled = _enabled;
            // 配置变化后释放所有按住的键，避免旧映射卡键
            ReleaseAllHeld();
            _config = newConfig;
            if (!wasEnabled && _enabled && !newConfig.StartEnabled)
                _enabled = wasEnabled; // 热更新不改变当前开关状态
        }
        newConfig.Save();
    }

    public void SetEnabled(bool on)
    {
        if (_enabled == on) return;
        _enabled = on;
        if (!on) ReleaseAllHeld();
        Log.Info($"映射{(on ? "已开启" : "已关闭")}");
        EnabledChanged?.Invoke(on);
        if (_enabled && Config.VibrateOnToggle) VibrateToggle(on: true);
        else if (!on && Config.VibrateOnToggle) VibrateToggle(on: false);
    }

    public void Toggle() => SetEnabled(!_enabled);

    /// <summary>开关映射震动反馈：开启短震，关闭长震。</summary>
    private void VibrateToggle(bool on)
    {
        try
        {
            XInput.SetVibration(0, 32000, 32000);
            var t = new Thread(() =>
            {
                try
                {
                    Thread.Sleep(on ? 200 : 800);
                    XInput.SetVibration(0, 0, 0);
                }
                catch { /* 震动失败忽略 */ }
            }) { IsBackground = true };
            t.Start();
        }
        catch { /* 震动失败忽略 */ }
    }

    public void Start()
    {
        if (_thread != null) return;
        _stop = false;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "GamepadMouse-Poll",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        try { _thread?.Join(500); } catch { /* 忽略 */ }
        _thread = null;
        ReleaseAllHeld();
    }

    // ---------------- 主循环 ----------------

    private void PollLoop()
    {
        if (!XInput.Available)
        {
            StatusChanged?.Invoke("未找到 XInput 驱动（仅支持 XInput 手柄，需将手柄切换为 XInput 模式）");
            return;
        }

        _clock.Start();
        var state = new XInput.State();
        bool lastConnected = false;

        while (!_stop)
        {
            var cfg = Config;
            int pollMs = cfg.PollRateMs;
            bool connected = XInput.GetState(0, ref state);

            if (!connected)
            {
                if (lastConnected)
                {
                    ReleaseAllHeld();
                    StatusChanged?.Invoke("手柄未连接（等待中…）");
                    Log.Warn("手柄断开连接");
                }
                lastConnected = false;
                Thread.Sleep(300);
                _clock.Restart();
                continue;
            }

            if (!lastConnected)
            {
                lastConnected = true;
                StatusChanged?.Invoke(null!); // null 表示恢复正常，由 UI 显示默认状态
                Log.Info("手柄已连接");
            }

            double dt = _clock.Elapsed.TotalSeconds;
            _clock.Restart();

            try
            {
                ProcessFrame(cfg, state.Gamepad, dt);
            }
            catch (Exception ex)
            {
                Log.Error("处理帧异常", ex);
            }

            // 保持轮询节奏
            int spent = (int)(_clock.Elapsed.TotalMilliseconds);
            Thread.Sleep(Math.Max(1, pollMs - spent));
        }
    }

    private void ProcessFrame(MappingConfig cfg, XInput.Gamepad g, double dt)
    {
        var held = HeldButtons(cfg, g);

        // ---- 1. 组合键开关（任何状态下都检测，边沿触发）----
        if (cfg.ToggleChord.Count > 0 && cfg.ToggleChord.All(held.Contains))
        {
            if (!_chordLatch)
            {
                _chordLatch = true;
                Toggle();
            }
        }
        else
        {
            _chordLatch = false;
        }

        // ---- 2. 映射关闭时只更新边沿基准 ----
        if (!_enabled)
        {
            _prevHeld = held;
            return;
        }

        // ---- 3. 摇杆 → 光标移动 ----
        (short mx, short my) = cfg.MoveStick == "Left"
            ? (g.sThumbLX, g.sThumbLY)
            : (g.sThumbRX, g.sThumbRY);

        double dx = ShapeAxis(mx, cfg.Deadzone, cfg.Curve) * cfg.Sensitivity * dt;
        double dy = ShapeAxis(my, cfg.Deadzone, cfg.Curve) * cfg.Sensitivity * dt;

        _moveAccX += dx;
        _moveAccY -= dy; // 手柄 Y 轴向上为正，屏幕 Y 轴向下为正
        int ix = (int)_moveAccX;
        int iy = (int)_moveAccY;
        if (ix != 0) { _moveAccX -= ix; }
        if (iy != 0) { _moveAccY -= iy; }
        if (ix != 0 || iy != 0)
            MouseSimulator.MoveCursor(ix, iy);

        // ---- 4. 另一个摇杆 → 滚轮 / 水平滚动 ----
        // 摇杆经死区+响应曲线整形（与光标一致）：轻推输出小 → 慢滚，重推输出大 → 快滚。
        // 平滑模式：按任意增量连续发送（支持的程序获得细腻的连续滚动）；
        // 整格模式：攒满一格（120）才发送，兼容只认整格增量的旧程序。
        if (cfg.ScrollStick != "None")
        {
            (short sx, short sy) = cfg.ScrollStick == "Left"
                ? (g.sThumbLX, g.sThumbLY)
                : (g.sThumbRX, g.sThumbRY);

            double vy = ShapeAxis(sy, cfg.Deadzone, cfg.Curve) * cfg.ScrollSensitivity * dt;
            double vx = ShapeAxis(sx, cfg.Deadzone, cfg.Curve) * cfg.ScrollSensitivity * dt;

            _wheelAcc += vy * 120.0;  // 累计单位：滚轮增量（一格 = 120）
            _hWheelAcc += vx * 120.0;

            int wy, wx;
            if (cfg.SmoothWheel)
            {
                wy = (int)_wheelAcc;  // 任意大小增量（可为 1~119），轻推也能连续慢滚
                wx = (int)_hWheelAcc;
                if (wy != 0) { _wheelAcc -= wy; MouseSimulator.Wheel(wy); }
                if (wx != 0) { _hWheelAcc -= wx; MouseSimulator.HWheel(wx); }
            }
            else
            {
                wy = (int)(_wheelAcc / 120);
                wx = (int)(_hWheelAcc / 120);
                if (wy != 0) { _wheelAcc -= wy * 120; MouseSimulator.Wheel(wy * 120); }
                if (wx != 0) { _hWheelAcc -= wx * 120; MouseSimulator.HWheel(wx * 120); }
            }
        }

        // ---- 5. 按键映射（边沿触发）----
        foreach (var (button, action) in cfg.ButtonMappings)
        {
            if (action == MappingConfig.ActNone) continue;
            bool now = held.Contains(button);
            bool prev = _prevHeld.Contains(button);
            if (now && !prev) OnPressed(action);
            else if (!now && prev) OnReleased(action);
        }

        _prevHeld = held;
    }

    /// <summary>当前按下的按键集合（含扳机阈值判定）。</summary>
    private static HashSet<string> HeldButtons(MappingConfig cfg, XInput.Gamepad g)
    {
        var set = new HashSet<string>();
        ushort w = g.wButtons;
        if ((w & XInput.A) != 0) set.Add("A");
        if ((w & XInput.B) != 0) set.Add("B");
        if ((w & XInput.X) != 0) set.Add("X");
        if ((w & XInput.Y) != 0) set.Add("Y");
        if ((w & XInput.LeftShoulder) != 0) set.Add("LB");
        if ((w & XInput.RightShoulder) != 0) set.Add("RB");
        if (g.bLeftTrigger >= cfg.TriggerThreshold) set.Add("LT");
        if (g.bRightTrigger >= cfg.TriggerThreshold) set.Add("RT");
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

    private void OnPressed(string action)
    {
        switch (action)
        {
            case MappingConfig.ActLeftClick:
                MouseSimulator.LeftDown();
                _heldMouseActions.Add(MappingConfig.ActLeftClick);
                break;
            case MappingConfig.ActRightClick:
                MouseSimulator.RightDown();
                _heldMouseActions.Add(MappingConfig.ActRightClick);
                break;
            case MappingConfig.ActMiddleClick:
                MouseSimulator.MiddleDown();
                _heldMouseActions.Add(MappingConfig.ActMiddleClick);
                break;
            case MappingConfig.ActWheelUp: MouseSimulator.Wheel(+120); break;
            case MappingConfig.ActWheelDown: MouseSimulator.Wheel(-120); break;
            case MappingConfig.ActWheelLeft: MouseSimulator.HWheel(-120); break;
            case MappingConfig.ActWheelRight: MouseSimulator.HWheel(+120); break;
            case MappingConfig.ActDoubleClick: MouseSimulator.DoubleLeftClick(); break;
            case MappingConfig.ActToggle: Toggle(); break;
        }
    }

    private void OnReleased(string action)
    {
        switch (action)
        {
            case MappingConfig.ActLeftClick:
                MouseSimulator.LeftUp();
                _heldMouseActions.Remove(MappingConfig.ActLeftClick);
                break;
            case MappingConfig.ActRightClick:
                MouseSimulator.RightUp();
                _heldMouseActions.Remove(MappingConfig.ActRightClick);
                break;
            case MappingConfig.ActMiddleClick:
                MouseSimulator.MiddleUp();
                _heldMouseActions.Remove(MappingConfig.ActMiddleClick);
                break;
        }
    }

    /// <summary>释放所有因映射而按下的鼠标键（停用/断开/退出时调用）。</summary>
    private void ReleaseAllHeld()
    {
        foreach (var a in _heldMouseActions.ToArray())
        {
            switch (a)
            {
                case MappingConfig.ActLeftClick: MouseSimulator.LeftUp(); break;
                case MappingConfig.ActRightClick: MouseSimulator.RightUp(); break;
                case MappingConfig.ActMiddleClick: MouseSimulator.MiddleUp(); break;
            }
        }
        _heldMouseActions.Clear();
    }

    /// <summary>摇杆输出整形：死区 + 响应曲线，返回 -1~1。</summary>
    private static double ShapeAxis(short value, double deadzone, double curve)
    {
        double n = value >= 0 ? value / 32767.0 : value / 32768.0;
        double mag = Math.Abs(n);
        if (mag <= deadzone) return 0;
        double t = (mag - deadzone) / (1.0 - deadzone);
        t = Math.Pow(t, curve);
        return Math.Sign(n) * t;
    }
}
