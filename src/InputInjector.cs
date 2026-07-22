using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

internal sealed class InputInjector {
    private struct KeyDef {
        public ushort Vk;
        public ushort Scan;
        public bool Extended;
    }

    private static readonly object s_instancesLock = new object();
    private static readonly List<InputInjector> s_instances = new List<InputInjector>();

    private readonly Dictionary<PhysicalKey, KeyDef> _keys = new Dictionary<PhysicalKey, KeyDef>();
    private readonly Dictionary<PhysicalKey, int> _modifierHoldCounts = new Dictionary<PhysicalKey, int>();
    private readonly HashSet<PhysicalKey> _heldKeys = new HashSet<PhysicalKey>();
    private readonly object _heldLock = new object();
    private readonly KeyDef _shift;
    private readonly KeyDef _ctrl;
    private readonly KeyDef _alt;
    private readonly KeyDef _win;
    private bool _leftMouseHeld;
    private bool _rightMouseHeld;

    public string CurrentSource = "Unknown";
    public string CurrentReason = "";

    public InputInjector() {
        if (!InterceptionDriver.Initialize()) {
            throw new InvalidOperationException("Interception 驱动不可用。请安装驱动、重启 Windows，并以管理员权限运行 ShikiPad。");
        }
        InitKeys();
        _shift = Resolve(0xA0, false);
        _ctrl = Resolve(0xA2, false);
        _alt = Resolve(0xA4, false);
        _win = Resolve(0x5B, true);
        lock (s_instancesLock) s_instances.Add(this);
    }

    public static void ReleaseAllRegistered() {
        InputInjector[] injectors;
        lock (s_instancesLock) injectors = s_instances.ToArray();
        for (int i = 0; i < injectors.Length; i++) {
            try {
                injectors[i].ReleaseAll();
            } catch (Exception first) {
                try {
                    injectors[i].ReleaseAll();
                } catch (Exception second) {
                    try { Console.Error.WriteLine("[warn] 输入释放连续失败：" + first.Message + "；重试：" + second.Message); } catch { }
                }
            }
        }
    }


    public void KeyDown(PhysicalKey key) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        lock (_heldLock) {
            if (IsReferenceCountedModifier(key)) {
                int count;
                _modifierHoldCounts.TryGetValue(key, out count);
                if (count > 0) {
                    _modifierHoldCounts[key] = count + 1;
                    return;
                }
            }

            List<INPUT> inputs = new List<INPUT>();
            AddKey(inputs, _keys[key], false);
            Send(inputs, "KeyDown(" + key + ")");
            _heldKeys.Add(key);
            if (IsReferenceCountedModifier(key)) _modifierHoldCounts[key] = 1;
        }
    }

    public void KeyUp(PhysicalKey key) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        lock (_heldLock) {
            bool releaseTrackedModifier = false;
            if (IsReferenceCountedModifier(key)) {
                int count;
                if (_modifierHoldCounts.TryGetValue(key, out count)) {
                    if (count > 1) {
                        _modifierHoldCounts[key] = count - 1;
                        return;
                    }
                }
                if (!_heldKeys.Contains(key)) return;
                releaseTrackedModifier = true;
            }

            List<INPUT> inputs = new List<INPUT>();
            AddKey(inputs, _keys[key], true);
            Send(inputs, "KeyUp(" + key + ")");
            if (releaseTrackedModifier) _modifierHoldCounts.Remove(key);
            _heldKeys.Remove(key);
        }
    }

    public void KeyTap(PhysicalKey key, bool shift, bool ctrl, bool alt, bool win) {
        KeyTapCore(key, shift, ctrl, alt, win, false);
    }

    public void KeyTapExact(PhysicalKey key, bool shift, bool ctrl, bool alt, bool win) {
        KeyTapCore(key, shift, ctrl, alt, win, true);
    }

    private void KeyTapCore(PhysicalKey key, bool shift, bool ctrl, bool alt, bool win, bool isolateUnrequestedModifiers) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        lock (_heldLock) {
            bool restoreHeldKey = ShouldRestoreHeldKeyForTap(_heldKeys.Contains(key), key);
            bool pressShift = shift && !IsModifierGroupHeld(PhysicalKey.LShift, PhysicalKey.RShift);
            bool pressCtrl = ctrl && !IsModifierGroupHeld(PhysicalKey.LCtrl, PhysicalKey.RCtrl);
            bool pressAlt = alt && !IsModifierGroupHeld(PhysicalKey.LAlt, PhysicalKey.RAlt);
            bool pressWin = win && !IsModifierGroupHeld(PhysicalKey.LWin, PhysicalKey.RWin);
            List<PhysicalKey> suspendedModifiers = new List<PhysicalKey>();

            List<INPUT> inputs = new List<INPUT>();
            if (restoreHeldKey) AddKey(inputs, _keys[key], true);
            if (isolateUnrequestedModifiers) {
                foreach (PhysicalKey heldKey in _heldKeys) {
                    if (!IsReferenceCountedModifier(heldKey) || !ShouldSuspendModifierForExactTap(heldKey, shift, ctrl, alt, win)) continue;
                    AddKey(inputs, _keys[heldKey], true);
                    suspendedModifiers.Add(heldKey);
                }
            }
            if (pressShift) AddKey(inputs, _shift, false);
            if (pressCtrl) AddKey(inputs, _ctrl, false);
            if (pressAlt) AddKey(inputs, _alt, false);
            if (pressWin) AddKey(inputs, _win, false);
            AddKey(inputs, _keys[key], false);
            AddKey(inputs, _keys[key], true);
            if (pressWin) AddKey(inputs, _win, true);
            if (pressAlt) AddKey(inputs, _alt, true);
            if (pressCtrl) AddKey(inputs, _ctrl, true);
            if (pressShift) AddKey(inputs, _shift, true);
            for (int i = suspendedModifiers.Count - 1; i >= 0; i--) AddKey(inputs, _keys[suspendedModifiers[i]], false);
            if (restoreHeldKey) AddKey(inputs, _keys[key], false);
            Send(inputs, (isolateUnrequestedModifiers ? "KeyTapExact(" : "KeyTap(") + key + ")");
        }
    }

    private bool IsModifierGroupHeld(PhysicalKey left, PhysicalKey right) {
        return _heldKeys.Contains(left) || _heldKeys.Contains(right);
    }

    private bool IsHeld(PhysicalKey key) {
        lock (_heldLock) {
            return _heldKeys.Contains(key);
        }
    }

    public bool IsKeyHeld(PhysicalKey key) {
        return IsHeld(key);
    }

    public void MouseMove(int dx, int dy) {
        if (dx == 0 && dy == 0) return;
        INPUT input = new INPUT();
        input.type = INPUT_MOUSE;
        MOUSEINPUT mouse = new MOUSEINPUT();
        mouse.dwFlags = MOUSEEVENTF_MOVE;
        mouse.dx = dx;
        mouse.dy = dy;
        input.mi = mouse;
        List<INPUT> inputs = new List<INPUT>();
        inputs.Add(input);
        Send(inputs, "MouseMove(" + dx + ", " + dy + ")");
    }

    public void MouseButton(int button, bool down) {
        lock (_heldLock) {
            INPUT input = new INPUT();
            input.type = INPUT_MOUSE;
            MOUSEINPUT mouse = new MOUSEINPUT();
            if (button == 0) mouse.dwFlags = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else mouse.dwFlags = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            input.mi = mouse;
            List<INPUT> inputs = new List<INPUT>();
            inputs.Add(input);
            Send(inputs, "MouseButton(" + button + ", " + down + ")");
            if (button == 0) _leftMouseHeld = down;
            else _rightMouseHeld = down;
        }
    }

    public void MouseWheel(int delta) {
        MouseWheelDelta(delta * WHEEL_DELTA);
    }

    public void MouseWheelDelta(int wheelDelta) {
        if (wheelDelta == 0) return;
        INPUT input = new INPUT();
        input.type = INPUT_MOUSE;
        MOUSEINPUT mouse = new MOUSEINPUT();
        mouse.dwFlags = MOUSEEVENTF_WHEEL;
        mouse.mouseData = wheelDelta;
        input.mi = mouse;
        List<INPUT> inputs = new List<INPUT>();
        inputs.Add(input);
        Send(inputs, "MouseWheelDelta(" + wheelDelta + ")");
    }

    public void ReleaseAll() {
        List<INPUT> inputs = new List<INPUT>();
        lock (_heldLock) {
            foreach (PhysicalKey key in _heldKeys) {
                KeyDef def;
                if (_keys.TryGetValue(key, out def)) AddKey(inputs, def, true);
            }

            if (_leftMouseHeld) AddMouseButton(inputs, 0, false);
            if (_rightMouseHeld) AddMouseButton(inputs, 1, false);

            Send(inputs, "ReleaseAll");
            _heldKeys.Clear();
            _modifierHoldCounts.Clear();
            _leftMouseHeld = false;
            _rightMouseHeld = false;
        }
    }

    private static bool IsReferenceCountedModifier(PhysicalKey key) {
        return key == PhysicalKey.LShift ||
               key == PhysicalKey.RShift ||
               key == PhysicalKey.LCtrl ||
               key == PhysicalKey.RCtrl ||
               key == PhysicalKey.LAlt ||
               key == PhysicalKey.RAlt ||
               key == PhysicalKey.LWin ||
               key == PhysicalKey.RWin;
    }

    internal static bool ShouldRestoreHeldKeyForTap(bool keyHeld, PhysicalKey key) {
        return keyHeld && !IsReferenceCountedModifier(key);
    }

    internal static bool ShouldSuspendModifierForExactTap(PhysicalKey modifier, bool shift, bool ctrl, bool alt, bool win) {
        if (modifier == PhysicalKey.LShift || modifier == PhysicalKey.RShift) return !shift;
        if (modifier == PhysicalKey.LCtrl || modifier == PhysicalKey.RCtrl) return !ctrl;
        if (modifier == PhysicalKey.LAlt || modifier == PhysicalKey.RAlt) return !alt;
        if (modifier == PhysicalKey.LWin || modifier == PhysicalKey.RWin) return !win;
        return false;
    }

    private void InitKeys() {
        for (int i = 0; i < 26; i++) Add((PhysicalKey)((int)PhysicalKey.A + i), (ushort)('A' + i), false);
        Add(PhysicalKey.Num0, (ushort)'0', false);
        Add(PhysicalKey.Num1, (ushort)'1', false);
        Add(PhysicalKey.Num2, (ushort)'2', false);
        Add(PhysicalKey.Num3, (ushort)'3', false);
        Add(PhysicalKey.Num4, (ushort)'4', false);
        Add(PhysicalKey.Num5, (ushort)'5', false);
        Add(PhysicalKey.Num6, (ushort)'6', false);
        Add(PhysicalKey.Num7, (ushort)'7', false);
        Add(PhysicalKey.Num8, (ushort)'8', false);
        Add(PhysicalKey.Num9, (ushort)'9', false);
        Add(PhysicalKey.Minus, 0xBD, false);
        Add(PhysicalKey.Equals, 0xBB, false);
        Add(PhysicalKey.LeftBracket, 0xDB, false);
        Add(PhysicalKey.RightBracket, 0xDD, false);
        Add(PhysicalKey.Backslash, 0xDC, false);
        Add(PhysicalKey.Semicolon, 0xBA, false);
        Add(PhysicalKey.Apostrophe, 0xDE, false);
        Add(PhysicalKey.Comma, 0xBC, false);
        Add(PhysicalKey.Period, 0xBE, false);
        Add(PhysicalKey.Slash, 0xBF, false);
        Add(PhysicalKey.Grave, 0xC0, false);
        Add(PhysicalKey.Space, 0x20, false);
        Add(PhysicalKey.Backspace, 0x08, false);
        Add(PhysicalKey.Enter, 0x0D, false);
        Add(PhysicalKey.Tab, 0x09, false);
        Add(PhysicalKey.Escape, 0x1B, false);
        Add(PhysicalKey.Delete, 0x2E, true);
        Add(PhysicalKey.Home, 0x24, true);
        Add(PhysicalKey.End, 0x23, true);
        Add(PhysicalKey.ArrowUp, 0x26, true);
        Add(PhysicalKey.ArrowDown, 0x28, true);
        Add(PhysicalKey.ArrowLeft, 0x25, true);
        Add(PhysicalKey.ArrowRight, 0x27, true);
        Add(PhysicalKey.F1, 0x70, false);
        Add(PhysicalKey.F2, 0x71, false);
        Add(PhysicalKey.F3, 0x72, false);
        Add(PhysicalKey.F4, 0x73, false);
        Add(PhysicalKey.F5, 0x74, false);
        Add(PhysicalKey.F6, 0x75, false);
        Add(PhysicalKey.F7, 0x76, false);
        Add(PhysicalKey.F8, 0x77, false);
        Add(PhysicalKey.F9, 0x78, false);
        Add(PhysicalKey.F10, 0x79, false);
        Add(PhysicalKey.F11, 0x7A, false);
        Add(PhysicalKey.F12, 0x7B, false);
        Add(PhysicalKey.LWin, 0x5B, true);
        Add(PhysicalKey.RWin, 0x5C, true);
        Add(PhysicalKey.LAlt, 0xA4, false);
        Add(PhysicalKey.LCtrl, 0xA2, false);
        Add(PhysicalKey.LShift, 0xA0, false);
        Add(PhysicalKey.RAlt, 0xA5, true);
        Add(PhysicalKey.RCtrl, 0xA3, true);
        Add(PhysicalKey.RShift, 0xA1, false);
        Add(PhysicalKey.CapsLock, 0x14, false);
    }

    private void Add(PhysicalKey key, ushort vk, bool extended) {
        _keys[key] = Resolve(vk, extended);
    }

    private static KeyDef Resolve(ushort vk, bool extended) {
        uint raw = MapVirtualKey(vk, MAPVK_VK_TO_VSC_EX);
        KeyDef def = new KeyDef();
        def.Vk = vk;
        def.Scan = (ushort)(raw & 0xFF);
        def.Extended = extended || ((raw & 0xFF00) != 0);
        return def;
    }

    private void AddKey(List<INPUT> inputs, KeyDef key, bool up) {
        INPUT input = new INPUT();
        input.type = INPUT_KEYBOARD;
        KEYBDINPUT keyboard = new KEYBDINPUT();

        // Interception consumes scan codes directly.
        keyboard.wScan = key.Scan;
        keyboard.wVk = 0;
        keyboard.dwFlags = KEYEVENTF_SCANCODE;
        if (up) keyboard.dwFlags |= KEYEVENTF_KEYUP;
        if (key.Extended) keyboard.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        input.ki = keyboard;
        inputs.Add(input);
    }

    private void AddMouseButton(List<INPUT> inputs, int button, bool down) {
        INPUT input = new INPUT();
        input.type = INPUT_MOUSE;
        MOUSEINPUT mouse = new MOUSEINPUT();
        if (button == 0) mouse.dwFlags = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
        else mouse.dwFlags = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
        input.mi = mouse;
        inputs.Add(input);
    }
    private void Send(List<INPUT> inputs, string actionType) {
        if (inputs.Count == 0) return;
        string failedStrokeType = null;
        foreach (var input in inputs) {
            if (input.type == INPUT_KEYBOARD) {
                InterceptionDriver.KeyState state;
                if ((input.ki.dwFlags & KEYEVENTF_KEYUP) != 0) {
                    state = (input.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0 ? (InterceptionDriver.KeyState.E0 | InterceptionDriver.KeyState.Up) : InterceptionDriver.KeyState.Up;
                } else {
                    state = (input.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0 ? (InterceptionDriver.KeyState.E0 | InterceptionDriver.KeyState.Down) : InterceptionDriver.KeyState.Down;
                }
                if (!TrySendKey(input.ki.wScan, state) && failedStrokeType == null) failedStrokeType = "keyboard";
            } else if (input.type == INPUT_MOUSE) {
                if ((input.mi.dwFlags & MOUSEEVENTF_MOVE) != 0) {
                    if (!TrySendMouseDelta(input.mi.dx, input.mi.dy) && failedStrokeType == null) failedStrokeType = "mouse move";
                } else if ((input.mi.dwFlags & MOUSEEVENTF_WHEEL) != 0) {
                    if (!TrySendMouseWheel(input.mi.mouseData) && failedStrokeType == null) failedStrokeType = "mouse wheel";
                } else {
                    InterceptionDriver.MouseState state = 0;
                    if ((input.mi.dwFlags & MOUSEEVENTF_LEFTDOWN) != 0) state |= InterceptionDriver.MouseState.LeftButtonDown;
                    if ((input.mi.dwFlags & MOUSEEVENTF_LEFTUP) != 0) state |= InterceptionDriver.MouseState.LeftButtonUp;
                    if ((input.mi.dwFlags & MOUSEEVENTF_RIGHTDOWN) != 0) state |= InterceptionDriver.MouseState.RightButtonDown;
                    if ((input.mi.dwFlags & MOUSEEVENTF_RIGHTUP) != 0) state |= InterceptionDriver.MouseState.RightButtonUp;
                    if (state != 0) {
                        if (!TrySendMouse(state) && failedStrokeType == null) failedStrokeType = "mouse button";
                    }
                }
            }
        }
        if (failedStrokeType != null) ThrowSendFailure(actionType, failedStrokeType);
    }

    private static bool TrySendKey(ushort code, InterceptionDriver.KeyState state) {
        return InterceptionDriver.SendKey(code, state) || InterceptionDriver.SendKey(code, state);
    }

    private static bool TrySendMouse(InterceptionDriver.MouseState state) {
        return InterceptionDriver.SendMouse(state) || InterceptionDriver.SendMouse(state);
    }

    private static bool TrySendMouseDelta(int dx, int dy) {
        return InterceptionDriver.SendMouseDelta(dx, dy) || InterceptionDriver.SendMouseDelta(dx, dy);
    }

    private static bool TrySendMouseWheel(int delta) {
        return InterceptionDriver.SendMouseWheel(delta) || InterceptionDriver.SendMouseWheel(delta);
    }

    private void ThrowSendFailure(string actionType, string strokeType) {
        throw new InvalidOperationException("Interception 发送失败（" + strokeType + "，来源 " + CurrentSource + "，动作 " + actionType + "，原因 " + CurrentReason + "）。");
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const int WHEEL_DELTA = 120;
    private const uint MAPVK_VK_TO_VSC_EX = 4;

    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT {
        public uint type;
        public InputUnion u;
        public MOUSEINPUT mi { get { return u.mi; } set { u.mi = value; } }
        public KEYBDINPUT ki { get { return u.ki; } set { u.ki = value; } }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
