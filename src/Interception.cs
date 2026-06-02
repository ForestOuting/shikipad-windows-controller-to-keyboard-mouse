using System;
using System.Runtime.InteropServices;

public static class InterceptionDriver {


    public enum KeyState : ushort {
        Down = 0x00,
        Up = 0x01,
        E0 = 0x02,
        E1 = 0x04,
        TermSrvSetLED = 0x08,
        TermSrvShadow = 0x10,
        TermSrvVkPacket = 0x20
    }

    public enum MouseState : ushort {
        LeftButtonDown = 0x001,
        LeftButtonUp = 0x002,
        RightButtonDown = 0x004,
        RightButtonUp = 0x008,
        MiddleButtonDown = 0x010,
        MiddleButtonUp = 0x020,
        Button4Down = 0x040,
        Button4Up = 0x080,
        Button5Down = 0x100,
        Button5Up = 0x200,
        Wheel = 0x400,
        HWheel = 0x800
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InterceptionMouseStroke {
        public ushort state;
        public ushort flags;
        public short rolling;
        public int x;
        public int y;
        public uint information;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InterceptionKeyStroke {
        public ushort code;
        public ushort state;
        public uint information;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InterceptionStroke {
        [FieldOffset(0)] public InterceptionMouseStroke mouse;
        [FieldOffset(0)] public InterceptionKeyStroke keyboard;
    }

    [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr interception_create_context();



    [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int interception_send(IntPtr context, int device, ref InterceptionStroke stroke, uint num_strokes);
    


    // Hardcoded device IDs for injection
    // Device 1 is the first keyboard, Device 11 is the first mouse
    public const int KEYBOARD_DEVICE = 1;
    public const int MOUSE_DEVICE = 11;

    private static IntPtr _context = IntPtr.Zero;

    public static bool Initialize() {
        if (_context == IntPtr.Zero) {
            try {
                _context = interception_create_context();
            } catch (Exception) {
                return false;
            }
        }
        return _context != IntPtr.Zero;
    }

    public static void SendKey(ushort code, KeyState state) {
        if (_context == IntPtr.Zero) return;
        InterceptionStroke stroke = new InterceptionStroke();
        stroke.keyboard.code = code;
        stroke.keyboard.state = (ushort)state;
        interception_send(_context, KEYBOARD_DEVICE, ref stroke, 1);
    }

    public static void SendMouse(MouseState state) {
        if (_context == IntPtr.Zero) return;
        InterceptionStroke stroke = new InterceptionStroke();
        stroke.mouse.state = (ushort)state;
        interception_send(_context, MOUSE_DEVICE, ref stroke, 1);
    }
    
    public static void SendMouseDelta(int dx, int dy) {
        if (_context == IntPtr.Zero) return;
        InterceptionStroke stroke = new InterceptionStroke();
        stroke.mouse.x = dx;
        stroke.mouse.y = dy;
        stroke.mouse.flags = 0; 
        interception_send(_context, MOUSE_DEVICE, ref stroke, 1);
    }

    public static void SendMouseWheel(int rolling) {
        if (_context == IntPtr.Zero) return;
        InterceptionStroke stroke = new InterceptionStroke();
        stroke.mouse.state = (ushort)MouseState.Wheel;
        stroke.mouse.rolling = (short)rolling;
        interception_send(_context, MOUSE_DEVICE, ref stroke, 1);
    }
}
