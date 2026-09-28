using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamepadMouse;

/// <summary>按键 → 鼠标动作的映射配置（JSON 持久化）。</summary>
public class MappingConfig
{
    // ---- 手柄按键名称 ----
    public static readonly string[] AllButtons =
    [
        "A", "B", "X", "Y", "LB", "RB", "LT", "RT",
        "Back", "Start", "LSB", "RSB",
        "DPadUp", "DPadDown", "DPadLeft", "DPadRight"
    ];

    // ---- 动作名称（存储用英文，界面显示中文）----
    public const string ActNone = "None";
    public const string ActLeftClick = "LeftClick";
    public const string ActRightClick = "RightClick";
    public const string ActMiddleClick = "MiddleClick";
    public const string ActWheelUp = "WheelUp";
    public const string ActWheelDown = "WheelDown";
    public const string ActWheelLeft = "WheelLeft";
    public const string ActWheelRight = "WheelRight";
    public const string ActDoubleClick = "DoubleLeftClick";
    public const string ActToggle = "ToggleMapping";

    public static readonly string[] AllActions =
    [
        ActNone, ActLeftClick, ActRightClick, ActMiddleClick,
        ActWheelUp, ActWheelDown, ActWheelLeft, ActWheelRight,
        ActDoubleClick, ActToggle
    ];

    /// <summary>动作英文名 → 中文显示名。</summary>
    public static readonly Dictionary<string, string> ActionDisplay = new()
    {
        [ActNone] = "（无）",
        [ActLeftClick] = "鼠标左键",
        [ActRightClick] = "鼠标右键",
        [ActMiddleClick] = "鼠标中键",
        [ActWheelUp] = "滚轮向上",
        [ActWheelDown] = "滚轮向下",
        [ActWheelLeft] = "滚轮向左",
        [ActWheelRight] = "滚轮向右",
        [ActDoubleClick] = "左键双击",
        [ActToggle] = "★ 开关映射",
    };

    /// <summary>开关映射的组合键（手柄按键名列表，需同时按住）。</summary>
    public List<string> ToggleChord { get; set; } = ["Back", "Start"];

    /// <summary>控制光标移动的摇杆：Left / Right。</summary>
    public string MoveStick { get; set; } = "Right";

    /// <summary>控制滚轮滚动/平移的摇杆：Left / Right / None。</summary>
    public string ScrollStick { get; set; } = "Left";

    /// <summary>光标满偏移速度（像素/秒）。</summary>
    public double Sensitivity { get; set; } = 2500;

    /// <summary>滚轮滚动速度（格/秒）。</summary>
    public double ScrollSensitivity { get; set; } = 6;

    /// <summary>摇杆死区（0~0.5）。</summary>
    public double Deadzone { get; set; } = 0.18;

    /// <summary>响应曲线指数（1=线性，越大越精细）。</summary>
    public double Curve { get; set; } = 1.6;

    /// <summary>手柄轮询间隔（毫秒）。</summary>
    public int PollRateMs { get; set; } = 8;

    /// <summary>扳机视为按下的阈值（0~255）。</summary>
    public int TriggerThreshold { get; set; } = 64;

    /// <summary>启动时自动开启映射。</summary>
    public bool StartEnabled { get; set; } = true;

    /// <summary>开关映射时手柄震动反馈。</summary>
    public bool VibrateOnToggle { get; set; } = true;

    /// <summary>按键映射表：手柄按键名 → 动作名。</summary>
    public Dictionary<string, string> ButtonMappings { get; set; } = DefaultMappings();

    public static Dictionary<string, string> DefaultMappings() => new()
    {
        ["A"] = ActLeftClick,
        ["B"] = ActRightClick,
        ["X"] = ActMiddleClick,
        ["Y"] = ActDoubleClick,
        ["LB"] = ActWheelUp,
        ["RB"] = ActWheelDown,
        ["LT"] = ActNone,
        ["RT"] = ActNone,
        ["Back"] = ActNone,
        ["Start"] = ActNone,
        ["LSB"] = ActNone,
        ["RSB"] = ActNone,
        ["DPadUp"] = ActWheelUp,
        ["DPadDown"] = ActWheelDown,
        ["DPadLeft"] = ActWheelLeft,
        ["DPadRight"] = ActWheelRight,
    };

    [JsonIgnore]
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamepadMouse");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static MappingConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = JsonSerializer.Deserialize<MappingConfig>(File.ReadAllText(ConfigPath), JsonOpts);
                if (cfg != null)
                {
                    cfg.Normalize();
                    Log.Info($"已加载配置 {ConfigPath}");
                    return cfg;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("配置加载失败，使用默认配置", ex);
            try { File.Copy(ConfigPath, ConfigPath + ".bad", overwrite: true); } catch { /* 忽略 */ }
        }

        var def = new MappingConfig();
        def.Save(); // 首次运行生成默认配置
        return def;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
            Log.Info($"配置已保存 {ConfigPath}");
        }
        catch (Exception ex)
        {
            Log.Error("配置保存失败", ex);
        }
    }

    /// <summary>修正无效值（编辑 config.json 时的兜底）。</summary>
    public void Normalize()
    {
        if (MoveStick != "Left" && MoveStick != "Right") MoveStick = "Right";
        if (ScrollStick != "Left" && ScrollStick != "Right") ScrollStick = "None";
        ToggleChord = [.. ToggleChord.Where(b => AllButtons.Contains(b)).Distinct()];
        Sensitivity = Math.Clamp(Sensitivity, 200, 20000);
        ScrollSensitivity = Math.Clamp(ScrollSensitivity, 0.5, 30);
        Deadzone = Math.Clamp(Deadzone, 0, 0.5);
        Curve = Math.Clamp(Curve, 1, 3);
        PollRateMs = Math.Clamp(PollRateMs, 4, 50);
        TriggerThreshold = Math.Clamp(TriggerThreshold, 1, 255);

        var clean = new Dictionary<string, string>();
        foreach (var b in AllButtons)
        {
            if (ButtonMappings.TryGetValue(b, out var act) && AllActions.Contains(act))
                clean[b] = act;
            else
                clean[b] = ActNone;
        }
        ButtonMappings = clean;
    }
}
