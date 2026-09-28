// IcoGen：把一张 256px PNG 转成多尺寸 .ico（16/24/32/48/64/128/256）。
// 用法：dotnet run --project tools/IcoGen -- <source.png> <output.ico>
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

var srcPath = args.Length > 0 ? args[0] : "assets/_icon256.png";
var outPath = args.Length > 1 ? args[1] : "assets/app.ico";
int[] sizes = [16, 24, 32, 48, 64, 128, 256];

using var src = new Bitmap(srcPath);
using var ms = new MemoryStream();
using (var bw = new BinaryWriter(ms))
{
    bw.Write((ushort)0);          // reserved
    bw.Write((ushort)1);          // type: icon
    bw.Write((ushort)sizes.Length);

    // 先编码每个尺寸，拿到长度后才能写目录
    var entries = new List<byte[]>();
    foreach (var size in sizes)
        entries.Add(EncodeEntry(src, size));

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        bw.Write((byte)(s >= 256 ? 0 : s)); // width
        bw.Write((byte)(s >= 256 ? 0 : s)); // height
        bw.Write((byte)0);                  // palette
        bw.Write((byte)0);                  // reserved
        bw.Write((ushort)1);                // planes
        bw.Write((ushort)32);               // bpp
        bw.Write((uint)entries[i].Length);
        bw.Write((uint)offset);
        offset += entries[i].Length;
    }
    foreach (var e in entries) bw.Write(e);
}

File.WriteAllBytes(outPath, ms.ToArray());
Console.WriteLine($"written {outPath} ({ms.ToArray().Length} bytes, {sizes.Length} sizes)");

// 单尺寸条目：BITMAPINFOHEADER + 预乘alpha的BGRA像素(自下而上) + 全零 AND 掩码
static byte[] EncodeEntry(Bitmap src, int size)
{
    using var bmp = new Bitmap(size, size);
    using (var g = Graphics.FromImage(bmp))
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(src, new Rectangle(0, 0, size, size));
    }

    var rect = new Rectangle(0, 0, size, size);
    var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    int stride = Math.Abs(data.Stride);
    var topDown = new byte[size * size * 4];
    for (int y = 0; y < size; y++)
        Marshal.Copy(data.Scan0 + y * data.Stride, topDown, y * size * 4, size * 4);
    bmp.UnlockBits(data);

    // 翻转成自下而上 + 预乘 alpha
    var pixels = new byte[size * size * 4];
    for (int y = 0; y < size; y++)
    {
        int srcRow = y * size * 4, dstRow = (size - 1 - y) * size * 4;
        for (int x = 0; x < size; x++)
        {
            int si = srcRow + x * 4, di = dstRow + x * 4;
            byte b = topDown[si], g2 = topDown[si + 1], r = topDown[si + 2], a = topDown[si + 3];
            pixels[di] = (byte)(b * a / 255);
            pixels[di + 1] = (byte)(g2 * a / 255);
            pixels[di + 2] = (byte)(r * a / 255);
            pixels[di + 3] = a;
        }
    }

    int andStride = (size + 31) / 32 * 4;
    var andMask = new byte[andStride * size]; // 全零：透明由 alpha 通道表达

    var ms2 = new MemoryStream();
    using (var bw2 = new BinaryWriter(ms2))
    {
        bw2.Write(40);                       // biSize
        bw2.Write(size);                     // biWidth
        bw2.Write(size * 2);                 // biHeight (XOR+AND)
        bw2.Write((short)1);                 // biPlanes
        bw2.Write((short)32);                // biBitCount
        bw2.Write(0);                        // biCompression = BI_RGB
        bw2.Write(pixels.Length + andMask.Length);
        bw2.Write(0); bw2.Write(0); bw2.Write(0); bw2.Write(0);
        bw2.Write(pixels);
        bw2.Write(andMask);
    }
    return ms2.ToArray();
}
