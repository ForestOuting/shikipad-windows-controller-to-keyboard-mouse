using System;
using System.Runtime.InteropServices;

internal static class InterceptionDriver {
    public enum KeyState : ushort {
        Down = 0x00,
        Up = 0x01,
        E0 = 0x02
    }

    public enum MouseState : ushort {
        LeftButtonDown = 0x001,
        LeftButtonUp = 0x002,
        RightButtonDown = 0x004,
        RightButtonUp = 0x008,
        Wheel = 0x400
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

    [DllImport("interception.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void interception_destroy_context(IntPtr context);

    // Hardcoded device IDs for injection
    // Device 1 is the first keyboard, Device 11 is the first mouse
    public const int KEYBOARD_DEVICE = 1;
    public const int MOUSE_DEVICE = 11;

    private static readonly object s_contextLock = new object();
    private static IntPtr _context = IntPtr.Zero;

    public static bool Initialize() {
        lock (s_contextLock) {
            if (_context == IntPtr.Zero) {
                try {
                    _context = interception_create_context();
                } catch (Exception) {
                    return false;
                }
            }
            return _context != IntPtr.Zero;
        }
    }

    public static void Cleanup() {
        lock (s_contextLock) {
            if (_context != IntPtr.Zero) {
                try {
                    interception_destroy_context(_context);
                } catch {
                } finally {
                    _context = IntPtr.Zero;
                }
            }
        }
    }

    public static bool SendKey(ushort code, KeyState state) {
        lock (s_contextLock) {
            if (_context == IntPtr.Zero) return false;
            InterceptionStroke stroke = new InterceptionStroke();
            stroke.keyboard.code = code;
            stroke.keyboard.state = (ushort)state;
            return interception_send(_context, KEYBOARD_DEVICE, ref stroke, 1) == 1;
        }
    }

    public static bool SendMouse(MouseState state) {
        lock (s_contextLock) {
            if (_context == IntPtr.Zero) return false;
            InterceptionStroke stroke = new InterceptionStroke();
            stroke.mouse.state = (ushort)state;
            return interception_send(_context, MOUSE_DEVICE, ref stroke, 1) == 1;
        }
    }

    public static bool SendMouseDelta(int dx, int dy) {
        lock (s_contextLock) {
            if (_context == IntPtr.Zero) return false;
            InterceptionStroke stroke = new InterceptionStroke();
            stroke.mouse.x = dx;
            stroke.mouse.y = dy;
            stroke.mouse.flags = 0;
            return interception_send(_context, MOUSE_DEVICE, ref stroke, 1) == 1;
        }
    }

    public static bool SendMouseWheel(int rolling) {
        lock (s_contextLock) {
            if (_context == IntPtr.Zero) return false;
            InterceptionStroke stroke = new InterceptionStroke();
            stroke.mouse.state = (ushort)MouseState.Wheel;
            stroke.mouse.rolling = (short)rolling;
            return interception_send(_context, MOUSE_DEVICE, ref stroke, 1) == 1;
        }
    }
}
