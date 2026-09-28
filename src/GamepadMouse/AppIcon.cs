using System.Drawing.Drawing2D;

namespace GamepadMouse;

/// <summary>程序内生成手柄图标：映射开启为绿色状态点，关闭为灰色，主体随状态换色。</summary>
internal static class AppIcon
{
    private static Icon? _on;
    private static Icon? _off;

    public static Icon Enabled => _on ??= Create(true);
    public static Icon Disabled => _off ??= Create(false);

    public static Icon Create(bool enabled)
    {
        using var bmp = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 2x 绘制再由 GetHicon 缩放，减少锯齿：直接以 64x64 画
            float s = 2f;
            g.ScaleTransform(s, s);

            var body = enabled ? Ui.AccentColor : Color.FromArgb(94, 100, 112);
            using var bodyBrush = new SolidBrush(body);
            using var cutBrush = new SolidBrush(Ui.WindowBg);
            using var dotBrush = new SolidBrush(enabled ? Ui.SuccessColor : Color.FromArgb(120, 126, 138));

            // 手柄主体：中间横条 + 左右握把
            g.FillRectangle(bodyBrush, 10, 22, 44, 22);
            g.FillEllipse(bodyBrush, 3, 24, 24, 24);
            g.FillEllipse(bodyBrush, 37, 24, 24, 24);

            // 左侧十字键
            g.FillRectangle(cutBrush, 14, 28, 6, 14);
            g.FillRectangle(cutBrush, 10, 32, 14, 6);

            // 右侧四键（菱形排列）
            void Dot(float cx, float cy)
            {
                g.FillEllipse(cutBrush, cx - 3f, cy - 3f, 6f, 6f);
            }
            Dot(49, 26);
            Dot(55, 32);
            Dot(49, 38);
            Dot(43, 32);

            // 状态点（右下角）
            g.FillEllipse(new SolidBrush(Ui.WindowBg), 46, 44, 17, 17);
            g.FillEllipse(dotBrush, 48, 46, 13, 13);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>避免依赖 Ui 命名空间内部类型的封装。</summary>
    private static class Ui
    {
        public static readonly Color WindowBg = Color.FromArgb(23, 25, 31);
        public static readonly Color AccentColor = Color.FromArgb(108, 123, 255);
        public static readonly Color SuccessColor = Color.FromArgb(74, 190, 138);
    }
}
