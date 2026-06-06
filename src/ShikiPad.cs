using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal enum PhysicalKey {
      None, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9, Minus, Equals, LeftBracket, RightBracket, Backslash, Semicolon, Apostrophe, Comma, Period, Slash, Grave, Space, Backspace, Enter, Tab, Escape, ArrowUp, ArrowDown, ArrowLeft, ArrowRight, LShift, RShift, LCtrl, RCtrl, LAlt, RAlt, LWin, RWin, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

internal enum Layer {
    Base,
    L1,
    R1,
    L2,
    R2,
    R1R2,
    L1L2,
    Reserved
}

internal enum ActionButton {
    Up,
    Right,
    Square,
    Triangle,
    Left,
    Down,
    Cross,
    Circle
}

internal enum StickDirection {
    None,
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft
}

internal enum ControllerProfile {
    DualSense,
    Xbox360,
    XboxSeries
}

internal sealed class Config {
    public bool Enabled = true;
    public double MouseSensitivity = 1.0;
    public double MouseMaxSpeed = 28.0;
    public double RightStickDeadzone = 0.05;
    public string RightStickCurve = "power";
    public double RightStickCurveExponent = 2.2;
    public double LeftStickEnterDeadzone = 0.50;
    public double LeftStickExitDeadzone = 0.35;
    public double TriggerPressThreshold = 0.35;
    public double TriggerReleaseThreshold = 0.25;
    public int RepeatDelayMs = 180;
    public int RepeatIntervalMs = 20;
    public int BaseRepeatSlowIntervalMs = 160;
    public int BaseRepeatRampMs = 1200;
    public int ActionLayerGraceMs = 80;
    public int LayerTakeoverWindowMs = 35;
    public int ActionLayerSwitchGuardMs = 120;
    public int ComboLayerWindowMs = 50;
    public bool UseScanCode = true;
    public bool UseInterception = true;
    public int ScrollSlowIntervalMs = 100;
    public int ScrollFastIntervalMs = 20;
    public int R3FreezeMs = 60;
    public static Config Load(string path) {
        Config cfg = new Config();
        if (!File.Exists(path)) {
            cfg.Save(path);
            return cfg;
        }

        try {
            string text = File.ReadAllText(path);
            bool shouldSaveMigratedConfig = text.Contains("\"mouseDeadzone\"") ||
                                            text.Contains("\"touchpadSwipeThreshold\"") ||
                                            text.Contains("\"touchpadMaxSwipeMs\"") ||
                                            !text.Contains("\"rightStickDeadzone\"") ||
                                            !text.Contains("\"rightStickCurve\"") ||
                                            !text.Contains("\"rightStickCurveExponent\"") ||
                                            !text.Contains("\"rightStickEpsilon\"") ||
                                            !text.Contains("\"leftStickEnterDeadzone\"") ||
                                            !text.Contains("\"leftStickExitDeadzone\"");
            bool shouldSaveLeftStickConfig = false;
            cfg.Enabled = GetBool(text, "enabled", cfg.Enabled);
            cfg.MouseSensitivity = GetDouble(text, "mouseSensitivity", cfg.MouseSensitivity);
            cfg.MouseMaxSpeed = GetDouble(text, "mouseMaxSpeed", cfg.MouseMaxSpeed);
            cfg.RightStickDeadzone = GetDouble(text, "rightStickDeadzone", cfg.RightStickDeadzone);
            cfg.RightStickCurve = GetString(text, "rightStickCurve", cfg.RightStickCurve);
            cfg.RightStickCurveExponent = GetDouble(text, "rightStickCurveExponent", cfg.RightStickCurveExponent);
            cfg.LeftStickEnterDeadzone = GetDouble(text, "leftStickEnterDeadzone", cfg.LeftStickEnterDeadzone);
            cfg.LeftStickExitDeadzone = GetDouble(text, "leftStickExitDeadzone", cfg.LeftStickExitDeadzone);
            cfg.TriggerPressThreshold = GetDouble(text, "triggerPressThreshold", cfg.TriggerPressThreshold);
            cfg.TriggerReleaseThreshold = GetDouble(text, "triggerReleaseThreshold", cfg.TriggerReleaseThreshold);
            cfg.RepeatDelayMs = GetInt(text, "repeatDelayMs", cfg.RepeatDelayMs);
            cfg.RepeatIntervalMs = GetInt(text, "repeatIntervalMs", cfg.RepeatIntervalMs);
            cfg.BaseRepeatSlowIntervalMs = GetInt(text, "baseRepeatSlowIntervalMs", cfg.BaseRepeatSlowIntervalMs);
            cfg.BaseRepeatRampMs = GetInt(text, "baseRepeatRampMs", cfg.BaseRepeatRampMs);
            cfg.ActionLayerGraceMs = GetInt(text, "actionLayerGraceMs", cfg.ActionLayerGraceMs);
            cfg.LayerTakeoverWindowMs = GetInt(text, "layerTakeoverWindowMs", cfg.LayerTakeoverWindowMs);
            cfg.ActionLayerSwitchGuardMs = GetInt(text, "actionLayerSwitchGuardMs", cfg.ActionLayerSwitchGuardMs);
            cfg.ComboLayerWindowMs = GetInt(text, "comboLayerWindowMs", cfg.ComboLayerWindowMs);
            cfg.UseScanCode = GetBool(text, "useScanCode", cfg.UseScanCode);
            cfg.UseInterception = GetBool(text, "useInterception", cfg.UseInterception);
            cfg.ScrollSlowIntervalMs = GetInt(text, "scrollSlowIntervalMs", cfg.ScrollSlowIntervalMs);
            cfg.ScrollFastIntervalMs = GetInt(text, "scrollFastIntervalMs", cfg.ScrollFastIntervalMs);
            cfg.R3FreezeMs = GetInt(text, "r3FreezeMs", cfg.R3FreezeMs);
            
            if (cfg.RightStickDeadzone == 0.0) {
                Logger.Info("migrating rightStickDeadzone to 0.03");
                cfg.RightStickDeadzone = 0.03;
                shouldSaveMigratedConfig = true;
            }

            if (!String.Equals(cfg.RightStickCurve, "power", StringComparison.Ordinal)) {
                Logger.Warn("unsupported rightStickCurve '" + cfg.RightStickCurve + "'; using power");
                cfg.RightStickCurve = "power";
                shouldSaveMigratedConfig = true;
            }
            if (cfg.RightStickCurveExponent <= 0.0 || Double.IsNaN(cfg.RightStickCurveExponent) || Double.IsInfinity(cfg.RightStickCurveExponent)) {
                Logger.Warn("invalid rightStickCurveExponent; using 2.2");
                cfg.RightStickCurveExponent = 2.2;
                shouldSaveMigratedConfig = true;
            }
            if (!text.Contains("\"baseRepeatSlowIntervalMs\"") ||
                !text.Contains("\"baseRepeatRampMs\"") ||
                !text.Contains("\"actionLayerGraceMs\"") ||
                !text.Contains("\"layerTakeoverWindowMs\"") ||
                !text.Contains("\"actionLayerSwitchGuardMs\"") ||
                !text.Contains("\"comboLayerWindowMs\"")) {
                shouldSaveMigratedConfig = true;
            }
            if (cfg.LayerTakeoverWindowMs < 0 || cfg.LayerTakeoverWindowMs > cfg.ActionLayerGraceMs) {
                int fallbackLayerTakeoverMs = Math.Min(50, Math.Max(0, cfg.ActionLayerGraceMs));
                Logger.Warn("invalid layerTakeoverWindowMs; using " + fallbackLayerTakeoverMs.ToString(CultureInfo.InvariantCulture));
                cfg.LayerTakeoverWindowMs = fallbackLayerTakeoverMs;
                shouldSaveMigratedConfig = true;
            }
            if (cfg.ComboLayerWindowMs < 0 || cfg.ComboLayerWindowMs > 500) {
                Logger.Warn("invalid comboLayerWindowMs; using 80");
                cfg.ComboLayerWindowMs = 80;
                shouldSaveMigratedConfig = true;
            }
            if (cfg.ComboLayerWindowMs == 100) {
                Logger.Info("migrating comboLayerWindowMs from 100 to 80");
                cfg.ComboLayerWindowMs = 80;
                shouldSaveMigratedConfig = true;
            }
            if (Math.Abs(cfg.LeftStickEnterDeadzone - 0.30) < 0.000001) {
                Logger.Info("migrating leftStickEnterDeadzone from 0.30 to 0.50");
                cfg.LeftStickEnterDeadzone = 0.50;
                shouldSaveLeftStickConfig = true;
            }
            if (Math.Abs(cfg.LeftStickExitDeadzone - 0.20) < 0.000001) {
                Logger.Info("migrating leftStickExitDeadzone from 0.20 to 0.35");
                cfg.LeftStickExitDeadzone = 0.35;
                shouldSaveLeftStickConfig = true;
            }
            if (shouldSaveMigratedConfig || shouldSaveLeftStickConfig) cfg.Save(path);
        } catch (Exception ex) {
            Logger.Error("config load failed: " + ex.Message);
        }

        return cfg;
    }

    public void Save(string path) {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        Write(sb, "enabled", Enabled, true);
        Write(sb, "mouseSensitivity", MouseSensitivity, true);
        Write(sb, "mouseMaxSpeed", MouseMaxSpeed, true);
        Write(sb, "rightStickDeadzone", RightStickDeadzone, true);
        Write(sb, "rightStickCurve", RightStickCurve, true);
        Write(sb, "rightStickCurveExponent", RightStickCurveExponent, true);
        Write(sb, "leftStickEnterDeadzone", LeftStickEnterDeadzone, true);
        Write(sb, "leftStickExitDeadzone", LeftStickExitDeadzone, true);
        Write(sb, "triggerPressThreshold", TriggerPressThreshold, true);
        Write(sb, "triggerReleaseThreshold", TriggerReleaseThreshold, true);
        Write(sb, "repeatDelayMs", RepeatDelayMs, true);
        Write(sb, "repeatIntervalMs", RepeatIntervalMs, true);
        Write(sb, "baseRepeatSlowIntervalMs", BaseRepeatSlowIntervalMs, true);
        Write(sb, "baseRepeatRampMs", BaseRepeatRampMs, true);
        Write(sb, "actionLayerGraceMs", ActionLayerGraceMs, true);
        Write(sb, "layerTakeoverWindowMs", LayerTakeoverWindowMs, true);
        Write(sb, "actionLayerSwitchGuardMs", ActionLayerSwitchGuardMs, true);
        Write(sb, "comboLayerWindowMs", ComboLayerWindowMs, true);
        Write(sb, "useScanCode", UseScanCode, true);
        Write(sb, "useInterception", UseInterception, true);
        Write(sb, "scrollSlowIntervalMs", ScrollSlowIntervalMs, true);
        Write(sb, "scrollFastIntervalMs", ScrollFastIntervalMs, true);
        Write(sb, "r3FreezeMs", R3FreezeMs, false);
        
        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }

    private static void Write(StringBuilder sb, string key, bool value, bool comma) {
        sb.Append("  \"").Append(key).Append("\": ").Append(value ? "true" : "false");
        if (comma) sb.Append(",");
        sb.AppendLine();
    }

    private static void Write(StringBuilder sb, string key, double value, bool comma) {
        sb.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
        if (comma) sb.Append(",");
        sb.AppendLine();
    }

    private static void Write(StringBuilder sb, string key, string value, bool comma) {
        sb.Append("  \"").Append(key).Append("\": \"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
        if (comma) sb.Append(",");
        sb.AppendLine();
    }

    private static void Write(StringBuilder sb, string key, int value, bool comma) {
        sb.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
        if (comma) sb.Append(",");
        sb.AppendLine();
    }

    private static bool GetBool(string text, string key, bool fallback) {
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
        return m.Success ? String.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase) : fallback;
    }

    private static int GetInt(string text, string key, int fallback) {
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+)");
        int value;
        return m.Success && Int32.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private static double GetDouble(string text, string key, double fallback) {
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)");
        double value;
        return m.Success && Double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }

    private static string GetString(string text, string key, string fallback) {
        Match m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : fallback;
    }
}

internal static class Logger {
    private static readonly object LockObj = new object();
    private static string _path = "logs\\shikipad.log";

    public static void Init(string root) {
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        _path = Path.Combine(root, "logs", "shikipad.log");
    }

    public static void Info(string message) { Write("INFO", message); }
    public static void Warn(string message) { Write("WARN", message); }
    public static void Error(string message) { Write("ERROR", message); }

    private static void Write(string level, string message) {
        lock (LockObj) {
            File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + " [" + level + "] " + message + Environment.NewLine);
        }
    }
}

internal sealed class MappingEngine {
    private readonly PhysicalKey[][] _tables;

    public MappingEngine() {
        _tables = new PhysicalKey[7][];
        _tables[(int)Layer.Base] = new PhysicalKey[] { PhysicalKey.ArrowUp, PhysicalKey.ArrowRight, PhysicalKey.Space, PhysicalKey.Backspace, PhysicalKey.ArrowLeft, PhysicalKey.ArrowDown, PhysicalKey.Enter, PhysicalKey.Tab };
        _tables[(int)Layer.R1] = new PhysicalKey[] { PhysicalKey.I, PhysicalKey.N, PhysicalKey.E, PhysicalKey.A, PhysicalKey.O, PhysicalKey.T, PhysicalKey.H, PhysicalKey.U };
        _tables[(int)Layer.L1] = new PhysicalKey[] { PhysicalKey.S, PhysicalKey.R, PhysicalKey.D, PhysicalKey.G, PhysicalKey.L, PhysicalKey.C, PhysicalKey.Y, PhysicalKey.Z };
        _tables[(int)Layer.R2] = new PhysicalKey[] { PhysicalKey.M, PhysicalKey.W, PhysicalKey.J, PhysicalKey.X, PhysicalKey.Q, PhysicalKey.F, PhysicalKey.P, PhysicalKey.B };
        _tables[(int)Layer.L2] = new PhysicalKey[] { PhysicalKey.K, PhysicalKey.V, PhysicalKey.Num1, PhysicalKey.Num2, PhysicalKey.Num3, PhysicalKey.Num4, PhysicalKey.Num5, PhysicalKey.Num6 };
        _tables[(int)Layer.R1R2] = new PhysicalKey[] { PhysicalKey.Num7, PhysicalKey.Num8, PhysicalKey.Num9, PhysicalKey.Num0, PhysicalKey.Minus, PhysicalKey.Equals, PhysicalKey.Comma, PhysicalKey.Period };
        _tables[(int)Layer.L1L2] = new PhysicalKey[] { PhysicalKey.Apostrophe, PhysicalKey.Slash, PhysicalKey.Semicolon, PhysicalKey.LeftBracket, PhysicalKey.RightBracket, PhysicalKey.Backslash, PhysicalKey.Grave, PhysicalKey.None };
    }

    public Layer Resolve(bool l1, bool r1, bool l2, bool r2, double l1Ms, double r1Ms, double l2Ms, double r2Ms, double comboLayerWindowMs) {
        if (!l1 && !r1 && !l2 && !r2) return Layer.Base;

        Layer layer = Layer.Reserved;
        double bestMs = double.NegativeInfinity;
        int bestRank = 0;
        double comboWindow = Math.Max(0.0, comboLayerWindowMs);

        ConsiderLayer(l1, Layer.L1, l1Ms, 1, ref layer, ref bestMs, ref bestRank);
        ConsiderLayer(r1, Layer.R1, r1Ms, 1, ref layer, ref bestMs, ref bestRank);
        ConsiderLayer(l2, Layer.L2, l2Ms, 1, ref layer, ref bestMs, ref bestRank);
        ConsiderLayer(r2, Layer.R2, r2Ms, 1, ref layer, ref bestMs, ref bestRank);
        ConsiderLayer(IsComboWithinWindow(r1, r2, r1Ms, r2Ms, comboWindow), Layer.R1R2, Math.Max(r1Ms, r2Ms), 2, ref layer, ref bestMs, ref bestRank);
        ConsiderLayer(IsComboWithinWindow(l1, l2, l1Ms, l2Ms, comboWindow), Layer.L1L2, Math.Max(l1Ms, l2Ms), 2, ref layer, ref bestMs, ref bestRank);

        return layer;
    }

    private static bool IsComboWithinWindow(bool a, bool b, double aMs, double bMs, double windowMs) {
        return a && b && Math.Abs(aMs - bMs) <= windowMs;
    }

    private static void ConsiderLayer(bool active, Layer candidate, double timestampMs, int rank, ref Layer layer, ref double bestMs, ref int bestRank) {
        if (!active) return;
        if (timestampMs > bestMs || (timestampMs == bestMs && rank >= bestRank)) {
            layer = candidate;
            bestMs = timestampMs;
            bestRank = rank;
        }
    }

    public PhysicalKey Lookup(Layer layer, ActionButton action) {
        if (layer == Layer.Reserved) return PhysicalKey.None;
        int li = (int)layer;
        int ai = (int)action;
        if (li < 0 || li >= _tables.Length || ai < 0 || ai >= 8) return PhysicalKey.None;
        return _tables[li][ai];
    }

    public static string KeyName(PhysicalKey key) {
        if (key >= PhysicalKey.Num0 && key <= PhysicalKey.Num9) return ((int)(key - PhysicalKey.Num0)).ToString(CultureInfo.InvariantCulture);
        return key.ToString();
    }

}

internal sealed class InputInjector {
    private struct KeyDef {
        public ushort Vk;
        public ushort Scan;
        public bool Extended;
    }

    private static readonly object s_instancesLock = new object();
    private static readonly List<InputInjector> s_instances = new List<InputInjector>();

    private readonly Dictionary<PhysicalKey, KeyDef> _keys = new Dictionary<PhysicalKey, KeyDef>();
    private readonly HashSet<PhysicalKey> _heldKeys = new HashSet<PhysicalKey>();
    private readonly object _heldLock = new object();
    private readonly KeyDef _shift;
    private readonly KeyDef _ctrl;
    private readonly KeyDef _alt;
    private readonly KeyDef _win;
    private readonly bool _useScanCode;
    private readonly bool _useInterception;
    private readonly bool _interceptionAvailable;
    private bool _leftMouseHeld;
    private bool _rightMouseHeld;

    public bool TraceInput;
    public bool TraceSendinput;
    public string CurrentSource = "Unknown";
    public string CurrentReason = "";

    public InputInjector(bool useScanCode, bool useInterception) {
        _useScanCode = useScanCode;
        _useInterception = useInterception;
        if (_useInterception) {
            _interceptionAvailable = InterceptionDriver.Initialize();
            if (_interceptionAvailable) {
                Logger.Info("Interception driver initialized successfully.");
            } else {
                Logger.Warn("Interception driver not found or failed to initialize. Falling back to SendInput.");
            }
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
            } catch {
            }
        }
    }


    public void KeyDown(PhysicalKey key) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        List<INPUT> inputs = new List<INPUT>();
        AddKey(inputs, _keys[key], false);
        Send(inputs, "KeyDown(" + key + ")");
        lock (_heldLock) _heldKeys.Add(key);
    }

    public void KeyUp(PhysicalKey key) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        List<INPUT> inputs = new List<INPUT>();
        AddKey(inputs, _keys[key], true);
        Send(inputs, "KeyUp(" + key + ")");
        lock (_heldLock) _heldKeys.Remove(key);
    }

    public void KeyTap(PhysicalKey key, bool shift, bool ctrl, bool alt, bool win) {
        if (key == PhysicalKey.None || !_keys.ContainsKey(key)) return;
        List<INPUT> inputs = new List<INPUT>();
        if (shift) AddKey(inputs, _shift, false);
        if (ctrl) AddKey(inputs, _ctrl, false);
        if (alt) AddKey(inputs, _alt, false);
        if (win) AddKey(inputs, _win, false);
        AddKey(inputs, _keys[key], false);
        AddKey(inputs, _keys[key], true);
        if (win) AddKey(inputs, _win, true);
        if (alt) AddKey(inputs, _alt, true);
        if (ctrl) AddKey(inputs, _ctrl, true);
        if (shift) AddKey(inputs, _shift, true);
        Send(inputs, "KeyTap(" + key + ")");
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
        INPUT input = new INPUT();
        input.type = INPUT_MOUSE;
        MOUSEINPUT mouse = new MOUSEINPUT();
        if (button == 0) mouse.dwFlags = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
        else mouse.dwFlags = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
        input.mi = mouse;
        List<INPUT> inputs = new List<INPUT>();
        inputs.Add(input);
        Send(inputs, "MouseButton(" + button + ", " + down + ")");
        lock (_heldLock) {
            if (button == 0) _leftMouseHeld = down;
            else _rightMouseHeld = down;
        }
    }

    public void MouseWheel(int delta) {
        INPUT input = new INPUT();
        input.type = INPUT_MOUSE;
        MOUSEINPUT mouse = new MOUSEINPUT();
        mouse.dwFlags = MOUSEEVENTF_WHEEL;
        mouse.mouseData = delta * WHEEL_DELTA;
        input.mi = mouse;
        List<INPUT> inputs = new List<INPUT>();
        inputs.Add(input);
        Send(inputs, "MouseWheel(" + delta + ")");
    }

    public void ReleaseAll() {
        List<INPUT> inputs = new List<INPUT>();
        lock (_heldLock) {
            foreach (PhysicalKey key in _heldKeys) {
                KeyDef def;
                if (_keys.TryGetValue(key, out def)) AddKey(inputs, def, true);
            }

            AddKey(inputs, _shift, true);
            AddKey(inputs, _ctrl, true);
            AddKey(inputs, _alt, true);
            AddKey(inputs, _win, true);

            if (_leftMouseHeld) AddMouseButton(inputs, 0, false);
            if (_rightMouseHeld) AddMouseButton(inputs, 1, false);

            _heldKeys.Clear();
            _leftMouseHeld = false;
            _rightMouseHeld = false;
        }
        Send(inputs, "ReleaseAll");
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

        // Always populate wScan so Interception can read it later in Send.
        keyboard.wScan = key.Scan;
        if (_useScanCode) {
            keyboard.wVk = 0;
            keyboard.dwFlags = KEYEVENTF_SCANCODE;
        } else {
            keyboard.wVk = key.Vk;
            keyboard.dwFlags = 0;
        }
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
        if (TraceInput || TraceSendinput) {
            string log = string.Format("[{0:HH:mm:ss.fff}] Source={1} Reason={2} Action={3}", DateTime.Now, CurrentSource, CurrentReason, actionType);
            Logger.Info(log);
            Console.WriteLine(log);
        }
        if (!TraceInput) {
            if (_useInterception && _interceptionAvailable) {
                foreach (var input in inputs) {
                    if (input.type == INPUT_KEYBOARD) {
                        InterceptionDriver.KeyState state;
                        if ((input.ki.dwFlags & KEYEVENTF_KEYUP) != 0) {
                            state = (input.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0 ? (InterceptionDriver.KeyState.E0 | InterceptionDriver.KeyState.Up) : InterceptionDriver.KeyState.Up;
                        } else {
                            state = (input.ki.dwFlags & KEYEVENTF_EXTENDEDKEY) != 0 ? (InterceptionDriver.KeyState.E0 | InterceptionDriver.KeyState.Down) : InterceptionDriver.KeyState.Down;
                        }
                        InterceptionDriver.SendKey(input.ki.wScan, state);
                    } else if (input.type == INPUT_MOUSE) {
                        if ((input.mi.dwFlags & MOUSEEVENTF_MOVE) != 0) {
                            InterceptionDriver.SendMouseDelta(input.mi.dx, input.mi.dy);
                        } else if ((input.mi.dwFlags & MOUSEEVENTF_WHEEL) != 0) {
                            InterceptionDriver.SendMouseWheel(input.mi.mouseData);
                        } else {
                            InterceptionDriver.MouseState state = 0;
                            if ((input.mi.dwFlags & MOUSEEVENTF_LEFTDOWN) != 0) state |= InterceptionDriver.MouseState.LeftButtonDown;
                            if ((input.mi.dwFlags & MOUSEEVENTF_LEFTUP) != 0) state |= InterceptionDriver.MouseState.LeftButtonUp;
                            if ((input.mi.dwFlags & MOUSEEVENTF_RIGHTDOWN) != 0) state |= InterceptionDriver.MouseState.RightButtonDown;
                            if ((input.mi.dwFlags & MOUSEEVENTF_RIGHTUP) != 0) state |= InterceptionDriver.MouseState.RightButtonUp;
                            if (state != 0) {
                                InterceptionDriver.SendMouse(state);
                            }
                        }
                    }
                }
            } else {
                SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            }
        }
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

    [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
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

internal sealed class ControllerState {
    public bool Connected;
    public double LX, LY, RX, RY, L2, R2;
    public bool Up, Right, Down, Left, Square, Triangle, Cross, Circle;
    public bool L1, R1, L3, R3, Options, Create;
    public bool TouchClick;
}

internal sealed class DirectHidController {
    public volatile ControllerState State = new ControllerState();
    private readonly ControllerProfile _profile;
    private Thread _thread;
    private volatile bool _running;
    private IntPtr _handle = IntPtr.Zero;
    private string _deviceName = "Sony Controller";
    private int _xinputUserIndex = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    public DirectHidController(ControllerProfile profile) {
        _profile = profile;
        _deviceName = DisplayName;
    }

    public string DisplayName {
        get {
            switch (_profile) {
                case ControllerProfile.Xbox360: return "Xbox 360 Controller / XInput";
                case ControllerProfile.XboxSeries: return "Xbox Series X|S Controller / XInput";
                default: return "DualSense / Direct HID";
            }
        }
    }

    public void Start() {
        _running = true;
        _thread = new Thread(Loop);
        _thread.IsBackground = true;
        _thread.Start();
    }

    public void Stop() {
        _running = false;
        if (_handle != IntPtr.Zero && _handle != new IntPtr(-1)) {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
        if (_thread != null) {
            _thread.Join(500);
        }
    }

    private void Loop() {
        if (_profile != ControllerProfile.DualSense) {
            XInputLoop();
            return;
        }

        byte[] buffer = new byte[1024];
        while (_running) {
            if (_handle == IntPtr.Zero || _handle == new IntPtr(-1)) {
                State = new ControllerState();
                _handle = FindAndOpenDevice();
                if (_handle != IntPtr.Zero && _handle != new IntPtr(-1)) {
                    ControllerState cs = new ControllerState();
                    cs.Connected = true;
                    State = cs;
                    Logger.Info("Direct HID device connected: " + _deviceName);
                } else {
                    Thread.Sleep(1000);
                    continue;
                }
            }

            uint bytesRead = 0;
            if (ReadFile(_handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero)) {
                if (bytesRead > 0) {
                    byte[] report = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, report, 0, (int)bytesRead);
                    try {
                        ParseReport(report);
                    } catch (Exception ex) {
                        Logger.Error("Parse error: " + ex.Message);
                    }
                }
            } else {
                Logger.Warn("ReadFile failed, disconnecting...");
                NativeMethods.CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }

    private void XInputLoop() {
        bool wasConnected = false;
        while (_running) {
            NativeMethods.XINPUT_STATE state;
            int result = XInputGetState(ref _xinputUserIndex, out state);
            if (result == 0) {
                if (!wasConnected) {
                    wasConnected = true;
                    Logger.Info("XInput controller connected: " + DisplayName + " slot " + _xinputUserIndex.ToString(CultureInfo.InvariantCulture));
                }
                ParseXInput(state.Gamepad);
                Thread.Sleep(1);
            } else {
                if (wasConnected) {
                    Logger.Warn("XInput controller disconnected");
                    wasConnected = false;
                }
                State = new ControllerState();
                _xinputUserIndex = -1;
                Thread.Sleep(1000);
            }
        }
    }

    private static int XInputGetState(ref int userIndex, out NativeMethods.XINPUT_STATE state) {
        state = new NativeMethods.XINPUT_STATE();
        if (userIndex >= 0) {
            int result = NativeMethods.XInputGetStateAny(userIndex, out state);
            if (result == 0) return 0;
            userIndex = -1;
        }

        for (int i = 0; i < 4; i++) {
            int result = NativeMethods.XInputGetStateAny(i, out state);
            if (result == 0) {
                userIndex = i;
                return 0;
            }
        }
        return 1167;
    }

    private void ParseXInput(NativeMethods.XINPUT_GAMEPAD gamepad) {
        State = ParseXInputState(gamepad);
    }

    internal static ControllerState ParseXInputState(NativeMethods.XINPUT_GAMEPAD gamepad) {
        ControllerState s = new ControllerState();
        s.Connected = true;
        ushort b = gamepad.wButtons;
        s.LX = Axis(gamepad.sThumbLX);
        s.LY = -Axis(gamepad.sThumbLY);
        s.RX = Axis(gamepad.sThumbRX);
        s.RY = -Axis(gamepad.sThumbRY);
        s.L2 = Trigger(gamepad.bLeftTrigger);
        s.R2 = Trigger(gamepad.bRightTrigger);

        s.Up = (b & NativeMethods.XINPUT_GAMEPAD_DPAD_UP) != 0;
        s.Down = (b & NativeMethods.XINPUT_GAMEPAD_DPAD_DOWN) != 0;
        s.Left = (b & NativeMethods.XINPUT_GAMEPAD_DPAD_LEFT) != 0;
        s.Right = (b & NativeMethods.XINPUT_GAMEPAD_DPAD_RIGHT) != 0;
        s.Square = (b & NativeMethods.XINPUT_GAMEPAD_X) != 0;
        s.Cross = (b & NativeMethods.XINPUT_GAMEPAD_A) != 0;
        s.Circle = (b & NativeMethods.XINPUT_GAMEPAD_B) != 0;
        s.Triangle = (b & NativeMethods.XINPUT_GAMEPAD_Y) != 0;
        s.L1 = (b & NativeMethods.XINPUT_GAMEPAD_LEFT_SHOULDER) != 0;
        s.R1 = (b & NativeMethods.XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0;
        s.L3 = (b & NativeMethods.XINPUT_GAMEPAD_LEFT_THUMB) != 0;
        s.R3 = (b & NativeMethods.XINPUT_GAMEPAD_RIGHT_THUMB) != 0;
        s.Create = (b & NativeMethods.XINPUT_GAMEPAD_BACK) != 0;
        s.Options = (b & NativeMethods.XINPUT_GAMEPAD_START) != 0;

        s.TouchClick = s.Create || s.Options;
        return s;
    }


    private IntPtr FindAndOpenDevice() {
        Guid hidGuid;
        NativeMethods.HidD_GetHidGuid(out hidGuid);

        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (deviceInfoSet == new IntPtr(-1)) return IntPtr.Zero;

        NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
        interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

        IntPtr foundHandle = IntPtr.Zero;
        uint index = 0;
        
        while (NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData)) {
            index++;
            uint requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
            
            if (requiredSize == 0) continue;
            
            IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
            Marshal.WriteInt32(detailData, (IntPtr.Size == 8) ? 8 : (Marshal.SystemDefaultCharSize == 1 ? 5 : 6));
            
            if (NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero)) {
                string devicePath = Marshal.PtrToStringAuto(new IntPtr(detailData.ToInt64() + 4));
                
                IntPtr handle = NativeMethods.CreateFile(devicePath, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); // GENERIC_READ, FILE_SHARE_READ|WRITE, OPEN_EXISTING
                if (handle != new IntPtr(-1)) {
                    NativeMethods.HIDD_ATTRIBUTES attrs = new NativeMethods.HIDD_ATTRIBUTES();
                    attrs.Size = (uint)Marshal.SizeOf(attrs);
                    if (NativeMethods.HidD_GetAttributes(handle, ref attrs)) {
                        if (attrs.VendorID == 0x054C) { // Sony
                            IntPtr prodStr = Marshal.AllocHGlobal(254);
                            string productName = "";
                            if (NativeMethods.HidD_GetProductString(handle, prodStr, 254)) {
                                productName = Marshal.PtrToStringAuto(prodStr);
                            }
                            Marshal.FreeHGlobal(prodStr);
                            _deviceName = String.IsNullOrEmpty(productName)
                                ? "Sony Controller"
                                : productName + " (PID 0x" + attrs.ProductID.ToString("X4", CultureInfo.InvariantCulture) + ")";
                            foundHandle = handle;
                            Marshal.FreeHGlobal(detailData);
                            break; 
                        }
                    }
                    NativeMethods.CloseHandle(handle);
                }
            }
            Marshal.FreeHGlobal(detailData);
        }
        
        NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        return foundHandle;
    }


    private void ParseReport(byte[] r) {
        ControllerState s;
        if (!TryParseDualSenseReport(r, out s)) return;
        State = s;  // Volatile publish: reference swap ensures state is visible atomically
    }

    internal static bool TryParseDualSenseReport(byte[] r, out ControllerState s) {
        s = null;
        if (r == null || r.Length < 10 || r[0] != 0x01) return false;

        s = new ControllerState();
        s.Connected = true;

        s.LX = Axis(r[1]);
        s.LY = Axis(r[2]);
        s.RX = Axis(r[3]);
        s.RY = Axis(r[4]);
        
        // Correct DualSense USB offsets:
        // r[5] = L2 Analog
        // r[6] = R2 Analog
        // r[7] = Sequence
        // r[8] = D-Pad (low 4 bits) and Face Buttons (high 4 bits)
        // r[9] = L1, R1, L2 Btn, R2 Btn, Share, Options, L3, R3
        
        s.L2 = Trigger(r[5]);
        s.R2 = Trigger(r[6]);
        
        FillDpadAndFace(s, r[8]);
        
        byte b2 = r[9];
        s.L1 = (b2 & 0x01) != 0;
        s.R1 = (b2 & 0x02) != 0;
        // bits 2 and 3 are digital L2/R2, but we use the analog values from r[5] and r[6]
        
        s.Create = (b2 & 0x10) != 0;
        s.Options = (b2 & 0x20) != 0;
        if (r.Length > 10) s.TouchClick = (r[10] & 0x02) != 0;
        s.L3 = (b2 & 0x40) != 0;
        s.R3 = (b2 & 0x80) != 0;

        return true;
    }

    private static void FillDpadAndFace(ControllerState s, byte b) {
        int d = b & 0x0F;
        s.Up = d == 0 || d == 1 || d == 7;
        s.Right = d == 1 || d == 2 || d == 3;
        s.Down = d == 3 || d == 4 || d == 5;
        s.Left = d == 5 || d == 6 || d == 7;
        s.Square = (b & 0x10) != 0;
        s.Cross = (b & 0x20) != 0;
        s.Circle = (b & 0x40) != 0;
        s.Triangle = (b & 0x80) != 0;
    }

    private static double Axis(byte value) { return Clamp(((double)value - 128.0) / 127.0, -1.0, 1.0); }
    private static double Axis(short value) {
        return value < 0
            ? Clamp((double)value / 32768.0, -1.0, 0.0)
            : Clamp((double)value / 32767.0, 0.0, 1.0);
    }
    private static double Trigger(byte value) { return Clamp((double)value / 255.0, 0.0, 1.0); }
    private static double Clamp(double value, double min, double max) { return value < min ? min : (value > max ? max : value); }



internal static class NativeMethods {
        [DllImport("hid.dll", SetLastError = true)] public static extern void HidD_GetHidGuid(out Guid hidGuid);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);
        [DllImport("setupapi.dll", SetLastError = true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid, uint MemberIndex, ref NativeMethods.SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr DeviceInfoSet, ref NativeMethods.SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, IntPtr DeviceInterfaceDetailData, uint DeviceInterfaceDetailDataSize, out uint RequiredSize, IntPtr DeviceInfoData);
        [DllImport("setupapi.dll", SetLastError = true)] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr hObject);
        [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetAttributes(IntPtr device, ref NativeMethods.HIDD_ATTRIBUTES attributes);
        [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Auto)] public static extern bool HidD_GetProductString(IntPtr hidDeviceObject, IntPtr buffer, uint bufferLength);

        public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
        public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
        public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
        public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
        public const ushort XINPUT_GAMEPAD_START = 0x0010;
        public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
        public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
        public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
        public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
        public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        public const ushort XINPUT_GAMEPAD_A = 0x1000;
        public const ushort XINPUT_GAMEPAD_B = 0x2000;
        public const ushort XINPUT_GAMEPAD_X = 0x4000;
        public const ushort XINPUT_GAMEPAD_Y = 0x8000;

        public static int XInputGetStateAny(int userIndex, out XINPUT_STATE state) {
            try {
                return XInputGetState14(userIndex, out state);
            } catch (DllNotFoundException) {
                try { return XInputGetState910(userIndex, out state); }
                catch (DllNotFoundException) { state = new XINPUT_STATE(); return 1167; }
                catch (EntryPointNotFoundException) { state = new XINPUT_STATE(); return 1167; }
            } catch (EntryPointNotFoundException) {
                try { return XInputGetState910(userIndex, out state); }
                catch (DllNotFoundException) { state = new XINPUT_STATE(); return 1167; }
                catch (EntryPointNotFoundException) { state = new XINPUT_STATE(); return 1167; }
            }
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState910(int dwUserIndex, out XINPUT_STATE pState);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public uint flags;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_GAMEPAD {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }
    }

internal sealed class MapperForm : Form {
    private readonly DirectHidController _hid;
    private readonly Config _config;
    private readonly ControllerProfile _controllerProfile;
    private readonly InputInjector _injector;
    private readonly MappingEngine _mapping = new MappingEngine();
    
    private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ButtonHold[] _holds = new ButtonHold[8];
    private readonly bool[] _prevDown = new bool[8];
    private bool _debugSources;
    private bool _enabled;
    private bool _runtimeReleased = true;
    private bool _printedConnectedGuide;
    private bool _l2Pressed;
    private bool _r2Pressed;
    private StickDirection _leftDirection = StickDirection.None;
    private double _scrollNextMs;
    private double _rightNeutralX;
    private double _rightNeutralY;
    private double _mouseAccumX;
    private double _mouseAccumY;
    private double _mouseFreezeUntilMs;
    private bool _leftMouseDown;
    private bool _rightMouseDown;
    private List<PhysicalKey> _accumulatedModifiers = new List<PhysicalKey>();
    private List<PhysicalKey> _heldLeftStickKeys = new List<PhysicalKey>();
    private List<PhysicalKey> _activeFnKeys = new List<PhysicalKey>();
    private bool _prevTouchClick;
    private double _disableStartMs;
    private bool _disableArmed = true;
    private double _lastTickMs;

    public MapperForm(Config config, ControllerProfile controllerProfile, bool debugSources, bool traceInput, bool traceSendinput) {
        _config = config;
        _controllerProfile = controllerProfile;
        _hid = new DirectHidController(controllerProfile);
        _debugSources = debugSources;
        _enabled = config.Enabled;
        _injector = new InputInjector(config.UseScanCode, config.UseInterception);
        _injector.TraceInput = traceInput;
        _injector.TraceSendinput = traceSendinput;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
        _timer.Interval = 1;
        _timer.Tick += OnTick;
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        _hid.Start();
        int parentId = 0;
        try {
            var pc = new System.Diagnostics.PerformanceCounter("Process", "Creating Process ID", Process.GetCurrentProcess().ProcessName);
            parentId = (int)pc.NextValue();
        } catch { }
        Program.PrintRuntimeStatus(Process.GetCurrentProcess().MainModule.FileName, Process.GetCurrentProcess().Id, parentId, _hid.DisplayName, true);
        
        _timer.Start();
        _lastTickMs = NowMs();
    }



    protected override void OnFormClosing(FormClosingEventArgs e) {
        _hid.Stop();
        ReleaseRuntimeHolds();
        
        Logger.Info("shutdown");
        base.OnFormClosing(e);
    }

    private bool _prevL1, _prevR1;
    private double _l1DownMs, _r1DownMs, _l2DownMs, _r2DownMs;

    private void OnTick(object sender, EventArgs e) {
        ControllerState s = _hid.State;
        double now = NowMs();
        double deltaSec = Math.Max(0.0, (now - _lastTickMs) / 1000.0);
        _lastTickMs = now;
        
        bool preL1 = _prevL1;
        bool preR1 = _prevR1;
        bool l1JustDown = s.L1 && !preL1;
        bool r1JustDown = s.R1 && !preR1;
        if (l1JustDown) _l1DownMs = now;
        if (r1JustDown) _r1DownMs = now;
        _prevL1 = s.L1;
        _prevR1 = s.R1;

        UpdateEmergency(s, now);
        if (!s.Connected || !_enabled) {
            if (!s.Connected) _printedConnectedGuide = false;
            if (!_runtimeReleased) {
                ReleaseRuntimeHolds();
                _runtimeReleased = true;
            }
            return;
        }
        _runtimeReleased = false;
        if (!_printedConnectedGuide) {
            Program.PrintControllerGuide(_controllerProfile, _hid.DisplayName, _config);
            _printedConnectedGuide = true;
        }
        UpdateTriggers(s, now);

        UpdateLeftStick(s, now);
        UpdateActionButtons(s, now);
        UpdateMouseButtons(s, now);
        UpdateRightStick(s, now, deltaSec);
    }

    private void UpdateTriggers(ControllerState s, double now) {
        if (!_l2Pressed && s.L2 > _config.TriggerPressThreshold) {
            _l2Pressed = true;
            _l2DownMs = now;
        } else if (_l2Pressed && s.L2 < _config.TriggerReleaseThreshold) _l2Pressed = false;
        
        if (!_r2Pressed && s.R2 > _config.TriggerPressThreshold) {
            _r2Pressed = true;
            _r2DownMs = now;
        } else if (_r2Pressed && s.R2 < _config.TriggerReleaseThreshold) _r2Pressed = false;
    }

    private PhysicalKey GetLeftStickKey(StickDirection dir) {
        switch (dir) {
            case StickDirection.Right: return PhysicalKey.LWin;
            case StickDirection.DownRight: return PhysicalKey.LAlt;
            case StickDirection.DownLeft: return PhysicalKey.LCtrl;
            case StickDirection.Left: return PhysicalKey.LShift;
            case StickDirection.UpLeft: return PhysicalKey.Escape;
            default: return PhysicalKey.None;
        }
    }

    private void UpdateLeftStick(ControllerState s, double now) {
        double radius = Math.Sqrt(s.LX * s.LX + s.LY * s.LY);
        StickDirection previous = _leftDirection;
        StickDirection next = previous;

        if (previous == StickDirection.None) {
            if (radius >= _config.LeftStickEnterDeadzone) {
                next = Sector(s.LX, s.LY);
            }
        } else if (radius < _config.LeftStickExitDeadzone) {
            next = StickDirection.None;
        } else {
            next = previous;
        }

        if (next != previous) {
            _leftDirection = next;
            _scrollNextMs = 0;
        }

        bool touchJustPressed = s.TouchClick && !_prevTouchClick;
        _prevTouchClick = s.TouchClick;

        List<PhysicalKey> desiredKeys = new List<PhysicalKey>();

        if (touchJustPressed) {
            foreach (var key in _heldLeftStickKeys) {
                AccumulateLeftStickKey(key);
            }
            _activeFnKeys.Clear();
        }

        if (_leftDirection != StickDirection.None && _leftDirection != StickDirection.Up && _leftDirection != StickDirection.Down) {
            PhysicalKey rawStickKey = GetLeftStickKey(_leftDirection);
            if (s.TouchClick) {
                AccumulateLeftStickKey(rawStickKey);
                desiredKeys.AddRange(_accumulatedModifiers);
            } else {
                if (_accumulatedModifiers.Count > 0) {
                    AccumulateLeftStickKey(rawStickKey);
                    desiredKeys.AddRange(_accumulatedModifiers);
                } else {
                    AddUnique(desiredKeys, rawStickKey);
                    foreach (var key in _activeFnKeys) AddUnique(desiredKeys, key);
                }
            }
        } else {
            if (s.TouchClick) {
                desiredKeys.AddRange(_accumulatedModifiers);
            } else {
                _accumulatedModifiers.Clear();
                _activeFnKeys.Clear();
            }
        }

        foreach (var key in _heldLeftStickKeys) {
            if (!desiredKeys.Contains(key)) {
                _injector.CurrentSource = "LeftStick";
                _injector.CurrentReason = "ModifierUp " + key;
                _injector.KeyUp(key);
            }
        }
        foreach (var key in desiredKeys) {
            if (!_heldLeftStickKeys.Contains(key)) {
                _injector.CurrentSource = "LeftStick";
                _injector.CurrentReason = "ModifierDown " + key;
                _injector.KeyDown(key);
            }
        }
        
        _heldLeftStickKeys.Clear();
        _heldLeftStickKeys.AddRange(desiredKeys);

        if (_leftDirection != StickDirection.Up && _leftDirection != StickDirection.Down) {
            _scrollNextMs = 0;
            return;
        }
        if (now < _scrollNextMs) return;

        _injector.CurrentSource = "LeftStick";
        _injector.CurrentReason = "RepeatTimer " + _leftDirection;
        _injector.MouseWheel(_leftDirection == StickDirection.Up ? 1 : -1);
        
        double normalized = Clamp((radius - _config.LeftStickEnterDeadzone) / (1.0 - _config.LeftStickEnterDeadzone), 0.0, 1.0);
        double interval = _config.ScrollSlowIntervalMs + (_config.ScrollFastIntervalMs - _config.ScrollSlowIntervalMs) * normalized;
        _scrollNextMs = now + Math.Max(1.0, interval);
    }
    private PhysicalKey TranslateToFKey(PhysicalKey numberKey) {
        switch (numberKey) {
            case PhysicalKey.Num1: return PhysicalKey.F1;
            case PhysicalKey.Num2: return PhysicalKey.F2;
            case PhysicalKey.Num3: return PhysicalKey.F3;
            case PhysicalKey.Num4: return PhysicalKey.F4;
            case PhysicalKey.Num5: return PhysicalKey.F5;
            case PhysicalKey.Num6: return PhysicalKey.F6;
            case PhysicalKey.Num7: return PhysicalKey.F7;
            case PhysicalKey.Num8: return PhysicalKey.F8;
            case PhysicalKey.Num9: return PhysicalKey.F9;
            case PhysicalKey.Num0: return PhysicalKey.F10;
            case PhysicalKey.Minus: return PhysicalKey.F11;
            case PhysicalKey.Equals: return PhysicalKey.F12;
            default: return PhysicalKey.None;
        }
    }

    private PhysicalKey ApplyFnLayer(PhysicalKey key) {
        if (_leftDirection != StickDirection.UpRight) return key;
        return TranslateToFKey(key);
    }

    private static bool IsFunctionKey(PhysicalKey key) {
        return key >= PhysicalKey.F1 && key <= PhysicalKey.F12;
    }

    private static void AddUnique(List<PhysicalKey> keys, PhysicalKey key) {
        if (key != PhysicalKey.None && !keys.Contains(key)) {
            keys.Add(key);
        }
    }

    private void AccumulateLeftStickKey(PhysicalKey key) {
        AddUnique(_accumulatedModifiers, key);
    }

    private void ActivateFnKey(PhysicalKey key, bool touchDown) {
        if (!IsFunctionKey(key)) return;

        if (touchDown) {
            AccumulateLeftStickKey(key);
            return;
        }

        if (!_activeFnKeys.Contains(key)) {
            _activeFnKeys.Add(key);
        }

        if (!_heldLeftStickKeys.Contains(key)) {
            _injector.CurrentSource = "LeftStickFn";
            _injector.CurrentReason = "Fn " + key;
            _injector.KeyDown(key);
            _heldLeftStickKeys.Add(key);
        }
    }

    private void UpdateActionButtons(ControllerState s, double now) {
        bool[] currentDown = new bool[] { s.Up, s.Right, s.Square, s.Triangle, s.Left, s.Down, s.Cross, s.Circle };
        Layer layer = _mapping.Resolve(s.L1, s.R1, _l2Pressed, _r2Pressed, _l1DownMs, _r1DownMs, _l2DownMs, _r2DownMs, _config.ComboLayerWindowMs);
        double layerMs = LayerTimestamp(layer);

        for (int i = 0; i < 8; i++) {
            bool prev = _prevDown[i];
            bool curr = currentDown[i];
            ButtonHold hold = _holds[i];
            bool touchChargingFn = s.TouchClick && _leftDirection == StickDirection.UpRight;
            PhysicalKey layerKey = ApplyFnLayer(_mapping.Lookup(layer, (ActionButton)i));

            if (hold.Pending) {
                if (!curr && !hold.PendingReleased) {
                    hold.PendingReleased = true;
                }
                UpdatePendingLayer(ref hold, layer, layerMs);

                bool shouldFlushPending = now - hold.PendingSinceMs >= _config.ActionLayerGraceMs;
                if (!shouldFlushPending) {
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                bool releasedPending = hold.PendingReleased || !curr;
                Layer resolvedLayer = hold.PendingLayer != Layer.Base && hold.PendingLayer != Layer.Reserved
                    ? hold.PendingLayer
                    : (releasedPending ? hold.PendingLayer : layer);
                PhysicalKey resolvedLayerKey = ApplyFnLayer(_mapping.Lookup(resolvedLayer, (ActionButton)i));
                if (IsFunctionKey(resolvedLayerKey)) {
                    ActivateFnKey(resolvedLayerKey, s.TouchClick);
                    if (!releasedPending) {
                        hold.Key = resolvedLayerKey;
                        hold.KeyLayer = resolvedLayer;
                        hold.KeyIsDown = false;
                        hold.SuppressUntilRelease = true;
                        hold.Pending = false;
                        hold.PendingReleased = false;
                        _holds[i] = hold;
                    } else {
                        _holds[i] = new ButtonHold();
                    }
                    _prevDown[i] = curr;
                    continue;
                } else if (resolvedLayerKey != PhysicalKey.None) {
                    if (resolvedLayer != Layer.Base) {
                        TapActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i) + " virtual tap");
                        if (!releasedPending) {
                            hold.Pending = false;
                            hold.PendingReleased = false;
                            hold.Key = resolvedLayerKey;
                            hold.KeyLayer = resolvedLayer;
                            hold.SuppressUntilRelease = true;
                            _holds[i] = hold;
                        } else {
                            _holds[i] = new ButtonHold();
                        }
                        _prevDown[i] = curr;
                        continue;
                    }

                    if (releasedPending) {
                        hold.Pending = false;
                        hold.PendingReleased = false;
                        PressActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i), ref hold, resolvedLayer, false, now);
                        ReleaseActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i) + " release after layer settle");
                        _holds[i] = new ButtonHold();
                        _prevDown[i] = curr;
                        continue;
                    }

                    hold.Pending = false;
                    hold.PendingReleased = false;
                    PressActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i), ref hold, resolvedLayer, resolvedLayer == Layer.Base, now);
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                _holds[i] = new ButtonHold();
                _prevDown[i] = curr;
                continue;
            }

            if (!prev && curr) {
                PhysicalKey key = layerKey;
                if (ShouldDeferInitialAction()) {
                    hold = new ButtonHold();
                    hold.Pending = true;
                    hold.PendingLayer = layer;
                    hold.PendingLayerMs = layerMs;
                    hold.PendingSinceMs = now;
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                if (IsFunctionKey(key)) {
                    ActivateFnKey(key, s.TouchClick);
                    hold.Key = key;
                    hold.KeyIsDown = false;
                    hold.SuppressUntilRelease = true;
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }
                
                hold.Key = key;
                hold.KeyIsDown = false;

                if (key != PhysicalKey.None) {
                    PressActionKey(i, key, "Button " + ActionButtonName(i), ref hold, layer, layer == Layer.Base, now);
                }
                
                _holds[i] = hold;
            } else if (prev && !curr) {
                // 1 -> 0: KeyUp edge
                if (hold.KeyIsDown) {
                    ReleaseActionKey(i, hold.Key, "Button " + ActionButtonName(i) + " release");
                }
                _holds[i] = new ButtonHold();
            } else if (prev && curr) {
                if (hold.SuppressUntilRelease) {
                    PhysicalKey key = layerKey;
                    if (touchChargingFn && IsFunctionKey(key)) {
                        AccumulateLeftStickKey(key);
                    }
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                PhysicalKey currentLayerKey = layerKey;
                
                if (hold.Key != currentLayerKey) {
                    if (hold.KeyLayer == Layer.Base && layer != Layer.Base) {
                        if (hold.KeyIsDown) {
                            ReleaseActionKey(i, hold.Key, "Button " + ActionButtonName(i) + " base release before layer change");
                        }
                        hold.Key = PhysicalKey.None;
                        hold.KeyIsDown = false;
                        hold.RepeatEnabled = false;
                        hold.SuppressUntilRelease = true;
                        _holds[i] = hold;
                        _prevDown[i] = curr;
                        continue;
                    }

                    if (hold.KeyIsDown && ShouldSuppressLayerChangeDuringCharacterTap(hold, layer, now)) {
                        ReleaseActionKey(i, hold.Key, "Button " + ActionButtonName(i) + " layer change suppress tap residue");
                        hold.Key = PhysicalKey.None;
                        hold.KeyIsDown = false;
                        hold.RepeatEnabled = false;
                        hold.SuppressUntilRelease = true;
                        _holds[i] = hold;
                        _prevDown[i] = curr;
                        continue;
                    }

                    if (hold.KeyIsDown) {
                        ReleaseActionKey(i, hold.Key, "Button " + ActionButtonName(i) + " layer change release");
                        hold.KeyIsDown = false;
                    }

                    if (IsFunctionKey(currentLayerKey)) {
                        ActivateFnKey(currentLayerKey, s.TouchClick);
                        hold.Key = currentLayerKey;
                        hold.KeyLayer = layer;
                        hold.SuppressUntilRelease = true;
                        _holds[i] = hold;
                        _prevDown[i] = curr;
                        continue;
                    }

                    if (layer != Layer.Base && currentLayerKey != PhysicalKey.None) {
                        TapActionKey(i, currentLayerKey, "Button " + ActionButtonName(i) + " layer change virtual tap");
                        hold.Key = currentLayerKey;
                        hold.KeyLayer = layer;
                        hold.KeyIsDown = false;
                        hold.RepeatEnabled = false;
                        hold.SuppressUntilRelease = true;
                        _holds[i] = hold;
                        _prevDown[i] = curr;
                        continue;
                    }

                    if (currentLayerKey != PhysicalKey.None) {
                        PressActionKey(i, currentLayerKey, "Button " + ActionButtonName(i) + " layer change press", ref hold, layer, layer == Layer.Base, now);
                    }

                    hold.Key = currentLayerKey;
                    _holds[i] = hold;
                } else {
                    UpdateBaseRepeat(i, ref hold, now);
                    _holds[i] = hold;
                }
            }

            _prevDown[i] = curr;
        }
    }

    private bool ShouldDeferInitialAction() {
        return _config.ActionLayerGraceMs > 0;
    }

    private double LayerTimestamp(Layer layer) {
        switch (layer) {
            case Layer.L1: return _l1DownMs;
            case Layer.R1: return _r1DownMs;
            case Layer.L2: return _l2DownMs;
            case Layer.R2: return _r2DownMs;
            case Layer.R1R2: return Math.Max(_r1DownMs, _r2DownMs);
            case Layer.L1L2: return Math.Max(_l1DownMs, _l2DownMs);
            default: return 0.0;
        }
    }

    private void UpdatePendingLayer(ref ButtonHold hold, Layer layer, double layerMs) {
        Layer next = ResolvePendingLayer(
            hold.PendingLayer,
            hold.PendingLayerMs,
            hold.PendingSinceMs,
            layer,
            layerMs,
            _config.LayerTakeoverWindowMs);

        if (next == hold.PendingLayer) return;
        hold.PendingLayer = next;
        hold.PendingLayerMs = layerMs;
    }

    internal static Layer ResolvePendingLayer(Layer pendingLayer, double pendingLayerMs, double pendingSinceMs, Layer layer, double layerMs, double takeoverWindowMs) {
        if (layer == Layer.Base || layer == Layer.Reserved) return pendingLayer;
        if (layer == pendingLayer) return pendingLayer;
        if (layerMs < pendingSinceMs) return pendingLayer;
        if (layerMs - pendingSinceMs > takeoverWindowMs) return pendingLayer;

        bool pendingCombo = IsComboLayer(pendingLayer);
        bool layerCombo = IsComboLayer(layer);
        if (pendingCombo && !layerCombo) return pendingLayer;

        bool pendingSingle = pendingLayer != Layer.Base && pendingLayer != Layer.Reserved && !pendingCombo;
        if (pendingSingle && !layerCombo) {
            if (layerMs <= pendingLayerMs) return pendingLayer;
        }

        return layer;
    }

    private bool ShouldSuppressLayerChangeDuringCharacterTap(ButtonHold hold, Layer newLayer, double now) {
        if (hold.KeyLayer == Layer.Base) return false;
        if (IsComboLayer(hold.KeyLayer) && hold.KeyLayer != newLayer) return true;
        if (newLayer == Layer.Base) return true;
        if (hold.KeyLayer == newLayer) return false;
        return now - hold.KeyDownMs <= _config.ActionLayerSwitchGuardMs;
    }

    private static bool IsComboLayer(Layer layer) {
        return layer == Layer.R1R2 || layer == Layer.L1L2;
    }

    private void PressActionKey(int index, PhysicalKey key, string reason, ref ButtonHold hold, Layer keyLayer, bool repeatable, double now) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Held -> " + MappingEngine.KeyName(key) + "Down");
        _injector.KeyDown(key);
        hold.Key = key;
        hold.KeyLayer = keyLayer;
        hold.KeyIsDown = true;
        hold.RepeatEnabled = repeatable;
        hold.KeyDownMs = now;
        hold.RepeatStartedMs = now;
        hold.NextRepeatMs = now + Math.Max(1, _config.RepeatDelayMs);
    }

    private void TapActionKey(int index, PhysicalKey key, string reason) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Tap -> " + MappingEngine.KeyName(key));
        _injector.KeyDown(key);
        _injector.CurrentReason = reason + " release";
        _injector.KeyUp(key);
    }

    private void ReleaseActionKey(int index, PhysicalKey key, string reason) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Held -> " + MappingEngine.KeyName(key) + "Up");
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        _injector.KeyUp(key);
    }

    private void UpdateBaseRepeat(int index, ref ButtonHold hold, double now) {
        if (!hold.RepeatEnabled || !hold.KeyIsDown || hold.Key == PhysicalKey.None) return;
        if (now < hold.NextRepeatMs) return;

        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        _injector.CurrentSource = source;
        _injector.CurrentReason = "Button " + btn + " progressive repeat";
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Repeat -> " + MappingEngine.KeyName(hold.Key) + "Down");
        _injector.KeyDown(hold.Key);

        double heldMs = Math.Max(0.0, now - hold.RepeatStartedMs);
        double interval = BaseRepeatIntervalMs(heldMs);
        hold.NextRepeatMs = now + interval;
    }

    private double BaseRepeatIntervalMs(double heldMs) {
        double fast = Math.Max(5.0, _config.RepeatIntervalMs);
        double slow = Math.Max(fast, _config.BaseRepeatSlowIntervalMs);
        double ramp = Math.Max(1.0, _config.BaseRepeatRampMs);
        double t = Clamp((heldMs - _config.RepeatDelayMs) / ramp, 0.0, 1.0);
        double eased = t * t * (3.0 - 2.0 * t);
        return slow + (fast - slow) * eased;
    }

    private static string ActionSource(int index) {
        return (index < 2 || index == 4 || index == 5) ? "DPad" : "FaceButton";
    }

    private static string ActionButtonName(int index) {
        return ((ActionButton)index).ToString();
    }

    private void UpdateMouseButtons(ControllerState s, double now) {
        if (s.L3 && !_leftMouseDown) {
            _injector.CurrentSource = "StickClick";
            _injector.CurrentReason = "L3";
            _injector.MouseButton(0, true);
            _leftMouseDown = true;
        } else if (!s.L3 && _leftMouseDown) {
            _injector.CurrentSource = "StickClick";
            _injector.CurrentReason = "L3 release";
            _injector.MouseButton(0, false);
            _leftMouseDown = false;
        }
        if (s.R3 && !_rightMouseDown) {
            _injector.CurrentSource = "StickClick";
            _injector.CurrentReason = "R3";
            _injector.MouseButton(1, true);
            _rightMouseDown = true;
            _mouseFreezeUntilMs = now + _config.R3FreezeMs;
        } else if (!s.R3 && _rightMouseDown) {
            _injector.CurrentSource = "StickClick";
            _injector.CurrentReason = "R3 release";
            _injector.MouseButton(1, false);
            _rightMouseDown = false;
        }
    }

    private void UpdateRightStick(ControllerState s, double now, double deltaSec) {
        double cx = s.RX - _rightNeutralX;
        double cy = s.RY - _rightNeutralY;
        if (Math.Abs(cx) < _config.RightStickDeadzone && Math.Abs(cy) < _config.RightStickDeadzone) {
            _rightNeutralX = _rightNeutralX * 0.995 + s.RX * 0.005;
            _rightNeutralY = _rightNeutralY * 0.995 + s.RY * 0.005;
        }
        if (now < _mouseFreezeUntilMs) return;

        double actualRadius = Math.Sqrt(cx * cx + cy * cy);
        double radius = Clamp(actualRadius, 0.0, 1.0);
        if (radius <= _config.RightStickDeadzone) {
            return;
        }

        double normalizedRadius = (radius - _config.RightStickDeadzone) / (1.0 - _config.RightStickDeadzone);

        double dirX = cx / actualRadius;
        double dirY = cy / actualRadius;
        double speedRatio = Math.Pow(normalizedRadius, _config.RightStickCurveExponent);
        double speed = _config.MouseMaxSpeed * deltaSec * 120.0 * _config.MouseSensitivity;
        double dx = dirX * speedRatio * speed;
        double dy = dirY * speedRatio * speed;
        if (Math.Abs(dx) + Math.Abs(dy) < 0.000001) return;
        _mouseAccumX += dx;
        _mouseAccumY += dy;
        int ix = (int)_mouseAccumX;
        int iy = (int)_mouseAccumY;
        if (ix != 0 || iy != 0) {
            _injector.CurrentSource = "RightStick";
            _injector.CurrentReason = "Mouse Move";
            _injector.MouseMove(ix, iy);
            _mouseAccumX -= ix;
            _mouseAccumY -= iy;
        }
    }

    private void UpdateEmergency(ControllerState s, double now) {
        bool held = s.Options && s.Create;
        if (!held) {
            _disableStartMs = 0;
            _disableArmed = true;
            return;
        }
        if (_disableStartMs <= 0) {
            _disableStartMs = now;
            return;
        }
        if (_disableArmed && now - _disableStartMs >= 2000.0) {
            _enabled = !_enabled;
            _disableArmed = false;
            Logger.Info(_enabled ? "enabled" : "disabled");
        }
    }

    private void ReleaseRuntimeHolds() {
        ReleaseHeldActionKeys();
        _injector.ReleaseAll();
        _leftMouseDown = false;
        _rightMouseDown = false;
        _leftDirection = StickDirection.None;
        _scrollNextMs = 0;
        _heldLeftStickKeys.Clear();
        _accumulatedModifiers.Clear();
        _activeFnKeys.Clear();
        for (int i = 0; i < _holds.Length; i++) _holds[i] = new ButtonHold();
        for (int i = 0; i < _prevDown.Length; i++) _prevDown[i] = false;
        _prevL1 = false;
        _prevR1 = false;
        _l1DownMs = 0;
        _r1DownMs = 0;
        _l2DownMs = 0;
        _r2DownMs = 0;
        _l2Pressed = false;
        _r2Pressed = false;
        _prevTouchClick = false;
        _mouseFreezeUntilMs = 0;
        _mouseAccumX = 0;
        _mouseAccumY = 0;
    }

    private void ReleaseHeldActionKeys() {
        for (int i = 0; i < _holds.Length; i++) {
            if (_holds[i].KeyIsDown) {
                _injector.CurrentSource = "Release";
                _injector.CurrentReason = "Runtime release " + ActionButtonName(i);
                _injector.KeyUp(_holds[i].Key);
            }
            _holds[i] = new ButtonHold();
        }
    }

    private double NowMs() { return _clock.Elapsed.TotalMilliseconds; }

    public static StickDirection Sector(double x, double y) {
        double angle = Math.Atan2(-y, x);
        int sector = (int)Math.Floor((angle + Math.PI / 8.0) / (Math.PI / 4.0));
        sector = ((sector % 8) + 8) % 8;
        switch (sector) {
            case 0: return StickDirection.Right;
            case 1: return StickDirection.UpRight;
            case 2: return StickDirection.Up;
            case 3: return StickDirection.UpLeft;
            case 4: return StickDirection.Left;
            case 5: return StickDirection.DownLeft;
            case 6: return StickDirection.Down;
            case 7: return StickDirection.DownRight;
            default: return StickDirection.None;
        }
    }


    private void DebugSources(string message) {
        if (!_debugSources) return;
        Logger.Info(message);
        Console.WriteLine(message);
    }


    private static double Clamp(double value, double min, double max) { return value < min ? min : (value > max ? max : value); }

    private struct ButtonHold {
        public PhysicalKey Key;
        public Layer KeyLayer;
        public bool KeyIsDown;
        public bool SuppressUntilRelease;
        public bool Pending;
        public Layer PendingLayer;
        public double PendingLayerMs;
        public bool PendingReleased;
        public double PendingSinceMs;
        public double KeyDownMs;
        public bool RepeatEnabled;
        public double RepeatStartedMs;
        public double NextRepeatMs;
    }
}

internal static class Program {

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    public static void PrintGradientBanner() {
        EnableAnsi();

        int width = GetConsoleWidth();
        int panelWidth = Math.Min(104, Math.Max(66, width - 6));
        bool zh = IsChineseUi();

        string[] logo = BuildShikiPadLogo();
        Console.WriteLine();
        WriteNeonRule(width, panelWidth, zh ? "ShikiPad \u63a7\u5236\u754c\u9762" : "ShikiPad Control Surface");
        WriteSeasonRail(width, panelWidth, zh);
        WriteReadyLine(width, panelWidth, zh);
        WriteLogoHalo(width, panelWidth, true, zh);
        WriteExtrudedLogo(width, logo, SeasonFlowStops());
        WriteLogoHalo(width, panelWidth, false, zh);
        WritePixelSubline(width, panelWidth, zh);
        WriteStatusCard(width, panelWidth, zh);
        WriteSeasonDivider(width, panelWidth, zh);
        Console.WriteLine("\x1b[0m");
    }

    public static void PrintRunHint() {
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(104, Math.Max(66, width - 6));
        WriteLiveStatusBar(width, panelWidth, IsChineseUi());
        Console.WriteLine("\x1b[0m");
    }

    public static void PrintRuntimeStatus(string processPath, int processId, int parentId, string backend, bool readsController) {
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(104, Math.Max(66, width - 6));
        string fileName = Path.GetFileName(processPath);
        bool zh = IsChineseUi();

        Console.WriteLine();
        WriteSeasonPanelBorder(width, panelWidth, true);
        WriteSeasonPanelTitle(width, panelWidth, zh ? "\u25c7 \u8fd0\u884c\u72b6\u6001 \u25c7" : "\u25c7 RUNTIME STATUS \u25c7");
        WriteSeasonPanelSeparator(width, panelWidth);
        WritePanelLine(width, panelWidth, zh ? "  \u8fdb\u7a0b" : "  Process", fileName + "  PID " + processId.ToString(CultureInfo.InvariantCulture), SeasonGold(), new Rgb(222, 238, 244));
        WritePanelLine(width, panelWidth, zh ? "  \u7236\u8fdb\u7a0b" : "  Parent", parentId.ToString(CultureInfo.InvariantCulture), SeasonSummer(), new Rgb(222, 238, 244));
        WritePanelLine(width, panelWidth, zh ? "  \u624b\u67c4\u540e\u7aef" : "  Controller backend", backend, SeasonSpring(), new Rgb(222, 238, 244));
        WritePanelLine(width, panelWidth, zh ? "  \u624b\u67c4\u8bfb\u53d6" : "  Controller read", readsController ? (zh ? "\u672c\u8fdb\u7a0b\u6d3b\u8dc3" : "active in this process") : (zh ? "\u672a\u6d3b\u8dc3" : "inactive"), SeasonAutumn(), new Rgb(222, 238, 244));
        WritePanelLine(width, panelWidth, zh ? "  \u8def\u5f84" : "  Path", ShortenPath(processPath, panelWidth - 14), SeasonWinter(), new Rgb(206, 220, 226));
        WriteSeasonPanelBorder(width, panelWidth, false);
        WriteSeasonDropShadow(width, panelWidth);
        Console.WriteLine("\x1b[0m");
    }

    public static void PrintControllerGuide(ControllerProfile profile, string backend, Config config) {
        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(112, Math.Max(72, width - 6));
        bool zh = IsChineseUi();
        bool xbox = profile == ControllerProfile.Xbox360 || profile == ControllerProfile.XboxSeries;

        Console.WriteLine();
        WritePanelBorder(width, panelWidth, true, new Rgb(126, 226, 244));
        WritePanelTitle(width, panelWidth, zh ? "\u25c7 \u6620\u5c04\u901f\u67e5 \u25c7" : "\u25c7 MAPPING QUICK REFERENCE \u25c7", new Rgb(235, 247, 252));
        WritePanelSeparator(width, panelWidth, new Rgb(74, 94, 106));

        if (zh) {
            WritePanelLine(width, panelWidth, "  \u5df2\u8fde\u63a5", backend, new Rgb(126, 226, 244), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u53f3\u6447\u6746", "\u79fb\u52a8\u9f20\u6807, R3 \u53f3\u952e, L3 \u5de6\u952e", new Rgb(113, 255, 194), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u5de6\u6447\u6746", "\u2191\u6eda\u8f6e\u4e0a  \u2197 Fn  \u2192 Win  \u2198 Alt  \u2193\u6eda\u8f6e\u4e0b  \u2199 Ctrl  \u2190 Shift  \u2196 Esc", new Rgb(128, 224, 255), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u57fa\u7840\u5c42", xbox ? "D-pad=\u65b9\u5411\u952e, X=Space, Y=Backspace, A=Enter, B=Tab" : "D-pad=\u65b9\u5411\u952e, Square=Space, Triangle=Backspace, Cross=Enter, Circle=Tab", new Rgb(255, 211, 106), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  R1 / L1", "R1: i n e a o t h u    L1: s r d g l c y z", new Rgb(255, 142, 206), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  R2 / L2", "R2: m w j x q f p b    L2: k v 1 2 3 4 5 6", new Rgb(190, 133, 255), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u7ec4\u5408\u5c42", "R1+R2: 7 8 9 0 - = , .    L1+L2: ' / ; [ ] \\ `", new Rgb(255, 169, 85), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u7ec4\u5408\u7a97\u53e3", "R1/R2 \u6216 L1/L2 \u9700\u5728 " + config.ComboLayerWindowMs.ToString(CultureInfo.InvariantCulture) + "ms \u5185\u5408\u6309; \u8d85\u65f6\u6309\u6700\u540e\u5355\u5c42", new Rgb(126, 226, 244), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u5c42\u786e\u8ba4", "\u52a8\u4f5c\u952e\u7b49 " + config.ActionLayerGraceMs.ToString(CultureInfo.InvariantCulture) + "ms; \u65b0\u5355\u5c42\u4ec5\u56de\u770b " + config.LayerTakeoverWindowMs.ToString(CultureInfo.InvariantCulture) + "ms", SeasonSummer(), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  \u84c4\u529b", xbox ? "View/Back \u6216 Menu/Start \u4efb\u610f\u4e00\u4e2a\u6309\u4f4f\u90fd\u7b97\u84c4\u529b" : "\u6309\u4f4f DualSense \u89e6\u63a7\u677f\u8fdb\u5165\u84c4\u529b", new Rgb(113, 255, 194), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Fn", "\u5de6\u6447\u6746\u2197 + 1..0,-,= => F1..F12", new Rgb(255, 255, 255), new Rgb(245, 250, 255));
        } else {
            WritePanelLine(width, panelWidth, "  Connected", backend, new Rgb(126, 226, 244), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Right stick", "Move mouse, R3 right click, L3 left click", new Rgb(113, 255, 194), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Left stick", "Up wheel, UpRight Fn, Right Win, DownRight Alt, Down Ctrl, Left Shift, UpLeft Esc", new Rgb(128, 224, 255), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Base layer", xbox ? "D-pad=arrows, X=Space, Y=Backspace, A=Enter, B=Tab" : "D-pad=arrows, Square=Space, Triangle=Backspace, Cross=Enter, Circle=Tab", new Rgb(255, 211, 106), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  R1 / L1", "R1: i n e a o t h u    L1: s r d g l c y z", new Rgb(255, 142, 206), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  R2 / L2", "R2: m w j x q f p b    L2: k v 1 2 3 4 5 6", new Rgb(190, 133, 255), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Combo layers", "R1+R2: 7 8 9 0 - = , .    L1+L2: ' / ; [ ] \\ `", new Rgb(255, 169, 85), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Combo window", "R1/R2 or L1/L2 must pair within " + config.ComboLayerWindowMs.ToString(CultureInfo.InvariantCulture) + "ms; later overlaps use the newest single layer", new Rgb(126, 226, 244), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Layer settle", "Action waits " + config.ActionLayerGraceMs.ToString(CultureInfo.InvariantCulture) + "ms; takeover looks back " + config.LayerTakeoverWindowMs.ToString(CultureInfo.InvariantCulture) + "ms", SeasonSummer(), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Clutch", xbox ? "Hold either View/Back or Menu/Start for touchpad charge" : "Hold the DualSense touchpad for touchpad charge", new Rgb(113, 255, 194), new Rgb(245, 250, 255));
            WritePanelLine(width, panelWidth, "  Fn", "Left stick UpRight + 1..0,-,= => F1..F12", new Rgb(255, 255, 255), new Rgb(245, 250, 255));
        }

        WritePanelBorder(width, panelWidth, false, new Rgb(126, 226, 244));
        Console.WriteLine("\x1b[0m");
    }

    private static bool IsChineseUi() {
        try {
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return String.Equals(lang, "zh", StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
    }

    private static void EnableAnsi() {
        try {
            IntPtr handle = GetStdHandle(-11);
            uint mode;
            if (GetConsoleMode(handle, out mode)) {
                SetConsoleMode(handle, mode | 0x0004 | 0x0008);
            }
        } catch { }

        try {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        } catch { }
    }

    private static int GetConsoleWidth() {
        int width = 88;
        try { width = Console.WindowWidth; } catch { }
        if (width < 64) width = 64;
        if (width > 160) width = 160;
        return width;
    }

    private static string CenterLine(int width, string text) {
        int textWidth = DisplayWidth(text);
        if (textWidth >= width) return TrimToWidth(text, width);
        int left = (width - textWidth) / 2;
        return new string(' ', left) + text + new string(' ', width - left - textWidth);
    }

    private struct Rgb {
        public int R;
        public int G;
        public int B;

        public Rgb(int r, int g, int b) {
            R = r;
            G = g;
            B = b;
        }
    }

    private static void WriteRgb(Rgb color, string text) {
        Console.Write(string.Format("\x1b[38;2;{0};{1};{2}m{3}", color.R, color.G, color.B, text));
    }

    private static Rgb Mix(Rgb a, Rgb b, double t) {
        return new Rgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static Rgb Scale(Rgb color, double amount) {
        return new Rgb(
            ClampColor(color.R * amount),
            ClampColor(color.G * amount),
            ClampColor(color.B * amount));
    }

    private static int ClampColor(double value) {
        if (value < 0.0) return 0;
        if (value > 255.0) return 255;
        return (int)value;
    }

    private static Rgb GradientAt(Rgb[] stops, double t) {
        if (t <= 0.0) return stops[0];
        if (t >= 1.0) return stops[stops.Length - 1];
        double scaled = t * (stops.Length - 1);
        int segment = (int)scaled;
        if (segment >= stops.Length - 1) segment = stops.Length - 2;
        return Mix(stops[segment], stops[segment + 1], scaled - segment);
    }

    private static Rgb[] SeasonStops() {
        return new Rgb[] {
            SeasonSpring(),
            SeasonSummer(),
            SeasonAutumn(),
            SeasonWinter()
        };
    }

    private static Rgb[] SeasonFlowStops() {
        return new Rgb[] {
            SeasonSpring(),
            new Rgb(91, 251, 226),
            SeasonSummer(),
            new Rgb(198, 244, 255),
            new Rgb(255, 238, 154),
            SeasonGold(),
            new Rgb(255, 183, 112),
            SeasonAutumn(),
            new Rgb(255, 213, 168),
            SeasonWinter()
        };
    }

    private static Rgb[] SeasonGlowStops() {
        return new Rgb[] {
            new Rgb(72, 255, 202),
            new Rgb(91, 226, 255),
            SeasonGold(),
            new Rgb(255, 163, 102),
            new Rgb(244, 252, 255)
        };
    }

    private static Rgb SeasonSpring() { return new Rgb(94, 255, 197); }
    private static Rgb SeasonSummer() { return new Rgb(91, 226, 255); }
    private static Rgb SeasonGold() { return new Rgb(255, 215, 92); }
    private static Rgb SeasonAutumn() { return new Rgb(255, 148, 82); }
    private static Rgb SeasonWinter() { return new Rgb(255, 255, 255); }
    private static Rgb PanelInk() { return new Rgb(48, 72, 86); }
    private static Rgb ShadowInk() { return new Rgb(9, 18, 24); }

    private static string[] BuildShikiPadLogo() {
        string[][] glyphs = new string[][] {
            new string[] {
                " ████████ ",
                "██        ",
                "██        ",
                " ███████  ",
                "      ██  ",
                "       ██ ",
                "████████  ",
                " ████████ "
            },
            new string[] {
                "██    ██",
                "██    ██",
                "██    ██",
                "████████",
                "██    ██",
                "██    ██",
                "██    ██",
                "██    ██"
            },
            new string[] {
                "████",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                "████"
            },
            new string[] {
                "██   ██ ",
                "██  ██  ",
                "██ ██   ",
                "████    ",
                "████    ",
                "██ ██   ",
                "██  ██  ",
                "██   ██ "
            },
            new string[] {
                "████",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                " ██ ",
                "████"
            },
            new string[] {
                "███████ ",
                "██    ██",
                "██    ██",
                "███████ ",
                "██      ",
                "██      ",
                "██      ",
                "██      "
            },
            new string[] {
                " █████  ",
                "██   ██ ",
                "     ██ ",
                " ██████ ",
                "██   ██ ",
                "██   ██ ",
                "██   ██ ",
                " ██████ "
            },
            new string[] {
                "     ██ ",
                "     ██ ",
                "     ██ ",
                " ██████ ",
                "██   ██ ",
                "██   ██ ",
                "██   ██ ",
                " ██████ "
            }
        };

        int rows = glyphs[0].Length;
        StringBuilder[] lines = new StringBuilder[rows];
        for (int row = 0; row < rows; row++) lines[row] = new StringBuilder();
        for (int glyph = 0; glyph < glyphs.Length; glyph++) {
            for (int row = 0; row < rows; row++) {
                if (glyph > 0) lines[row].Append(' ');
                lines[row].Append(glyphs[glyph][row]);
            }
        }

        string[] logo = new string[rows];
        for (int row = 0; row < rows; row++) logo[row] = lines[row].ToString().TrimEnd();
        return logo;
    }

    private static void WriteExtrudedLogo(int width, string[] logo, Rgb[] stops) {
        int logoWidth = 0;
        for (int row = 0; row < logo.Length; row++) if (logo[row].Length > logoWidth) logoWidth = logo[row].Length;
        int shadowX = 3;
        int shadowY = 2;
        int outputWidth = logoWidth + shadowX;
        int left = Math.Max(0, (width - outputWidth) / 2);

        for (int row = 0; row < logo.Length + shadowY; row++) {
            Console.Write(new string(' ', left));
            Console.Write("\x1b[1m");
            for (int col = 0; col < outputWidth; col++) {
                bool main = IsLogoPixel(logo, row, col);
                bool nearShadow = IsLogoPixel(logo, row - 1, col - 2);
                bool farShadow = IsLogoPixel(logo, row - shadowY, col - shadowX);
                double t = logoWidth <= 1 ? 1.0 : (double)Math.Max(0, Math.Min(col, logoWidth - 1)) / (double)(logoWidth - 1);
                Rgb baseColor = GradientAt(stops, t);

                if (main) {
                    double rowT = logo.Length <= 1 ? 0.0 : (double)row / (double)(logo.Length - 1);
                    Rgb face = LogoFaceColor(baseColor, rowT);
                    WriteRgb(face, "\u2588");
                } else if (nearShadow) {
                    WriteRgb(Scale(baseColor, 0.38), "\u2593");
                } else if (farShadow) {
                    WriteRgb(Scale(baseColor, 0.22), "\u2592");
                } else {
                    Console.Write(' ');
                }
            }
            Console.Write("\x1b[22m");
            Console.WriteLine();
        }
    }

    private static bool IsLogoPixel(string[] logo, int row, int col) {
        if (row < 0 || row >= logo.Length) return false;
        if (col < 0 || col >= logo[row].Length) return false;
        return logo[row][col] != ' ';
    }

    private static Rgb LogoFaceColor(Rgb baseColor, double rowT) {
        if (rowT < 0.22) return Mix(baseColor, new Rgb(255, 255, 255), 0.24);
        if (rowT > 0.72) return Scale(baseColor, 0.82);
        return baseColor;
    }

    private static void WriteReadyLine(int width, int panelWidth, bool zh) {
        WriteEmbossedCenteredText(width, panelWidth, zh ? "\u257a \u63a7\u5236\u754c\u9762\u5df2\u5c31\u7eea \u2578" : "\u257a CONTROL SURFACE READY \u2578", SeasonGlowStops(), true);
        WriteEmbossedCenteredText(width, panelWidth, zh ? "\u2727  \u56db\u5b63\u4fe1\u53f7\u5728\u7ebf  \u25c7  \u8f93\u5165\u5c42\u5df2\u5c31\u7eea  \u2727" : "\u2727  seasonal signal online  \u25c7  input layers armed  \u2727", SeasonFlowStops(), false);
    }

    private static void WriteNeonRule(int width, int panelWidth, string title) {
        string line = "\u2726\u2500\u2500 " + title + " " + new string('\u2500', Math.Max(0, panelWidth - title.Length - 7)) + "\u2726";
        WriteEmbossedCenteredText(width, panelWidth, line, SeasonGlowStops(), true);
    }

    private static void WriteGradientText(string text, Rgb[] stops) {
        for (int i = 0; i < text.Length; i++) {
            double t = text.Length <= 1 ? 1.0 : (double)i / (double)(text.Length - 1);
            WriteRgb(GradientAt(stops, t), text[i].ToString());
        }
    }

    private static void WriteEmbossedCenteredText(int width, int panelWidth, string text, Rgb[] stops, bool bold) {
        int left = (width - panelWidth) / 2;
        string line = CenterLine(panelWidth, text);
        Console.Write(new string(' ', left + 1));
        WriteGradientShadowGlyphs(line, stops);
        Console.Write("\r");
        Console.Write(new string(' ', left));
        if (bold) Console.Write("\x1b[1m");
        WriteGradientText(line, stops);
        if (bold) Console.Write("\x1b[22m");
        Console.WriteLine();
    }

    private static void WriteGradientShadowGlyphs(string text, Rgb[] stops) {
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            if (c == ' ') {
                Console.Write(' ');
            } else {
                double t = text.Length <= 1 ? 1.0 : (double)i / (double)(text.Length - 1);
                WriteRgb(Scale(GradientAt(stops, t), 0.22), "\u2592");
            }
        }
    }

    private static void WriteSeasonDropShadow(int width, int panelWidth) {
        int left = Math.Max(0, (width - panelWidth) / 2 + 2);
        Console.Write(new string(' ', left));
        WriteRgb(ShadowInk(), RepeatPattern("\u2591", Math.Max(0, panelWidth - 2)));
        Console.WriteLine();
    }

    private static void WriteSeasonRail(int width, int panelWidth, bool zh) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteGradientText("\u256d" + new string('\u2500', panelWidth - 2) + "\u256e", SeasonFlowStops());
        Console.WriteLine();

        Console.Write(new string(' ', left));
        WriteRgb(PanelInk(), "\u2502");
        int inner = panelWidth - 2;
        string[] labels = zh
            ? new string[] { "\u273f \u6625", "\u25c7 \u590f", "\u25c8 \u79cb", "\u2744 \u51ac" }
            : new string[] { "\u273f Spring", "\u25c7 Summer", "\u25c8 Autumn", "\u2744 Winter" };
        Rgb[] colors = SeasonStops();
        int cell = inner / labels.Length;
        int used = 0;
        for (int i = 0; i < labels.Length; i++) {
            int cellWidth = (i == labels.Length - 1) ? inner - used : cell;
            string label = CenterLine(cellWidth, labels[i]);
            Console.Write("\x1b[1m");
            WriteRgb(colors[i], label);
            Console.Write("\x1b[22m");
            used += cellWidth;
        }
        WriteRgb(PanelInk(), "\u2502");
        Console.WriteLine();
        Console.Write(new string(' ', left));
        WriteGradientText("\u2570" + new string('\u2500', panelWidth - 2) + "\u256f", SeasonFlowStops());
        Console.WriteLine();
        WriteSeasonDropShadow(width, panelWidth);
    }

    private static void WriteLogoHalo(int width, int panelWidth, bool top, bool zh) {
        if (top) {
            WriteSeasonLegend(width, panelWidth, zh);
            return;
        }
        string line = zh
            ? "\u25c7  \u7269\u7406\u6309\u952e  \u2506  \u9f20\u6807\u66f2\u7ebf  \u2506  \u89e6\u63a7\u677f\u84c4\u529b  \u25c7"
            : "\u25c7  physical keys  \u2506  mouse curve  \u2506  touch clutch  \u25c7";
        WriteEmbossedCenteredText(width, panelWidth, TrimToWidth(line, panelWidth), SeasonFlowStops(), false);
    }

    private static void WriteSeasonLegend(int width, int panelWidth, bool zh) {
        int left = (width - panelWidth) / 2;
        int inner = panelWidth;
        string[] labels = zh
            ? new string[] { "\u273f \u6625\u4e4b\u8584\u8377", "\u25c7 \u590f\u4e4b\u6c34\u84dd", "\u25c8 \u79cb\u4e4b\u6696\u6a59", "\u2744 \u51ac\u4e4b\u971c\u767d" }
            : new string[] { "\u273f Spring mint", "\u25c7 Summer aqua", "\u25c8 Autumn ember", "\u2744 Winter frost" };
        Rgb[] colors = SeasonStops();
        int cell = inner / labels.Length;
        int used = 0;

        Console.Write(new string(' ', left + 1));
        for (int i = 0; i < labels.Length; i++) {
            int cellWidth = (i == labels.Length - 1) ? inner - used : cell;
            WriteRgb(Scale(colors[i], 0.22), CenterLine(cellWidth, ShadowText(labels[i])));
            used += cellWidth;
        }
        Console.Write("\r");
        Console.Write(new string(' ', left));
        used = 0;
        Console.Write("\x1b[1m");
        for (int i = 0; i < labels.Length; i++) {
            int cellWidth = (i == labels.Length - 1) ? inner - used : cell;
            WriteRgb(colors[i], CenterLine(cellWidth, labels[i]));
            used += cellWidth;
        }
        Console.Write("\x1b[22m");
        Console.WriteLine();
    }

    private static string ShadowText(string text) {
        StringBuilder sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++) sb.Append(text[i] == ' ' ? ' ' : '\u2592');
        return sb.ToString();
    }

    private static void WritePixelSubline(int width, int panelWidth, bool zh) {
        string text = zh
            ? "\u25a3 \u56db\u5b63\u8f93\u5165\u6620\u5c04\u5668  \u25b8  \u7269\u7406\u6309\u952e / \u9f20\u6807 / \u89e6\u63a7\u677f\u84c4\u529b"
            : "\u25a3 seasonal input mapper  \u25b8  physical keys / mouse / touch charge";
        WriteEmbossedCenteredText(width, panelWidth, text, SeasonFlowStops(), false);
    }

    private static void WriteStatusCard(int width, int panelWidth, bool zh) {
        WriteSeasonPanelBorder(width, panelWidth, true);
        WriteSeasonPanelTitle(width, panelWidth, zh ? "\u25c7 \u7cfb\u7edf\u72b6\u6001 \u25c7" : "\u25c7 SYSTEM STATUS \u25c7");
        WriteSeasonPanelSeparator(width, panelWidth);
        WriteDoublePanelLine(width, panelWidth,
            zh ? "\u624b\u67c4\u5df2\u5524\u9192" : "Controller awake", zh ? "\u5c31\u7eea" : "READY",
            zh ? "\u952e\u76d8\u9f20\u6807\u5df2\u5c31\u7eea" : "Keyboard and mouse ready", zh ? "\u5c31\u7eea" : "READY",
            SeasonSpring(), SeasonWinter());
        WritePanelLine(width, panelWidth, zh ? "  \u56db\u5b63\u5faa\u73af" : "  Season cycle", zh ? "\u6625 / \u590f / \u79cb / \u51ac" : "Spring / Summer / Autumn / Winter", SeasonSummer(), SeasonWinter());
        WritePanelLine(width, panelWidth, zh ? "  \u8f93\u5165\u5b89\u5168" : "  Input safety", zh ? "\u5173\u95ed\u65f6\u81ea\u52a8\u91ca\u653e" : "Auto-release on close", SeasonAutumn(), SeasonWinter());
        WriteSeasonPanelBorder(width, panelWidth, false);
        WriteSeasonDropShadow(width, panelWidth);
    }

    private static void WriteSeasonPanelBorder(int width, int panelWidth, bool top) {
        int left = (width - panelWidth) / 2;
        string line = (top ? "\u256d" : "\u2570") + new string('\u2500', panelWidth - 2) + (top ? "\u256e" : "\u256f");
        Console.Write(new string(' ', left));
        WriteGradientText(line, SeasonFlowStops());
        Console.WriteLine();
    }

    private static void WriteSeasonPanelSeparator(int width, int panelWidth) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(PanelInk(), "\u2502");
        WriteGradientText(new string('\u2504', panelWidth - 2), SeasonFlowStops());
        WriteRgb(PanelInk(), "\u2502");
        Console.WriteLine();
    }

    private static void WriteSeasonPanelTitle(int width, int panelWidth, string title) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(PanelInk(), "\u2502");
        Console.Write("\x1b[1m");
        WriteGradientText(CenterLine(panelWidth - 2, title), SeasonFlowStops());
        Console.Write("\x1b[22m");
        WriteRgb(PanelInk(), "\u2502");
        Console.WriteLine();
    }

    private static void WriteSeasonDivider(int width, int panelWidth, bool zh) {
        int left = (width - panelWidth) / 2;
        string text = zh
            ? "\u273f  \u6625\u4e4b\u8bb0\u5fc6   \u25c7  \u590f\u4e4b\u4fe1\u53f7   \u25c8  \u79cb\u4e4b\u952e\u5149   \u2744  \u51ac\u4e4b\u5c42\u5728\u7ebf"
            : "\u273f  Spring memory   \u25c7  Summer signal   \u25c8  Autumn keylight   \u2744  Winter layer online";
        string rule = RepeatPattern("\u2500\u22c5", panelWidth);
        Console.Write(new string(' ', left));
        WriteGradientText(rule, SeasonFlowStops());
        Console.WriteLine();
        WriteEmbossedCenteredText(width, panelWidth, text, SeasonFlowStops(), false);
    }

    private static void WritePanelBorder(int width, int panelWidth, bool top, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        string line = (top ? "\u256d" : "\u2570") + new string('\u2500', panelWidth - 2) + (top ? "\u256e" : "\u256f");
        WriteRgb(color, line);
        Console.WriteLine();
    }

    private static void WritePanelSeparator(int width, int panelWidth, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(color, "\u2502" + new string('\u2504', panelWidth - 2) + "\u2502");
        Console.WriteLine();
    }

    private static void WritePanelTitle(int width, int panelWidth, string title, Rgb color) {
        int left = (width - panelWidth) / 2;
        Console.Write(new string(' ', left));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        WriteRgb(color, CenterLine(panelWidth - 2, title));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.WriteLine();
    }

    private static void WriteDoublePanelLine(int width, int panelWidth, string leftLabel, string leftValue, string rightLabel, string rightValue, Rgb leftColor, Rgb rightColor) {
        int left = (width - panelWidth) / 2;
        int inner = panelWidth - 2;
        int gap = 5;
        int column = (inner - gap) / 2;
        string leftText = "\u25b8 " + leftLabel + "  | " + leftValue;
        string rightText = "\u25b8 " + rightLabel + "  | " + rightValue;

        Console.Write(new string(' ', left));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.Write("\x1b[1m");
        WriteRgb(leftColor, PadRight(TrimToWidth(leftText, column), column));
        Console.Write("\x1b[22m");
        WriteRgb(new Rgb(72, 91, 101), "  \u2506  ");
        Console.Write("\x1b[1m");
        WriteRgb(rightColor, PadRight(TrimToWidth(rightText, inner - column - gap), inner - column - gap));
        Console.Write("\x1b[22m");
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.WriteLine();
    }

    private static void WritePanelLine(int width, int panelWidth, string label, string value, Rgb labelColor, Rgb valueColor) {
        int left = (width - panelWidth) / 2;
        int inner = panelWidth - 2;
        int labelWidth = Math.Min(28, Math.Max(18, inner / 3));
        int valueWidth = inner - labelWidth - 3;
        if (DisplayWidth(value) > valueWidth) value = TrimToWidth(value, valueWidth);

        Console.Write(new string(' ', left));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.Write("\x1b[1m");
        WriteRgb(labelColor, PadRight(label, labelWidth));
        Console.Write("\x1b[22m");
        WriteRgb(new Rgb(72, 91, 101), " \u2506 ");
        WriteRgb(valueColor, PadRight(value, valueWidth));
        WriteRgb(new Rgb(72, 91, 101), "\u2502");
        Console.WriteLine();
    }

    private static string PadRight(string text, int width) {
        if (width <= 0) return "";
        int textWidth = DisplayWidth(text);
        if (textWidth >= width) return TrimToWidth(text, width);
        return text + new string(' ', width - textWidth);
    }

    private static string TrimToWidth(string text, int width) {
        if (width <= 0) return "";
        if (DisplayWidth(text) <= width) return text;
        if (width <= 1) return "\u2026";

        StringBuilder sb = new StringBuilder();
        int used = 0;
        for (int i = 0; i < text.Length; i++) {
            char c = text[i];
            int cw = CharDisplayWidth(c);
            if (used + cw > width - 1) break;
            sb.Append(c);
            used += cw;
        }
        sb.Append("\u2026");
        return sb.ToString();
    }

    private static int DisplayWidth(string text) {
        if (String.IsNullOrEmpty(text)) return 0;
        int width = 0;
        for (int i = 0; i < text.Length; i++) width += CharDisplayWidth(text[i]);
        return width;
    }

    private static int CharDisplayWidth(char c) {
        if (c >= 0x1100 &&
            (c <= 0x115F ||
             c == 0x2329 || c == 0x232A ||
             (c >= 0x2E80 && c <= 0xA4CF) ||
             (c >= 0xAC00 && c <= 0xD7A3) ||
             (c >= 0xF900 && c <= 0xFAFF) ||
             (c >= 0xFE10 && c <= 0xFE19) ||
             (c >= 0xFE30 && c <= 0xFE6F) ||
             (c >= 0xFF00 && c <= 0xFF60) ||
             (c >= 0xFFE0 && c <= 0xFFE6))) {
            return 2;
        }
        return 1;
    }

    private static string RepeatPattern(string pattern, int width) {
        if (width <= 0) return "";
        StringBuilder sb = new StringBuilder(width + pattern.Length);
        while (sb.Length < width) sb.Append(pattern);
        if (sb.Length > width) sb.Length = width;
        return sb.ToString();
    }

    private static void WriteLiveStatusBar(int width, int panelWidth, bool zh) {
        int left = (width - panelWidth) / 2;
        string text = zh ? "\u25c6 \u5b9e\u65f6\u8fd0\u884c" : "\u25c6 Live session";
        string rail = "\u256d" + RepeatPattern("\u2500\u22c5", panelWidth - 2) + "\u256e";
        string bottom = "\u2570" + RepeatPattern("\u2500\u22c5", panelWidth - 2) + "\u256f";

        Console.Write(new string(' ', left));
        WriteGradientText(rail, SeasonFlowStops());
        Console.WriteLine();
        Console.Write(new string(' ', left));
        WriteRgb(PanelInk(), "\u2502");
        WriteGradientText(CenterLine(panelWidth - 2, text), SeasonFlowStops());
        WriteRgb(PanelInk(), "\u2502");
        Console.WriteLine();
        Console.Write(new string(' ', left));
        WriteGradientText(bottom, SeasonFlowStops());
        Console.WriteLine();
        WriteSeasonDropShadow(width, panelWidth);
    }

    private static string ShortenPath(string path, int maxLength) {
        if (path == null) return "";
        if (path.Length <= maxLength) return path;
        if (maxLength <= 4) return path.Substring(0, maxLength);
        return "\u2026" + path.Substring(path.Length - maxLength + 1);
    }

    private static bool _shutdownReleaseRegistered;

    [STAThread]
    private static int Main(string[] args) {
        PrintGradientBanner();

        string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Directory.SetCurrentDirectory(root);
        Logger.Init(root);
        Config config = Config.Load(Path.Combine(root, "shikipad.json"));
        RegisterShutdownRelease();
        bool debugAltTab = HasArg(args, "--debug-alt-tab");
        bool debugSources = HasArg(args, "--debug-sources");
        bool traceInput = HasArg(args, "--trace-input");
        bool traceSendinput = HasArg(args, "--trace-sendinput");
        if (HasArg(args, "--list-devices") || HasArg(args, "--enum-hid")) {
            RunHidEnumTest();
            return 0;
        }

        if (HasArg(args, "--identity")) {
            Console.WriteLine("\n--- SHIKIPAD PROCESS IDENTITY ---");
            Console.WriteLine("Current process exe path: " + Process.GetCurrentProcess().MainModule.FileName);
            Console.WriteLine("Working directory: " + Environment.CurrentDirectory);
            Console.WriteLine("Command line: " + Environment.CommandLine);
            Console.WriteLine("Process ID: " + Process.GetCurrentProcess().Id);
            
            int parentId = 0;
            try {
                var pc = new System.Diagnostics.PerformanceCounter("Process", "Creating Process ID", Process.GetCurrentProcess().ProcessName);
                parentId = (int)pc.NextValue();
            } catch { }
            Console.WriteLine("Parent process ID: " + parentId);
            
            Console.WriteLine("Does this exact process register RawInput? YES");
            Console.WriteLine("Is any helper process used? NO");
            Console.WriteLine("\nAdd THIS EXACT path to HidHide Applications:");
            Console.WriteLine(Process.GetCurrentProcess().MainModule.FileName);
            return 0;
        }

        if (HasArg(args, "--layer-test")) {
            PrintLayerTest(config);
            return 0;
        }
        if (HasArg(args, "--mouse-test")) {
            PrintMouseTest(config);
            return 0;
        }
        if (HasArg(args, "--left-stick-test")) {
            PrintLeftStickTest(config);
            return 0;
        }
        if (HasArg(args, "--clutch-test")) {
            PrintClutchTest();
            return Environment.ExitCode;
        }
        if (HasArg(args, "--shift-test")) {
            RunShiftTest(config);
            return 0;
        }
        if (HasArg(args, "--test")) {
            RunSelfTest(config);
            return 0;
        }

        ControllerProfile controllerProfile = SelectControllerProfile(args);
        Logger.Info("startup");
        Logger.Info("controller profile: " + ControllerProfileName(controllerProfile));
        Logger.Info("mouse settings: rightStickDeadzone = " + config.RightStickDeadzone.ToString("0.0", CultureInfo.InvariantCulture) +
                    ", rightStickCurve = " + config.RightStickCurve +
                    ", rightStickCurveExponent = " + config.RightStickCurveExponent.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", mouseMaxSpeed = " + config.MouseMaxSpeed.ToString(CultureInfo.InvariantCulture) +
                    ", neutralCalibration = enabled");
        Logger.Info("left stick modifiers = physical held keys");
        if (debugSources) Logger.Info("debug-sources enabled");
        if (traceInput) Logger.Info("trace-input enabled");
        if (traceSendinput) Logger.Info("trace-sendinput enabled");

        PrintRunHint();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MapperForm(config, controllerProfile, debugSources, traceInput, traceSendinput));
        return 0;
    }

    private static void RegisterShutdownRelease() {
        if (_shutdownReleaseRegistered) return;
        _shutdownReleaseRegistered = true;
        Application.ApplicationExit += delegate(object sender, EventArgs e) {
            InputInjector.ReleaseAllRegistered();
        };
        AppDomain.CurrentDomain.ProcessExit += delegate(object sender, EventArgs e) {
            InputInjector.ReleaseAllRegistered();
        };
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
            InputInjector.ReleaseAllRegistered();
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) {
            InputInjector.ReleaseAllRegistered();
        };
    }

    private static ControllerProfile SelectControllerProfile(string[] args) {
        ControllerProfile fromArgs;
        if (TryGetControllerProfileArg(args, out fromArgs)) return fromArgs;
        try {
            if (Console.IsInputRedirected) return ControllerProfile.DualSense;
        } catch { }

        EnableAnsi();
        int width = GetConsoleWidth();
        int panelWidth = Math.Min(104, Math.Max(66, width - 6));
        bool zh = IsChineseUi();
        Console.WriteLine();
        WriteSeasonPanelBorder(width, panelWidth, true);
        WriteSeasonPanelTitle(width, panelWidth, zh ? "\u25c7 \u9009\u62e9\u624b\u67c4\u578b\u53f7 \u25c7" : "\u25c7 CONTROLLER PROFILE \u25c7");
        WriteSeasonPanelSeparator(width, panelWidth);
        WritePanelLine(width, panelWidth, "  [1] DualSense", zh ? "PS5 / Direct HID / \u89e6\u63a7\u677f\u84c4\u529b" : "PS5 / Direct HID / touchpad clutch", SeasonSummer(), new Rgb(245, 250, 255));
        WritePanelLine(width, panelWidth, "  [2] Xbox 360", zh ? "XInput / View \u6216 Menu \u84c4\u529b" : "XInput / View or Menu touchpad clutch", SeasonSpring(), new Rgb(245, 250, 255));
        WritePanelLine(width, panelWidth, "  [3] Xbox Series X|S", zh ? "XInput / View \u6216 Menu \u84c4\u529b" : "XInput / View or Menu touchpad clutch", SeasonGold(), new Rgb(245, 250, 255));
        WriteSeasonPanelBorder(width, panelWidth, false);
        WriteSeasonDropShadow(width, panelWidth);
        Console.WriteLine();

        while (true) {
            WriteRgb(SeasonSummer(), zh ? "\u9009\u62e9\u624b\u67c4\u578b\u53f7 [1/2/3\uff0cEnter = 1] > " : "Select controller profile [1/2/3, Enter = 1] > ");
            Console.Write("\x1b[0m");
            string line = Console.ReadLine();
            if (line == null) return ControllerProfile.DualSense;
            line = line.Trim();
            if (line.Length == 0 || line == "1") return ControllerProfile.DualSense;
            if (line == "2") return ControllerProfile.Xbox360;
            if (line == "3") return ControllerProfile.XboxSeries;
            WriteRgb(SeasonAutumn(), zh ? "\u8bf7\u9009\u62e9 1\u30012 \u6216 3\u3002\n" : "Please choose 1, 2, or 3.\n");
        }
    }

    private static bool TryGetControllerProfileArg(string[] args, out ControllerProfile profile) {
        profile = ControllerProfile.DualSense;
        for (int i = 0; i < args.Length; i++) {
            string arg = args[i] ?? "";
            string value = null;
            if (arg.StartsWith("--controller=", StringComparison.OrdinalIgnoreCase)) {
                value = arg.Substring("--controller=".Length);
            } else if (String.Equals(arg, "--controller", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) {
                value = args[i + 1];
            }
            if (value != null) return TryParseControllerProfile(value, out profile);
        }
        return false;
    }

    private static bool TryParseControllerProfile(string value, out ControllerProfile profile) {
        string v = (value ?? "").Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
        if (v == "1" || v == "ds5" || v == "dualsense" || v == "ps5") {
            profile = ControllerProfile.DualSense;
            return true;
        }
        if (v == "2" || v == "xbox360" || v == "x360") {
            profile = ControllerProfile.Xbox360;
            return true;
        }
        if (v == "3" || v == "xboxseries" || v == "xboxseriesxs" || v == "xsx" || v == "xss" || v == "xboxxs") {
            profile = ControllerProfile.XboxSeries;
            return true;
        }
        profile = ControllerProfile.DualSense;
        return false;
    }

    private static string ControllerProfileName(ControllerProfile profile) {
        switch (profile) {
            case ControllerProfile.Xbox360: return "Xbox 360 Controller / XInput";
            case ControllerProfile.XboxSeries: return "Xbox Series X|S Controller / XInput";
            default: return "DualSense / Direct HID";
        }
    }

    private static bool HasArg(string[] args, string value) {
        for (int i = 0; i < args.Length; i++) if (String.Equals(args[i], value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }


    private static void RunHidEnumTest() {
        Console.WriteLine("\n--- HID DEVICE ENUMERATION TEST ---");
        Console.WriteLine("This test bypasses RawInput and directly queries the OS for connected HID devices.");
        Console.WriteLine("It will attempt to open each device to read VID/PID.\n");

        Guid hidGuid;
        NativeMethods.HidD_GetHidGuid(out hidGuid);

        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (deviceInfoSet == new IntPtr(-1)) {
            Console.WriteLine("Failed to get device info set.");
            return;
        }

        NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
        interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

        uint index = 0;
        int foundCount = 0;
        int sonyCount = 0;
        
        while (NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData)) {
            index++;
            uint requiredSize = 0;
            NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);
            
            if (requiredSize == 0) continue;
            
            IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
            Marshal.WriteInt32(detailData, (IntPtr.Size == 8) ? 8 : (Marshal.SystemDefaultCharSize == 1 ? 5 : 6));
            
            if (NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out requiredSize, IntPtr.Zero)) {
                string devicePath = Marshal.PtrToStringAuto(new IntPtr(detailData.ToInt64() + 4));
                
                IntPtr handle = NativeMethods.CreateFile(devicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (handle != new IntPtr(-1)) {
                    NativeMethods.HIDD_ATTRIBUTES attrs = new NativeMethods.HIDD_ATTRIBUTES();
                    attrs.Size = (uint)Marshal.SizeOf(attrs);
                    if (NativeMethods.HidD_GetAttributes(handle, ref attrs)) {
                        string product = "";
                        IntPtr prodStr = Marshal.AllocHGlobal(254);
                        if (NativeMethods.HidD_GetProductString(handle, prodStr, 254)) {
                            product = Marshal.PtrToStringAuto(prodStr);
                        }
                        Marshal.FreeHGlobal(prodStr);
                        
                        if (attrs.VendorID == 0x054C) {
                            Console.WriteLine("FOUND SONY DEVICE:");
                            Console.WriteLine("  VID: 0x" + attrs.VendorID.ToString("X4") + "  PID: 0x" + attrs.ProductID.ToString("X4"));
                            Console.WriteLine("  Product: " + product);
                            Console.WriteLine("  Path: " + devicePath);
                            Console.WriteLine();
                            sonyCount++;
                        }
                        foundCount++;
                    }
                    NativeMethods.CloseHandle(handle);
                }
            }
            Marshal.FreeHGlobal(detailData);
        }
        
        NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        
        Console.WriteLine("Total HID devices opened successfully: " + foundCount);
        Console.WriteLine("Total Sony devices (VID 0x054C) found: " + sonyCount);
        if (sonyCount > 0) {
            Console.WriteLine("\nSUCCESS: The physical controller is VISIBLE to this process via Direct HID.");
            Console.WriteLine("If ShikiPad still cannot read input normally, it confirms that HidHide hides devices from the Windows RawInput subsystem itself.");
        } else {
            Console.WriteLine("\nFAILED: No Sony devices visible. Either it is unplugged, or HidHide whitelist failed.");
        }
    }

    private static void PrintLayerTest(Config config) {
        MappingEngine m = new MappingEngine();
        Layer[] layers = new Layer[] { Layer.Base, Layer.L1, Layer.R1, Layer.L2, Layer.R2, Layer.R1R2, Layer.L1L2 };
        Console.WriteLine("Action button order: Up, Right, Square, Triangle, Left, Down, Cross, Circle");
        Console.WriteLine();
        for (int l = 0; l < layers.Length; l++) {
            Console.WriteLine(LayerDisplayName(layers[l]) + ":");
            for (int i = 0; i < 8; i++) {
                PhysicalKey key = m.Lookup(layers[l], (ActionButton)i);
                Console.WriteLine(((ActionButton)i).ToString() + " = " + LayerTestKeyName(key));
            }
            Console.WriteLine();
        }
        Console.WriteLine("Layer priority: latest triggered layer wins; R1+R2 and L1+L2 activate only inside comboLayerWindowMs.");
        Console.WriteLine("comboLayerWindowMs = " + config.ComboLayerWindowMs.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine();
        Console.WriteLine("Resolution checks:");
        double delayedMs = config.ComboLayerWindowMs + 120.0;
        PrintResolutionCheck(config, m, "R1 then R2 + Square", false, true, false, true, 0, 10, 0, 20, ActionButton.Square);
        PrintResolutionCheck(config, m, "R2 held then R1 after window + Square", false, true, false, true, 0, delayedMs, 0, 10, ActionButton.Square);
        PrintResolutionCheck(config, m, "R1 held then R2 after window + Square", false, true, false, true, 0, 10, 0, delayedMs, ActionButton.Square);
        PrintResolutionCheck(config, m, "R1+R2 then L1 + Square", true, true, false, true, 30, 10, 0, 20, ActionButton.Square);
        PrintResolutionCheck(config, m, "R1+R2 release R2 + Square", false, true, false, false, 0, 10, 0, 20, ActionButton.Square);
        PrintResolutionCheck(config, m, "L1 then L2 + Up", true, false, true, false, 10, 0, 20, 0, ActionButton.Up);
        PrintResolutionCheck(config, m, "L2 held then L1 after window + Up", true, false, true, false, delayedMs, 0, 10, 0, ActionButton.Up);
        PrintResolutionCheck(config, m, "L1 held then L2 after window + Up", true, false, true, false, 10, 0, delayedMs, 0, ActionButton.Up);
        PrintResolutionCheck(config, m, "L1+L2 then R2 + Up", true, false, true, true, 10, 0, 20, 30, ActionButton.Up);
        PrintResolutionCheck(config, m, "R1 then L1 + Square", true, true, false, false, 20, 10, 0, 0, ActionButton.Square);
        PrintResolutionCheck(config, m, "L2 then R2 + Square", false, false, true, true, 0, 0, 10, 20, ActionButton.Square);
        Console.WriteLine();
        PrintPendingTimingChecks(config, m);
    }

    private static void PrintResolutionCheck(Config config, MappingEngine mapping, string label, bool l1, bool r1, bool l2, bool r2, double l1Ms, double r1Ms, double l2Ms, double r2Ms, ActionButton action) {
        Layer layer = mapping.Resolve(l1, r1, l2, r2, l1Ms, r1Ms, l2Ms, r2Ms, config.ComboLayerWindowMs);
        PhysicalKey key = mapping.Lookup(layer, action);
        Console.WriteLine(label + " = " + LayerDisplayName(layer) + " / " + LayerTestKeyName(key));
    }

    private static void PrintPendingTimingChecks(Config config, MappingEngine mapping) {
        Console.WriteLine("Pending timing checks:");
        bool ok = true;
        ok = PrintCrossTakeoverCheck(config, mapping) && ok;
        ok = PrintControllerParityCheck(config, mapping) && ok;
        Console.WriteLine("Pending timing result = " + (ok ? "PASS" : "FAIL"));
        if (!ok) Environment.ExitCode = 1;
    }

    private static bool PrintCrossTakeoverCheck(Config config, MappingEngine mapping) {
        double l1Ms = 0.0;
        double upMs = 0.0;
        double crossMs = upMs + 100.0;
        double quickR1Ms = crossMs + 30.0;
        double lateR1Ms = crossMs + 70.0;

        Layer firstLayer = mapping.Resolve(true, false, false, false, l1Ms, 0, 0, 0, config.ComboLayerWindowMs);
        PhysicalKey firstKey = mapping.Lookup(firstLayer, ActionButton.Up);
        Layer crossStartLayer = mapping.Resolve(true, false, false, false, l1Ms, 0, 0, 0, config.ComboLayerWindowMs);
        Layer afterQuickR1Layer = mapping.Resolve(true, true, false, false, l1Ms, quickR1Ms, 0, 0, config.ComboLayerWindowMs);
        Layer quickSettledLayer = MapperForm.ResolvePendingLayer(crossStartLayer, l1Ms, crossMs, afterQuickR1Layer, quickR1Ms, config.LayerTakeoverWindowMs);
        PhysicalKey quickSettledKey = mapping.Lookup(quickSettledLayer, ActionButton.Cross);
        Layer afterLateR1Layer = mapping.Resolve(true, true, false, false, l1Ms, lateR1Ms, 0, 0, config.ComboLayerWindowMs);
        Layer lateSettledLayer = MapperForm.ResolvePendingLayer(crossStartLayer, l1Ms, crossMs, afterLateR1Layer, lateR1Ms, config.LayerTakeoverWindowMs);
        PhysicalKey lateSettledKey = mapping.Lookup(lateSettledLayer, ActionButton.Cross);

        bool firstSettled = crossMs - upMs >= config.ActionLayerGraceMs;
        bool quickInsideTakeover = quickR1Ms - crossMs <= config.LayerTakeoverWindowMs;
        bool lateOutsideTakeover = lateR1Ms - crossMs > config.LayerTakeoverWindowMs;
        bool lateStillInsideGrace = lateR1Ms - crossMs <= config.ActionLayerGraceMs;
        bool ok = firstSettled
            && quickInsideTakeover
            && lateOutsideTakeover
            && lateStillInsideGrace
            && firstLayer == Layer.L1
            && firstKey == PhysicalKey.S
            && crossStartLayer == Layer.L1
            && afterQuickR1Layer == Layer.R1
            && quickSettledLayer == Layer.R1
            && quickSettledKey == PhysicalKey.H
            && afterLateR1Layer == Layer.R1
            && lateSettledLayer == Layer.L1
            && lateSettledKey == PhysicalKey.Y;

        Console.WriteLine("L1+Up -> s, then Cross + R1 after 30ms while L1 held = " +
                          LayerDisplayName(quickSettledLayer) + " / " + LayerTestKeyName(quickSettledKey) +
                          (quickSettledKey == PhysicalKey.H ? " [PASS]" : " [FAIL]"));
        Console.WriteLine("L1+Up -> s, then Cross + R1 after 70ms while L1 held = " +
                          LayerDisplayName(lateSettledLayer) + " / " + LayerTestKeyName(lateSettledKey) +
                          (ok ? " [PASS]" : " [FAIL]"));
        return ok;
    }

    private static bool PrintControllerParityCheck(Config config, MappingEngine mapping) {
        ControllerProfile[] profiles = new ControllerProfile[] {
            ControllerProfile.DualSense,
            ControllerProfile.Xbox360,
            ControllerProfile.XboxSeries
        };
        bool ok = true;
        for (int i = 0; i < profiles.Length; i++) {
            Layer layer = mapping.Resolve(false, true, false, false, 0, 10, 0, 0, config.ComboLayerWindowMs);
            PhysicalKey key = mapping.Lookup(layer, ActionButton.Cross);
            bool profileOk = layer == Layer.R1 && key == PhysicalKey.H;
            string actionName = profiles[i] == ControllerProfile.DualSense ? "Cross" : "A";
            Console.WriteLine(ControllerProfileName(profiles[i]) + " R1/RB + " + actionName + " = " +
                              LayerTestKeyName(key) + (profileOk ? " [PASS]" : " [FAIL]"));
            ok = profileOk && ok;
        }
        return ok;
    }

    private static void PrintMouseTest(Config config) {
        Console.WriteLine("rightStickDeadzone = " + config.RightStickDeadzone.ToString("0.0", CultureInfo.InvariantCulture));
        Console.WriteLine("rightStickCurve = " + config.RightStickCurve);
        Console.WriteLine("rightStickCurveExponent = " + config.RightStickCurveExponent.ToString("0.###", CultureInfo.InvariantCulture));
        Console.WriteLine("mouseMaxSpeed = " + config.MouseMaxSpeed.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("neutralCalibration = enabled");
        Logger.Info("mouse-test: rightStickDeadzone = " + config.RightStickDeadzone.ToString("0.0", CultureInfo.InvariantCulture) +
                    ", rightStickCurve = " + config.RightStickCurve +
                    ", rightStickCurveExponent = " + config.RightStickCurveExponent.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", mouseMaxSpeed = " + config.MouseMaxSpeed.ToString(CultureInfo.InvariantCulture) +
                    ", neutralCalibration = enabled");
    }

    private static void PrintLeftStickTest(Config config) {
        Console.WriteLine("leftStickEnterDeadzone = " + config.LeftStickEnterDeadzone.ToString("0.00", CultureInfo.InvariantCulture));
        Console.WriteLine("leftStickExitDeadzone = " + config.LeftStickExitDeadzone.ToString("0.00", CultureInfo.InvariantCulture));
        Console.WriteLine("exclusive8Way = enabled");
        Console.WriteLine();
        PrintLeftStickSample(config, "Center", 0.0, 0.0);
        PrintLeftStickSample(config, "Up", 0.0, -1.0);
        PrintLeftStickSample(config, "UpRight", 0.70710678, -0.70710678);
        PrintLeftStickSample(config, "Right", 1.0, 0.0);
        PrintLeftStickSample(config, "DownRight", 0.70710678, 0.70710678);
        PrintLeftStickSample(config, "Down", 0.0, 1.0);
        PrintLeftStickSample(config, "DownLeft", -0.70710678, 0.70710678);
        PrintLeftStickSample(config, "Left", -1.0, 0.0);
        PrintLeftStickSample(config, "UpLeft", -0.70710678, -0.70710678);
        Console.WriteLine();
        Console.WriteLine("Latch simulation:");
        StickDirection latched = StickDirection.None;
        SimulateLeftStickLatch(config, ref latched, "enter DownRight", 0.70710678, 0.70710678);
        SimulateLeftStickLatch(config, ref latched, "jitter toward Down while held", 0.0, 1.0);
        SimulateLeftStickLatch(config, ref latched, "jitter toward Right while held", 1.0, 0.0);
        SimulateLeftStickLatch(config, ref latched, "release below exit", 0.1, 0.1);
    }

    private static void PrintClutchTest() {
        bool ok = true;

        byte[] dualSensePressed = NeutralDualSenseReport();
        dualSensePressed[10] = 0x02;
        ControllerState dualSensePressedState;
        bool parsedPressed = DirectHidController.TryParseDualSenseReport(dualSensePressed, out dualSensePressedState);
        ok = PrintClutchCheck("DualSense touchpad press = clutch", parsedPressed && dualSensePressedState.TouchClick) && ok;

        byte[] dualSenseReleased = NeutralDualSenseReport();
        ControllerState dualSenseReleasedState;
        bool parsedReleased = DirectHidController.TryParseDualSenseReport(dualSenseReleased, out dualSenseReleasedState);
        ok = PrintClutchCheck("DualSense touchpad released = no clutch", parsedReleased && !dualSenseReleasedState.TouchClick) && ok;

        NativeMethods.XINPUT_GAMEPAD xboxBack = new NativeMethods.XINPUT_GAMEPAD();
        xboxBack.wButtons = NativeMethods.XINPUT_GAMEPAD_BACK;
        ok = PrintClutchCheck("Xbox View/Back = clutch", DirectHidController.ParseXInputState(xboxBack).TouchClick) && ok;

        NativeMethods.XINPUT_GAMEPAD xboxStart = new NativeMethods.XINPUT_GAMEPAD();
        xboxStart.wButtons = NativeMethods.XINPUT_GAMEPAD_START;
        ok = PrintClutchCheck("Xbox Menu/Start = clutch", DirectHidController.ParseXInputState(xboxStart).TouchClick) && ok;

        NativeMethods.XINPUT_GAMEPAD xboxReleased = new NativeMethods.XINPUT_GAMEPAD();
        ok = PrintClutchCheck("Xbox View/Menu released = no clutch", !DirectHidController.ParseXInputState(xboxReleased).TouchClick) && ok;

        Console.WriteLine("Clutch mapping result = " + (ok ? "PASS" : "FAIL"));
        if (!ok) Environment.ExitCode = 1;
    }

    private static byte[] NeutralDualSenseReport() {
        byte[] report = new byte[11];
        report[0] = 0x01;
        report[1] = 128;
        report[2] = 128;
        report[3] = 128;
        report[4] = 128;
        report[8] = 0x08;
        return report;
    }

    private static bool PrintClutchCheck(string label, bool passed) {
        Console.WriteLine(label + (passed ? " [PASS]" : " [FAIL]"));
        return passed;
    }

    private static void PrintLeftStickSample(Config config, string label, double x, double y) {
        double radius = Math.Sqrt(x * x + y * y);
        StickDirection direction = radius >= config.LeftStickEnterDeadzone ? MapperForm.Sector(x, y) : StickDirection.None;
        double angle = radius > 0.0 ? Math.Atan2(-y, x) * 180.0 / Math.PI : 0.0;
        Console.WriteLine(label + ": x=" + x.ToString("0.###", CultureInfo.InvariantCulture) +
                          ", y=" + y.ToString("0.###", CultureInfo.InvariantCulture) +
                          ", radius=" + radius.ToString("0.###", CultureInfo.InvariantCulture) +
                          ", angle=" + angle.ToString("0.#", CultureInfo.InvariantCulture) +
                          ", direction=" + direction +
                          ", action=" + LeftStickActionName(direction));
    }

    private static string LeftStickActionName(StickDirection direction) {
        switch (direction) {
            case StickDirection.Up: return "WheelUp only";
            case StickDirection.UpRight: return "Fn only";
            case StickDirection.Right: return "Win only";
            case StickDirection.DownRight: return "Alt only";
            case StickDirection.Down: return "WheelDown only";
            case StickDirection.DownLeft: return "Ctrl only";
            case StickDirection.Left: return "Shift only";
            case StickDirection.UpLeft: return "Esc only";
            default: return "None";
        }
    }

    private static void SimulateLeftStickLatch(Config config, ref StickDirection latched, string label, double x, double y) {
        double radius = Math.Sqrt(x * x + y * y);
        if (latched == StickDirection.None) {
            if (radius >= config.LeftStickEnterDeadzone) latched = MapperForm.Sector(x, y);
        } else if (radius < config.LeftStickExitDeadzone) {
            latched = StickDirection.None;
        }

        Console.WriteLine(label + ": radius=" + radius.ToString("0.###", CultureInfo.InvariantCulture) +
                          ", rawSector=" + (radius > 0.0 ? MapperForm.Sector(x, y).ToString() : "None") +
                          ", latched=" + latched +
                          ", action=" + LeftStickActionName(latched));
    }

    private static string LayerDisplayName(Layer layer) {
        if (layer == Layer.R1R2) return "R1+R2";
        if (layer == Layer.L1L2) return "L1+L2";
        return layer.ToString();
    }

    private static string LayerTestKeyName(PhysicalKey key) {
        if (key >= PhysicalKey.A && key <= PhysicalKey.Z) {
            char letter = (char)('a' + (int)(key - PhysicalKey.A));
            return letter.ToString();
        }

        switch (key) {
            case PhysicalKey.Num0: return "0";
            case PhysicalKey.Num1: return "1";
            case PhysicalKey.Num2: return "2";
            case PhysicalKey.Num3: return "3";
            case PhysicalKey.Num4: return "4";
            case PhysicalKey.Num5: return "5";
            case PhysicalKey.Num6: return "6";
            case PhysicalKey.Num7: return "7";
            case PhysicalKey.Num8: return "8";
            case PhysicalKey.Num9: return "9";
            default: return MappingEngine.KeyName(key);
        }
    }

    private static void RunSelfTest(Config config) {
        Console.WriteLine("Focus Notepad. Typing starts in 2 seconds.");
        Thread.Sleep(2000);
        InputInjector i = new InputInjector(config.UseScanCode, config.UseInterception);
        PhysicalKey[] keys = new PhysicalKey[] {
            PhysicalKey.A, PhysicalKey.I, PhysicalKey.N, PhysicalKey.T, PhysicalKey.S,
            PhysicalKey.Num1, PhysicalKey.Num2, PhysicalKey.Num9, PhysicalKey.Num0,
            PhysicalKey.Comma, PhysicalKey.Period, PhysicalKey.Minus, PhysicalKey.Equals,
            PhysicalKey.Slash, PhysicalKey.Semicolon, PhysicalKey.Apostrophe,
            PhysicalKey.LeftBracket, PhysicalKey.RightBracket, PhysicalKey.Backslash, PhysicalKey.Grave
        };
        for (int k = 0; k < keys.Length; k++) {
            i.KeyTap(keys[k], false, false, false, false);
            Thread.Sleep(25);
        }
        i.ReleaseAll();
    }

    private static void RunShiftTest(Config config) {
        Console.WriteLine("Focus Notepad. Typing starts in 2 seconds.");
        Thread.Sleep(2000);
        InputInjector i = new InputInjector(config.UseScanCode, config.UseInterception);
        PhysicalKey[] keys = new PhysicalKey[] {
            PhysicalKey.A, PhysicalKey.Num1, PhysicalKey.Num9, PhysicalKey.Num0,
            PhysicalKey.Comma, PhysicalKey.Period, PhysicalKey.Minus, PhysicalKey.Equals,
            PhysicalKey.Slash, PhysicalKey.Semicolon, PhysicalKey.Apostrophe,
            PhysicalKey.LeftBracket, PhysicalKey.RightBracket, PhysicalKey.Backslash, PhysicalKey.Grave
        };
        for (int k = 0; k < keys.Length; k++) {
            i.KeyTap(keys[k], true, false, false, false);
            Thread.Sleep(25);
        }
        i.ReleaseAll();
    }
}
}
