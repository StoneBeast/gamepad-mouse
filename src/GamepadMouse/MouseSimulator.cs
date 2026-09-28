using System.Runtime.InteropServices;

namespace GamepadMouse;

/// <summary>
/// 通过 SendInput / SetCursorPos 模拟鼠标操作。
/// </summary>
internal static class MouseSimulator
{
    private const uint MouseEventfMove = 0x0001;
    private const uint MouseEventfLeftDown = 0x0002;
    private const uint MouseEventfLeftUp = 0x0004;
    private const uint MouseEventfRightDown = 0x0008;
    private const uint MouseEventfRightUp = 0x0010;
    private const uint MouseEventfMiddleDown = 0x0020;
    private const uint MouseEventfMiddleUp = 0x0040;
    private const uint MouseEventfWheel = 0x0800;
    private const uint MouseEventfHWheel = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type; // 0 = MOUSE
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetPhysicalCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetPhysicalCursorPos(int x, int y);

    private static void SendMouse(uint flags, uint mouseData = 0)
    {
        var input = new INPUT
        {
            type = 0,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = mouseData,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        var inputs = new INPUT[] { input };
        if (SendInput(1, inputs, Marshal.SizeOf<INPUT>()) != 1)
            Log.Warn($"SendInput 失败，错误码 {Marshal.GetLastWin32Error()}（如需控制管理员权限的窗口，请以管理员身份运行本程序）");
    }

    public static void MoveCursor(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        // 使用物理坐标，避免 DPI 虚拟化导致光标位置漂移
        if (GetPhysicalCursorPos(out POINT p) || GetCursorPos(out p))
        {
            var target = new POINT { X = p.X + dx, Y = p.Y + dy };
            // SetCursorPos / SetPhysicalCursorPos 会自动截断到屏幕范围内
            if (!SetPhysicalCursorPos(target.X, target.Y))
                SetCursorPos(target.X, target.Y);
        }
    }

    public static void LeftDown() => SendMouse(MouseEventfLeftDown);
    public static void LeftUp() => SendMouse(MouseEventfLeftUp);
    public static void RightDown() => SendMouse(MouseEventfRightDown);
    public static void RightUp() => SendMouse(MouseEventfRightUp);
    public static void MiddleDown() => SendMouse(MouseEventfMiddleDown);
    public static void MiddleUp() => SendMouse(MouseEventfMiddleUp);

    public static void LeftClick()
    {
        LeftDown();
        LeftUp();
    }

    public static void RightClick()
    {
        RightDown();
        RightUp();
    }

    public static void MiddleClick()
    {
        MiddleDown();
        MiddleUp();
    }

    public static void DoubleLeftClick()
    {
        LeftClick();
        Thread.Sleep(40);
        LeftClick();
    }

    /// <summary>delta 为 120 的倍数，正数向上。</summary>
    public static void Wheel(int delta) => SendMouse(MouseEventfWheel, unchecked((uint)delta));

    /// <summary>delta 为 120 的倍数，正数向右。</summary>
    public static void HWheel(int delta) => SendMouse(MouseEventfHWheel, unchecked((uint)delta));
}
