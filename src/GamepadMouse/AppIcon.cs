using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GamepadMouse;

/// <summary>
/// 程序图标：assets/app.ico（由 assets/app-icon.svg 生成）内嵌于程序集。
/// Create(enabled) 输出运行时状态版本：开启 = 原始彩色，关闭 = 灰阶。
/// </summary>
internal static class AppIcon
{
    private static Icon? _on;
    private static Icon? _off;
    private static byte[]? _icoBytes;

    public static Icon Enabled => _on ??= Create(true);
    public static Icon Disabled => _off ??= Create(false);

    private static byte[] EmbeddedIco()
    {
        if (_icoBytes != null) return _icoBytes;
        using var s = typeof(AppIcon).Assembly.GetManifestResourceStream("GamepadMouse.app.ico")
            ?? throw new InvalidOperationException("内嵌图标资源缺失（GamepadMouse.app.ico）");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return _icoBytes = ms.ToArray();
    }

    public static Icon Create(bool enabled)
    {
        const int size = 64;

        // 取最大帧解码，再高质量缩放
        Icon large;
        using (var ms = new MemoryStream(EmbeddedIco()))
            large = new Icon(ms, 256, 256);
        using var src = large.ToBitmap();

        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (enabled)
            {
                g.DrawImage(src, new Rectangle(0, 0, size, size));
            }
            else
            {
                // 关闭映射：去饱和 + 降低不透明度
                var attr = new ImageAttributes();
                attr.SetColorMatrix(new ColorMatrix
                {
                    Matrix00 = 0.33f, Matrix01 = 0.33f, Matrix02 = 0.33f,
                    Matrix10 = 0.59f, Matrix11 = 0.59f, Matrix12 = 0.59f,
                    Matrix20 = 0.11f, Matrix21 = 0.11f, Matrix22 = 0.11f,
                    Matrix33 = 0.75f, // alpha
                });
                g.DrawImage(src, new Rectangle(0, 0, size, size),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            }
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
