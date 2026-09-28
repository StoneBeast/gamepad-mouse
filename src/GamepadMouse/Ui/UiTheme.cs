using System.Drawing.Drawing2D;

namespace GamepadMouse.Ui;

/// <summary>深色主题配色 / 字体 / 圆角绘制工具。</summary>
internal static class UiTheme
{
    // ---- 配色 ----
    public static readonly Color WindowBg = Color.FromArgb(23, 25, 31);      // 窗口底
    public static readonly Color CardBg = Color.FromArgb(32, 35, 43);        // 卡片底
    public static readonly Color CardBgHover = Color.FromArgb(42, 46, 57);   // 卡片内悬停
    public static readonly Color CardBorder = Color.FromArgb(52, 57, 70);    // 卡片描边
    public static readonly Color TextPrimary = Color.FromArgb(235, 237, 243);
    public static readonly Color TextSecondary = Color.FromArgb(150, 157, 173);
    public static readonly Color Accent = Color.FromArgb(108, 123, 255);     // 主题色
    public static readonly Color AccentHover = Color.FromArgb(130, 144, 255);
    public static readonly Color AccentPressed = Color.FromArgb(88, 101, 228);
    public static readonly Color AccentDim = Color.FromArgb(26, Accent);     // 半透明主题色底
    public static readonly Color TrackOff = Color.FromArgb(64, 69, 82);      // 开关关闭
    public static readonly Color SliderTrack = Color.FromArgb(56, 61, 74);
    public static readonly Color Success = Color.FromArgb(74, 190, 138);
    public static readonly Color Danger = Color.FromArgb(230, 109, 109);

    // ---- 字体 ----
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
