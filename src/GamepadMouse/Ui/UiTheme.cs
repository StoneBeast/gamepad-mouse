using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace GamepadMouse.Ui;

/// <summary>一套完整的界面配色。深色 / 浅色两套，由 UiTheme.Current 决定当前生效。</summary>
internal sealed class Palette
{
    public bool IsDark { get; }

    // 基础
    public Color WindowBg { get; private set; }        // 窗口底
    public Color CardBg { get; private set; }          // 卡片底
    public Color CardBgHover { get; private set; }     // 卡片内悬停
    public Color CardBorder { get; private set; }      // 卡片描边
    public Color TextPrimary { get; private set; }
    public Color TextSecondary { get; private set; }

    // 主题色
    public Color Accent { get; private set; }
    public Color AccentHover { get; private set; }
    public Color AccentPressed { get; private set; }
    public int AccentAlpha { get; private set; }       // 半透明主题色底的不透明度

    // 控件
    public Color TrackOff { get; private set; }        // 开关关闭
    public Color SliderTrack { get; private set; }
    public Color Success { get; private set; }
    public Color Danger { get; private set; }

    // 表格
    public Color HeaderBg { get; private set; }        // 表头
    public Color GridLine { get; private set; }        // 行分隔线
    public Color RowAlt { get; private set; }          // 交替行
    public Color RowSelect { get; private set; }       // 选中行

    private static Color From(int r, int g, int b) => Color.FromArgb(r, g, b);

    private Palette(bool dark)
    {
        IsDark = dark;
        if (dark)
        {
            WindowBg = From(23, 25, 31);
            CardBg = From(32, 35, 43);
            CardBgHover = From(42, 46, 57);
            CardBorder = From(52, 57, 70);
            TextPrimary = From(235, 237, 243);
            TextSecondary = From(150, 157, 173);
            Accent = From(108, 123, 255);
            AccentHover = From(130, 144, 255);
            AccentPressed = From(88, 101, 228);
            AccentAlpha = 26;
            TrackOff = From(64, 69, 82);
            SliderTrack = From(56, 61, 74);
            Success = From(74, 190, 138);
            Danger = From(230, 109, 109);
            HeaderBg = From(38, 42, 51);
            GridLine = From(43, 47, 57);
            RowAlt = From(35, 38, 46);
            RowSelect = From(44, 49, 64);
        }
        else
        {
            WindowBg = From(242, 243, 247);
            CardBg = From(255, 255, 255);
            CardBgHover = From(244, 245, 250);
            CardBorder = From(226, 228, 236);
            TextPrimary = From(33, 36, 51);
            TextSecondary = From(122, 129, 148);
            Accent = From(91, 108, 250);
            AccentHover = From(115, 132, 255);
            AccentPressed = From(76, 91, 224);
            AccentAlpha = 18;
            TrackOff = From(215, 218, 227);
            SliderTrack = From(228, 231, 238);
            Success = From(46, 158, 107);
            Danger = From(217, 83, 79);
            HeaderBg = From(246, 247, 251);
            GridLine = From(236, 238, 244);
            RowAlt = From(248, 249, 252);
            RowSelect = From(233, 236, 255);
        }
    }

    public static readonly Palette Dark = new(true);
    public static readonly Palette Light = new(false);
}

/// <summary>深色主题配色 / 字体 / 圆角绘制工具。颜色均代理到 UiTheme.Current。</summary>
internal static class UiTheme
{
    public static Palette Current { get; private set; } = Palette.Dark;

    /// <summary>主题切换后触发（UI 线程调用 Apply 时）。</summary>
    public static event Action? ThemeChanged;

    public static bool IsDark => Current.IsDark;

    /// <summary>切换当前主题（应在 UI 线程调用；调用方负责刷新缓存了颜色的控件）。</summary>
    public static void Apply(Palette palette)
    {
        if (Current == palette) return;
        Current = palette;
        ThemeChanged?.Invoke();
    }

    // ---- 代理到当前主题 ----
    public static Color WindowBg => Current.WindowBg;
    public static Color CardBg => Current.CardBg;
    public static Color CardBgHover => Current.CardBgHover;
    public static Color CardBorder => Current.CardBorder;
    public static Color TextPrimary => Current.TextPrimary;
    public static Color TextSecondary => Current.TextSecondary;
    public static Color Accent => Current.Accent;
    public static Color AccentHover => Current.AccentHover;
    public static Color AccentPressed => Current.AccentPressed;
    public static Color AccentDim => Color.FromArgb(Current.AccentAlpha, Current.Accent);
    public static Color TrackOff => Current.TrackOff;
    public static Color SliderTrack => Current.SliderTrack;
    public static Color Success => Current.Success;
    public static Color Danger => Current.Danger;
    public static Color HeaderBg => Current.HeaderBg;
    public static Color GridLine => Current.GridLine;
    public static Color RowAlt => Current.RowAlt;
    public static Color RowSelect => Current.RowSelect;

    // ---- 字体（两套主题共用）----
    public static readonly Font FontUi = new("Microsoft YaHei UI", 9f);
    public static readonly Font FontUiBold = new("Microsoft YaHei UI", 9f, FontStyle.Bold);
    public static readonly Font FontSmall = new("Microsoft YaHei UI", 8.25f);
    public static readonly Font FontTitle = new("Microsoft YaHei UI", 14f, FontStyle.Bold);
    public static readonly Font FontTitleSub = new("Microsoft YaHei UI", 8.5f);
    public static readonly Font FontValue = new("Consolas", 9.5f, FontStyle.Bold);

    /// <summary>圆角矩形路径。</summary>
    public static GraphicsPath Round(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        int d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(a.A + (b.A - a.A) * t),
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    // ---- 深色/浅色标题栏（DWM）----

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>让窗口标题栏跟随主题（Win10 20H1+/Win11；旧系统静默失败）。</summary>
    public static void ApplyTitleBarTheme(Form form)
    {
        if (!form.IsHandleCreated) return;
        int on = IsDark ? 1 : 0;
        try { DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)); } catch { /* 旧系统 */ }
        try { DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int)); } catch { /* 旧系统 */ }
    }
}

/// <summary>自绘控件基类：开启抗锯齿双缓冲、禁用 Tab 焦点。</summary>
internal class UiControl : Control
{
    public UiControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }
}

/// <summary>自绘面板基类。</summary>
internal class UiPanel : Panel
{
    public UiPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }
}
