using System.Runtime.InteropServices;

namespace GamepadMouse;

/// <summary>
/// XInput 动态加载封装：依次尝试 xinput1_4 / xinput1_3 / xinput9_1_0。
/// </summary>
internal static class XInput
{
    // XINPUT_GAMEPAD 按键位标志
    public const ushort DPadUp = 0x0001;
    public const ushort DPadDown = 0x0002;
    public const ushort DPadLeft = 0x0004;
    public const ushort DPadRight = 0x0008;
    public const ushort Start = 0x0010;
    public const ushort Back = 0x0020;
    public const ushort LeftThumb = 0x0040;   // LSB
    public const ushort RightThumb = 0x0080;  // RSB
    public const ushort LeftShoulder = 0x0100;  // LB
    public const ushort RightShoulder = 0x0200; // RB
    public const ushort A = 0x1000;
    public const ushort B = 0x2000;
    public const ushort X = 0x4000;
    public const ushort Y = 0x8000;

    public const string ControllerGuid = "XInput";

    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public uint dwPacketNumber;
        public Gamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetStateDelegate(int userIndex, ref State state);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetStateDelegate(int userIndex, ref Vibration vibration);

    private static GetStateDelegate? _getState;
    private static SetStateDelegate? _setState;

    /// <summary>是否成功加载了任一 XInput 库。</summary>
    public static bool Available { get; private set; }

    static XInput()
    {
        foreach (var lib in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            if (TryLoad(lib))
            {
                Available = true;
                Log.Info($"已加载 {lib}");
                return;
            }
        }
        Log.Warn("未找到可用的 XInput 库");
    }

    private static bool TryLoad(string libName)
    {
        if (!NativeLibrary.TryLoad(libName, out IntPtr hModule))
            return false;

        var pGet = GetProcAddress(hModule, "XInputGetState");
        var pSet = GetProcAddress(hModule, "XInputSetState");
        if (pGet == IntPtr.Zero || pSet == IntPtr.Zero)
        {
            NativeLibrary.Free(hModule);
            return false;
        }

        _getState = Marshal.GetDelegateForFunctionPointer<GetStateDelegate>(pGet);
        _setState = Marshal.GetDelegateForFunctionPointer<SetStateDelegate>(pSet);
        return true;
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    /// <summary>ERROR_SUCCESS=0, ERROR_DEVICE_NOT_CONNECTED=1167。</summary>
    public static bool GetState(int userIndex, ref State state)
    {
        if (_getState is null) return false;
        return _getState(userIndex, ref state) == 0;
    }

    public static void SetVibration(int userIndex, ushort leftMotor, ushort rightMotor)
    {
        if (_setState is null) return;
        var vib = new Vibration { wLeftMotorSpeed = leftMotor, wRightMotorSpeed = rightMotor };
        _setState(userIndex, ref vib);
    }
}
