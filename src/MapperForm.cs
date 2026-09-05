using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

internal sealed class MapperForm : Form {
    private const double PollIntervalMs = 1.0;
    private const double MaxMouseFrameSeconds = 0.05;
    private readonly DirectHidController _hid;
    private readonly Config _config;
    private readonly InputInjector _injector;
    private readonly MappingEngine _mapping = new MappingEngine();

    private Thread _pollThread;
    private volatile bool _pollRunning;
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
    private readonly LeftStickScrollIntegrator _leftStickScroll = new LeftStickScrollIntegrator();
    private readonly RightStickMouseIntegrator _rightStickMouse = new RightStickMouseIntegrator();
    private double _mouseFreezeUntilMs;
    private MouseButtonHold _leftMouseButton;
    private MouseButtonHold _rightMouseButton;
    private List<PhysicalKey> _accumulatedModifiers = new List<PhysicalKey>();
    private List<PhysicalKey> _desiredLeftStickKeys = new List<PhysicalKey>();
    private List<PhysicalKey> _pressedLeftStickKeys = new List<PhysicalKey>();
    private readonly List<PhysicalKey> _pendingLeftStickModifierTaps = new List<PhysicalKey>();
    private readonly ClutchButtonStateMachine _clutchButton = new ClutchButtonStateMachine();
    private TouchGestureState _touchGesture = new TouchGestureState();
    private bool _touchAltTabAltDown;
    private bool _touchAltTabAltOwned;
    private bool _touchGestureBlockedUntilRelease;
    private bool _touchGestureBlockOwnsTouchpad;
    private bool _touchClickBlockedByGesture;
    private bool _prevTouchClick;
    private PhysicalKey _touchClickKey = PhysicalKey.None;
    private bool _touchClickKeyDown;
    private double _touchClickRepeatStartedMs;
    private double _touchClickNextRepeatMs;
    private bool _prevClutchActive;
    private bool _capsFnLayerActive;
    private bool _prevCreate;
    private bool _prevOptions;
    private bool _prevMute;
    private double _muteDownMs;
    private bool _muteLongPressTriggered;
    private bool _clutchToggleActionReleases;
    private bool _createKeyDown;
    private bool _optionsKeyDown;
    private bool _createPendingTap;
    private bool _optionsPendingTap;
    private volatile bool _manualVisible;
    private double _lastTickMs;
    private double _lastTickExceptionLogMs = -10000.0;

    public MapperForm(Config config) {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.Validate();
        _config = config;
        _hid = new DirectHidController();
        _debugSources = false;
        _enabled = config.Enabled;
        _injector = new InputInjector();
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        Opacity = 0;
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        _hid.Start();

        _lastTickMs = NowMs();
        _pollRunning = true;
        NativeMethods.timeBeginPeriod(1);
        _pollThread = new Thread(PollLoop);
        _pollThread.IsBackground = true;
        _pollThread.Priority = ThreadPriority.AboveNormal;
        _pollThread.Start();

        Thread guideThread = new Thread(() => {
            while (true) {
                try {
                    if (Console.KeyAvailable) {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Enter) {
                            if (_manualVisible) {
                                Program.PrintRunningHome(_config, _hid.DisplayName, _hid.State.Connected);
                                _manualVisible = false;
                            } else {
                                Program.PrintDetailedManual(_config);
                                _manualVisible = true;
                            }
                        } else if (key.Key == ConsoleKey.Escape) {
                            CloseFromConsoleThread();
                            break;
                        }
                    }
                } catch { }
                Thread.Sleep(100);
            }
        });
        guideThread.IsBackground = true;
        guideThread.Start();
    }

    private void CloseFromConsoleThread() {
        _manualVisible = false;
        try {
            if (IsHandleCreated) {
                BeginInvoke((MethodInvoker)delegate { Close(); });
            } else {
                Close();
            }
        } catch {
            try { Application.ExitThread(); } catch { }
        }
    }

    private void PollLoop() {
        double nextTick = _clock.Elapsed.TotalMilliseconds;
        while (_pollRunning) {
            RunTickSafely();
            
            double now = _clock.Elapsed.TotalMilliseconds;
            if (nextTick < now) {
                nextTick = now;
            }
            nextTick += PollIntervalMs;
            
            while (_pollRunning) {
                now = _clock.Elapsed.TotalMilliseconds;
                double diff = nextTick - now;
                if (diff <= 0) break;
                
                if (diff > 2.0) {
                    Thread.Sleep(1);
                } else if (diff > 0.1) {
                    Thread.Sleep(0);
                } else {
                    Thread.SpinWait(10);
                }
            }
        }
    }



    protected override void OnFormClosing(FormClosingEventArgs e) {
        _pollRunning = false;
        if (_pollThread != null) {
            _pollThread.Join(500);
        }
        NativeMethods.timeEndPeriod(1);
        _hid.Stop();
        try {
            ReleaseRuntimeHolds();
        } catch (Exception ex) {
            try { _injector.ReleaseAll(); } catch { }
            LogTickException(ex);
        }

        base.OnFormClosing(e);
    }

    private bool _prevL1, _prevR1;
    private double _l1DownMs, _r1DownMs, _l2DownMs, _r2DownMs;
    private double _l1UpMs, _r1UpMs, _l2UpMs, _r2UpMs;
    private Layer _previousActionLayer = Layer.Base;
    private Layer _lastReleasedActionLayer = Layer.Base;
    private double _lastReleasedActionLayerUpMs;
    private double _lastReleasedActionLayerDownMs;
    private bool _l1ConsumedByCombo, _r1ConsumedByCombo, _l2ConsumedByCombo, _r2ConsumedByCombo;
    private bool _l1HadLayerOverlap, _r1HadLayerOverlap, _l2HadLayerOverlap, _r2HadLayerOverlap;

    private void RunTickSafely() {
        try {
            OnTick();
        } catch (Exception ex) {
            try {
                ReleaseRuntimeHolds();
                _runtimeReleased = true;
            } catch {
            }
            LogTickException(ex);
        }
    }

    private void LogTickException(Exception ex) {
        double now = NowMs();
        if (now - _lastTickExceptionLogMs < 1000.0) return;
        _lastTickExceptionLogMs = now;
        try {
            Console.WriteLine("[warn] Released runtime input after mapper exception: " + ex.GetType().Name + ": " + ex.Message);
        } catch {
        }
    }

    private void OnTick() {
        ControllerState s = _hid.State;
        double now = NowMs();
        double deltaSec = Clamp((now - _lastTickMs) / 1000.0, 0.0, MaxMouseFrameSeconds);
        _lastTickMs = now;
        bool preL1 = _prevL1;
        bool preR1 = _prevR1;
        bool l1JustDown = s.L1 && !preL1;
        bool r1JustDown = s.R1 && !preR1;
        if (l1JustDown) { _l1DownMs = now; _l1UpMs = 0; _l1ConsumedByCombo = false; }
        if (r1JustDown) { _r1DownMs = now; _r1UpMs = 0; _r1ConsumedByCombo = false; }
        if (!s.L1 && preL1) { _l1UpMs = now; _l1ConsumedByCombo = false; }
        if (!s.R1 && preR1) { _r1UpMs = now; _r1ConsumedByCombo = false; }
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
            Program.PrintConnectedWelcome(_config, _hid.DisplayName);
            _manualVisible = false;
            _printedConnectedGuide = true;
        }
        UpdateSystemButtonPresses(s, now);
        UpdateSystemButtonReleases(s);
        UpdateTriggers(s, now);
        MarkActiveLayerOverlap();
        UpdateClutchButton(s, now);
        UpdateLeftStick(s, now);

        UpdateTouchpadClick(s, now);
        UpdateTouchGestures(s, now);
        FlushPendingModifierKeys();

        UpdateMouseButtons(s, now);
        FlushPendingMouseButtons(s, now);
        UpdateActionButtons(s, now);
        int wheelDelta = UpdateLeftStickScroll(s, deltaSec);
        int pointerX;
        int pointerY;
        UpdateRightStick(s, now, deltaSec, out pointerX, out pointerY);
        SendContinuousMouseOutputs(pointerX, pointerY, wheelDelta);
        ClearInactiveLayerOverlapFlags(s);
    }

    private void UpdateTriggers(ControllerState s, double now) {
        if (!_l2Pressed && IsTriggerPressed(s.L2, _config.TriggerPressThreshold)) {
            _l2Pressed = true;
            _l2DownMs = now;
            _l2UpMs = 0;
            _l2ConsumedByCombo = false;
        } else if (_l2Pressed && IsTriggerReleased(s.L2, _config.TriggerReleaseThreshold)) {
            _l2Pressed = false;
            _l2UpMs = now;
            _l2ConsumedByCombo = false;
        }

        if (!_r2Pressed && IsTriggerPressed(s.R2, _config.TriggerPressThreshold)) {
            _r2Pressed = true;
            _r2DownMs = now;
            _r2UpMs = 0;
            _r2ConsumedByCombo = false;
        } else if (_r2Pressed && IsTriggerReleased(s.R2, _config.TriggerReleaseThreshold)) {
            _r2Pressed = false;
            _r2UpMs = now;
            _r2ConsumedByCombo = false;
        }
    }

    internal static bool IsTriggerPressed(double value, double threshold) {
        double press = Math.Max(0.0, threshold);
        return value > press;
    }

    internal static bool IsTriggerReleased(double value, double threshold) {
        double release = Math.Max(0.0, threshold);
        return value <= release;
    }

    private void UpdateClutchButton(ControllerState s, double now) {
        bool wasToggled = _clutchButton.Toggled;
        _clutchButton.Update(s.Home, now, _config.ClutchLongPressMs);
        if (!wasToggled && _clutchButton.Toggled) {
            _clutchToggleActionReleases = _accumulatedModifiers.Count > 0;
        } else if (wasToggled && !_clutchButton.Toggled) {
            _clutchToggleActionReleases = false;
        }
    }

    private bool IsClutchActive() {
        return _clutchButton.Active;
    }

    private PhysicalKey GetLeftStickKey(StickDirection dir) {
        switch (dir) {
            case StickDirection.Up: return PhysicalKey.None; // Wheel Up
            case StickDirection.UpRight: return PhysicalKey.LWin;
            case StickDirection.DownRight: return PhysicalKey.LAlt;
            case StickDirection.Down: return PhysicalKey.None; // Wheel Down
            case StickDirection.DownLeft: return PhysicalKey.LCtrl;
            case StickDirection.UpLeft: return PhysicalKey.LShift;
            default: return PhysicalKey.None;
        }
    }

    private StickDirection ResolveLeftStickDirection(double x, double y) {
        double radius = Math.Sqrt(x * x + y * y);
        StickDirection candidate = Sector(x, y);
        if (radius < LeftStickEnterDeadzoneFor(candidate)) return StickDirection.None;

        return candidate;
    }

    private double LeftStickEnterDeadzoneFor(StickDirection direction) {
        return IsLeftStickModifierDirection(direction)
            ? Clamp(_config.LeftStickModifierEnterDeadzone, 0.0, 1.0)
            : Clamp(_config.LeftStickEnterDeadzone, 0.0, 1.0);
    }

    private void UpdateLeftStick(ControllerState s, double now) {
        StickDirection previous = _leftDirection;
        StickDirection next = ResolveLeftStickDirection(s.LX, s.LY);

        if (next != previous) {
            _leftDirection = next;
            _leftStickScroll.Reset();
        }

        bool clutch = IsClutchActive();
        bool clutchJustPressed = clutch && !_prevClutchActive;
        _prevClutchActive = clutch;

        List<PhysicalKey> desiredKeys = new List<PhysicalKey>();

        if (clutchJustPressed) {
            foreach (var key in _desiredLeftStickKeys) {
                AccumulateLeftStickKey(key);
            }
        }

        if (IsLeftStickModifierDirection(_leftDirection)) {
            PhysicalKey rawStickKey = GetLeftStickKey(_leftDirection);
            if (clutch) {
                AccumulateLeftStickKey(rawStickKey);
                desiredKeys.AddRange(_accumulatedModifiers);
            } else {
                _accumulatedModifiers.Clear();
                AddUnique(desiredKeys, rawStickKey);
            }
        } else {
            if (clutch) {
                desiredKeys.AddRange(_accumulatedModifiers);
            } else {
                _accumulatedModifiers.Clear();
            }
        }

        foreach (PhysicalKey key in _desiredLeftStickKeys) {
            if (ShouldQueueDeferredModifierTap(true, _pressedLeftStickKeys.Contains(key), desiredKeys.Contains(key))) {
                AddUnique(_pendingLeftStickModifierTaps, key);
            }
        }
        for (int i = _pressedLeftStickKeys.Count - 1; i >= 0; i--) {
            PhysicalKey key = _pressedLeftStickKeys[i];
            if (!desiredKeys.Contains(key)) {
                _injector.CurrentSource = "LeftStick";
                _injector.CurrentReason = "ModifierUp " + key;
                _injector.KeyUp(key);
                _pressedLeftStickKeys.RemoveAt(i);
            }
        }
        foreach (var key in desiredKeys) {
            if (!_desiredLeftStickKeys.Contains(key)) {
                RegisterModifierBinding(key, now);
            }
        }

        _desiredLeftStickKeys.Clear();
        _desiredLeftStickKeys.AddRange(desiredKeys);
    }

    private void FlushPendingModifierKeys() {
        bool hasPendingOutput = _pendingLeftStickModifierTaps.Count > 0 || _createPendingTap || _optionsPendingTap;
        foreach (PhysicalKey key in _desiredLeftStickKeys) {
            if (!_pressedLeftStickKeys.Contains(key)) {
                hasPendingOutput = true;
                break;
            }
        }
        hasPendingOutput |= _prevCreate && !_createKeyDown;
        hasPendingOutput |= _prevOptions && !_optionsKeyDown;
        if (!hasPendingOutput) return;
        foreach (PhysicalKey key in _pendingLeftStickModifierTaps) {
            EmitDeferredModifierTap("LeftStick", key);
        }
        _pendingLeftStickModifierTaps.Clear();

        foreach (PhysicalKey key in _desiredLeftStickKeys) {
            if (_pressedLeftStickKeys.Contains(key)) continue;
            _injector.CurrentSource = "LeftStick";
            _injector.CurrentReason = "ModifierDown " + key;
            _injector.KeyDown(key);
            _pressedLeftStickKeys.Add(key);
        }

        FlushPendingSystemModifier("Share/Create", PhysicalKey.RAlt, _prevCreate, ref _createKeyDown, ref _createPendingTap);
        FlushPendingSystemModifier("Options/Menu", PhysicalKey.RCtrl, _prevOptions, ref _optionsKeyDown, ref _optionsPendingTap);
    }

    private void EmitDeferredModifierTap(string source, PhysicalKey key) {
        _injector.CurrentSource = source;
        _injector.CurrentReason = "DeferredModifierTap " + key;
        _injector.KeyDown(key);
        _injector.KeyUp(key);
    }

    private void FlushPendingSystemModifier(string source, PhysicalKey key, bool logicallyDown, ref bool outputDown, ref bool pendingTap) {
        if (pendingTap) {
            EmitDeferredModifierTap(source, key);
            pendingTap = false;
        }
        if (!logicallyDown || outputDown) return;
        _injector.CurrentSource = source;
        _injector.CurrentReason = source + " press";
        _injector.KeyDown(key);
        outputDown = true;
    }

    private int UpdateLeftStickScroll(ControllerState s, double deltaSec) {
        if (_leftDirection != StickDirection.Up && _leftDirection != StickDirection.Down) {
            _leftStickScroll.Reset();
            return 0;
        }

        StickDirection scrollDirection = VerticalScrollDirection(s.LY);
        if (scrollDirection != StickDirection.Up && scrollDirection != StickDirection.Down) {
            _leftStickScroll.Reset();
            return 0;
        }

        int wheelDelta;
        double radius = Math.Sqrt(s.LX * s.LX + s.LY * s.LY);
        int scrollSign = scrollDirection == StickDirection.Up ? 1 : -1;
        if (_leftStickScroll.TryUpdate(radius, deltaSec, _config, scrollSign, out wheelDelta)) {
            return wheelDelta;
        }
        return 0;
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

    private ResolvedActionStroke ResolveActionStroke(KeyStroke stroke) {
        if (stroke.IsNone) return new ResolvedActionStroke(stroke, false);

        if (_capsFnLayerActive) {
            if (!stroke.Shift) {
                PhysicalKey fKey = TranslateToFKey(stroke.Key);
                if (fKey != PhysicalKey.None) {
                    return new ResolvedActionStroke(KeyStroke.Of(fKey), true);
                }
                if (IsLetterKey(stroke.Key)) {
                    return new ResolvedActionStroke(KeyStroke.Shifted(stroke.Key), true);
                }
            }
        }

        return new ResolvedActionStroke(stroke, false);
    }

    private static bool IsLetterKey(PhysicalKey key) {
        return key >= PhysicalKey.A && key <= PhysicalKey.Z;
    }

    private static bool IsFunctionKey(KeyStroke stroke) {
        return !stroke.Shift && stroke.Key >= PhysicalKey.F1 && stroke.Key <= PhysicalKey.F12;
    }

    private static void AddUnique(List<PhysicalKey> keys, PhysicalKey key) {
        if (key != PhysicalKey.None && !keys.Contains(key)) {
            keys.Add(key);
        }
    }

    private void AccumulateLeftStickKey(PhysicalKey key) {
        AddUnique(_accumulatedModifiers, key);
    }

    private static bool IsLeftStickModifierDirection(StickDirection dir) {
        return dir == StickDirection.UpLeft ||
               dir == StickDirection.DownLeft ||
               dir == StickDirection.UpRight ||
               dir == StickDirection.DownRight;
    }

    private void UpdateTouchpadClick(ControllerState s, double now) {
        if (s.TouchClick && !_prevTouchClick) {
            if ((_touchGesture.Completed && _touchGesture.Shortcut != TouchGestureShortcut.None) ||
                (_touchGestureBlockedUntilRelease && _touchGestureBlockOwnsTouchpad)) {
                _touchClickBlockedByGesture = true;
                _prevTouchClick = true;
                return;
            }

            ResetTouchGesture();
            TouchpadClickResolution click = ResolveTouchpadClick(s, _config);
            if (click.Kind == TouchpadClickKind.Key && click.Key != PhysicalKey.None) {
                PressTouchpadClickKey(click.Key, now);
            }
        } else if (!s.TouchClick && _prevTouchClick) {
            if (!_touchClickBlockedByGesture) {
                ReleaseTouchpadClickKey();
            }
            _touchClickBlockedByGesture = false;
        } else if (s.TouchClick && !_touchClickBlockedByGesture) {
            UpdateTouchpadClickRepeat(now);
        }

        _prevTouchClick = s.TouchClick;
    }

    private void PressTouchpadClickKey(PhysicalKey key, double now) {
        _injector.CurrentSource = "TouchpadClick";
        _injector.CurrentReason = "Touchpad click " + key;
        _injector.KeyDown(key);
        _touchClickKey = key;
        _touchClickKeyDown = true;
        if (IsTouchpadClickRepeatKey(key)) {
            _touchClickRepeatStartedMs = now;
            _touchClickNextRepeatMs = now + Math.Max(1, _config.RepeatDelayMs);
        }
    }

    private void ReleaseTouchpadClickKey() {
        if (!_touchClickKeyDown) return;
        _injector.CurrentSource = "TouchpadClick";
        _injector.CurrentReason = "Touchpad click release " + _touchClickKey;
        _injector.KeyUp(_touchClickKey);
        _touchClickKey = PhysicalKey.None;
        _touchClickKeyDown = false;
        _touchClickRepeatStartedMs = 0.0;
        _touchClickNextRepeatMs = 0.0;
    }

    private void UpdateTouchpadClickRepeat(double now) {
        if (!_touchClickKeyDown || !IsTouchpadClickRepeatKey(_touchClickKey)) return;
        if (now < _touchClickNextRepeatMs) return;
        _injector.CurrentSource = "TouchpadClick";
        _injector.CurrentReason = "Touchpad click progressive repeat " + _touchClickKey;
        _injector.KeyDown(_touchClickKey);

        double heldMs = Math.Max(0.0, now - _touchClickRepeatStartedMs);
        _touchClickNextRepeatMs = now + BaseRepeatIntervalMs(heldMs);
    }

    private static bool IsTouchpadClickRepeatKey(PhysicalKey key) {
        return key == PhysicalKey.Delete || key == PhysicalKey.Backspace;
    }

    private static TouchpadClickResolution ResolveTouchpadClick(ControllerState s, Config config) {
        const double touchpadWidth = 1920.0;
        double confirmedWidth = Clamp(config.TouchGestureSideConfirmedWidth, 1.0, touchpadWidth / 2.0);
        int activeTouchCount = 0;
        if (s.Touch1Active) activeTouchCount++;
        if (s.Touch2Active) activeTouchCount++;

        if (activeTouchCount == 0) return TouchpadClickResolution.ForKey(PhysicalKey.Backspace);
        if (activeTouchCount >= 2 || s.TouchCount >= 2) return TouchpadClickResolution.ForKey(PhysicalKey.Backspace);

        bool anyActive = false;
        bool leftConfirmed = false;
        bool rightConfirmed = false;
        AddTouchpadClickPoint(s.Touch1Active, s.Touch1X, confirmedWidth, touchpadWidth, ref anyActive, ref leftConfirmed, ref rightConfirmed);
        AddTouchpadClickPoint(s.Touch2Active, s.Touch2X, confirmedWidth, touchpadWidth, ref anyActive, ref leftConfirmed, ref rightConfirmed);

        if (leftConfirmed && rightConfirmed) return TouchpadClickResolution.ForKey(PhysicalKey.Backspace);
        if (leftConfirmed) return TouchpadClickResolution.ForKey(PhysicalKey.Delete);
        if (rightConfirmed) return TouchpadClickResolution.ForKey(PhysicalKey.Backspace);
        return anyActive ? TouchpadClickResolution.ForKey(PhysicalKey.CapsLock) : TouchpadClickResolution.None();
    }

    private static void AddTouchpadClickPoint(bool active, int x, double confirmedWidth, double touchpadWidth, ref bool anyActive, ref bool leftConfirmed, ref bool rightConfirmed) {
        if (!active) return;
        anyActive = true;
        if (x < confirmedWidth) leftConfirmed = true;
        else if (x >= touchpadWidth - confirmedWidth) rightConfirmed = true;
    }

    private void UpdateTouchGestures(ControllerState s, double now) {
        if (s.TouchClick && !_touchClickBlockedByGesture) {
            _touchGestureBlockedUntilRelease = false;
            _touchGestureBlockOwnsTouchpad = false;
            ResetTouchGesture();
            return;
        }

        int count = s.TouchCount;
        if (count <= 0) {
            _touchGestureBlockedUntilRelease = false;
            _touchGestureBlockOwnsTouchpad = false;
            ResetTouchGesture();
            return;
        }

        if (_touchGestureBlockedUntilRelease) return;

        if (!_touchGesture.Active) {
            StartTouchGesture(s, now);
            return;
        }

        UpdateTouchGestureFingers(ref _touchGesture, s, now);

        if (!_touchGesture.Completed) {
            if (!_touchGesture.Moving) {
                double primary = MaxTouchGesturePrimaryMovement(_touchGesture, s);
                if (IsTouchGestureStill(primary, _config)) return;
                _touchGesture.Moving = true;
            }

            TouchGestureRecognition recognition;
            if (!TryRecognizeTouchGesture(_touchGesture, s, _config, out recognition)) return;

            int fingers = _touchGesture.HadTwoFingers ? 2 : 1;
            TouchGestureShortcut shortcut;
            if (!TryResolveTouchGestureShortcut(fingers, recognition.Side, recognition.Direction, out shortcut)) {
                if (fingers == 2 && recognition.TwoFingerContinuation) {
                    ArmIgnoredTwoFingerContinuationSegment(recognition.Side, recognition.Direction, recognition.Finger, recognition.CurrentX, recognition.CurrentY, recognition.StaticFinger, recognition.StaticFingerId, recognition.StaticStartX, recognition.StaticStartY, now);
                    return;
                }
                BlockTouchGestureUntilRelease(false);
                return;
            }

            TouchGestureRepeatMode repeatMode = TouchGestureRepeatModeFor(shortcut);
            TriggerTouchGestureShortcut(shortcut);
            if (repeatMode == TouchGestureRepeatMode.None && fingers != 2) {
                BlockTouchGestureUntilRelease(true);
                return;
            }

            ArmTouchGestureRepeat(shortcut, repeatMode, fingers, recognition.Side, recognition.Direction, recognition.Finger, recognition.CurrentX, recognition.CurrentY, recognition.TwoFingerContinuation, recognition.StaticFinger, recognition.StaticFingerId, recognition.StaticStartX, recognition.StaticStartY, now);
            return;
        }

        UpdateTouchGestureRepeat(s, now);
    }

    private void ArmIgnoredTwoFingerContinuationSegment(TouchGestureSide side, TouchGestureDirection direction, int finger, int currentX, int currentY, int staticFinger, int staticFingerId, double staticStartX, double staticStartY, double now) {
        ReleaseTouchGestureModifiers();
        ArmTouchGestureRepeat(TouchGestureShortcut.None, TouchGestureRepeatMode.None, 2, side, direction, finger, currentX, currentY, true, staticFinger, staticFingerId, staticStartX, staticStartY, now);
    }

    private void StartTouchGesture(ControllerState s, double now) {
        _touchGesture.Reset();
        _touchGesture.Active = true;
        _touchGesture.FingerCount = s.TouchCount >= 2 ? 2 : 1;
        _touchGesture.HadTwoFingers = s.TouchCount >= 2;
        InitializeTouchGestureFingerStarts(ref _touchGesture, s, now);
        _touchGesture.Direction = TouchGestureDirection.None;
    }

    private void ResetTouchGesture() {
        ReleaseTouchGestureModifiers();
        _touchGesture.Reset();
    }

    private void BlockTouchGestureUntilRelease(bool ownsTouchpad) {
        ResetTouchGesture();
        _touchGestureBlockedUntilRelease = true;
        _touchGestureBlockOwnsTouchpad = ownsTouchpad;
    }

    private void UpdateTouchGestureRepeat(ControllerState s, double now) {
        if (_touchGesture.TwoFingerContinuation) {
            UpdateTwoFingerContinuationRepeat(s, now);
            return;
        }

        if (_touchGesture.FingerCount >= 2 && s.TouchCount < 2) {
            RestartTouchGestureFromCurrentContacts(s, now);
            return;
        }

        int currentX;
        int currentY;
        if (!TryGetTouchGestureFingerPosition(_touchGesture.ActiveFinger, s, out currentX, out currentY)) {
            ResetTouchGesture();
            return;
        }

        double stepStartX = _touchGesture.RepeatAnchorX;
        double dx = currentX - stepStartX;
        double dy = currentY - _touchGesture.RepeatAnchorY;
        TouchGestureDirection direction;
        double primary;
        double repeatDistance;
        if (TryResolveTouchGestureMovement(dx, dy, _config, true, out direction, out primary, out repeatDistance)) {
            SettleTouchGestureStep(ref _touchGesture, currentX, currentY);

            if (IsAltTabShortcut(_touchGesture.Shortcut)) {
                TriggerTouchGestureAltTabNavigation(direction, 1);
                ArmTouchGestureRepeat(_touchGesture.Shortcut, _touchGesture.RepeatMode, _touchGesture.FingerCount, _touchGesture.Side, direction, _touchGesture.ActiveFinger, currentX, currentY, _touchGesture.TwoFingerContinuation, _touchGesture.StaticFinger, _touchGesture.StaticFingerId, _touchGesture.StaticStartX, _touchGesture.StaticStartY, now);
                return;
            }

            if (_touchGesture.RepeatMode == TouchGestureRepeatMode.Timed &&
                AreTouchGestureDirectionsOnSameAxis(direction, _touchGesture.Direction) &&
                direction != _touchGesture.Direction) {
                TouchGestureShortcut shortcut;
                if (TryResolveTouchGestureShortcut(_touchGesture.FingerCount, _touchGesture.Side, direction, out shortcut) &&
                    TouchGestureRepeatModeFor(shortcut) == TouchGestureRepeatMode.Timed) {
                    TriggerTouchGestureShortcut(shortcut);
                    ArmTouchGestureRepeat(shortcut, TouchGestureRepeatMode.Timed, _touchGesture.FingerCount, _touchGesture.Side, direction, _touchGesture.ActiveFinger, currentX, currentY, _touchGesture.TwoFingerContinuation, _touchGesture.StaticFinger, _touchGesture.StaticFingerId, _touchGesture.StaticStartX, _touchGesture.StaticStartY, now);
                    return;
                }
            }
        }

        if (_touchGesture.RepeatMode == TouchGestureRepeatMode.Timed && now >= _touchGesture.NextTimedRepeatMs) {
            TriggerTouchGestureShortcut(_touchGesture.Shortcut);
            _touchGesture.NextTimedRepeatMs = now + Math.Max(1, TouchGestureTimeRepeatIntervalMsFor(_touchGesture.Shortcut));
        }
    }

    private void UpdateTwoFingerContinuationRepeat(ControllerState s, double now) {
        int staticX;
        int staticY;
        int staticFinger;
        if (!TryGetTwoFingerContinuationStatic(s, out staticFinger, out staticX, out staticY)) {
            BlockTouchGestureUntilRelease(true);
            return;
        }
        _touchGesture.StaticFinger = staticFinger;

        double staticPrimary = PrimaryMovement(staticX - _touchGesture.StaticStartX, staticY - _touchGesture.StaticStartY);
        if (!IsTouchGestureStill(staticPrimary, _config)) {
            BlockTouchGestureUntilRelease(true);
            return;
        }

        int currentX;
        int currentY;
        if (_touchGesture.TwoFingerContinuationAwaitingMoverRelease) {
            int activeFinger;
            if (TryGetTouchGestureFingerPositionById(_touchGesture.ActiveFingerId, s, out activeFinger, out currentX, out currentY)) {
                _touchGesture.ActiveFinger = activeFinger;
                return;
            }

            StopTwoFingerContinuationMover(s, now);
        }

        if (!TryGetActiveTwoFingerContinuationMover(s, now, out currentX, out currentY)) return;

        double stepStartX = _touchGesture.RepeatAnchorX;
        double dx = currentX - stepStartX;
        double dy = currentY - _touchGesture.RepeatAnchorY;
        TouchGestureDirection direction;
        double primary;
        double repeatDistance;
        bool movementReady = TryResolveTouchGestureMovement(dx, dy, _config, true, out direction, out primary, out repeatDistance);
        if (movementReady) {
            SettleTouchGestureStep(ref _touchGesture, currentX, currentY);
            RefreshTwoFingerContinuationStaticAnchor(staticFinger, _touchGesture.StaticFingerId, staticX, staticY);

            if (_touchGesture.Side == TouchGestureSide.None) {
                TouchGestureSide side = ResolveTouchGestureSide(stepStartX, direction, _config);
                TouchGestureShortcut shortcut;
                if (!TryResolveTouchGestureShortcut(2, side, direction, out shortcut)) {
                    ArmIgnoredTwoFingerContinuationSegment(side, direction, _touchGesture.ActiveFinger, currentX, currentY, _touchGesture.StaticFinger, _touchGesture.StaticFingerId, _touchGesture.StaticStartX, _touchGesture.StaticStartY, now);
                    return;
                }

                TouchGestureRepeatMode repeatMode = TouchGestureRepeatModeFor(shortcut);
                TriggerTouchGestureShortcut(shortcut);
                ArmTouchGestureRepeat(shortcut, repeatMode, 2, side, direction, _touchGesture.ActiveFinger, currentX, currentY, true, _touchGesture.StaticFinger, _touchGesture.StaticFingerId, _touchGesture.StaticStartX, _touchGesture.StaticStartY, now);
                return;
            }

            if (_touchGesture.RepeatMode == TouchGestureRepeatMode.Timed &&
                AreTouchGestureDirectionsOnSameAxis(direction, _touchGesture.Direction) &&
                direction != _touchGesture.Direction) {
                TouchGestureShortcut shortcut;
                if (TryResolveTouchGestureShortcut(2, _touchGesture.Side, direction, out shortcut) &&
                    TouchGestureRepeatModeFor(shortcut) == TouchGestureRepeatMode.Timed) {
                    TriggerTouchGestureShortcut(shortcut);
                    ArmTouchGestureRepeat(shortcut, TouchGestureRepeatMode.Timed, 2, _touchGesture.Side, direction, _touchGesture.ActiveFinger, currentX, currentY, true, _touchGesture.StaticFinger, _touchGesture.StaticFingerId, _touchGesture.StaticStartX, _touchGesture.StaticStartY, now);
                    return;
                }
            }
        }

        if (_touchGesture.RepeatMode == TouchGestureRepeatMode.Timed && now >= _touchGesture.NextTimedRepeatMs) {
            TriggerTouchGestureShortcut(_touchGesture.Shortcut);
            _touchGesture.NextTimedRepeatMs = now + Math.Max(1, TouchGestureTimeRepeatIntervalMsFor(_touchGesture.Shortcut));
        }
    }

    private bool TryGetActiveTwoFingerContinuationMover(ControllerState s, double now, out int currentX, out int currentY) {
        currentX = 0;
        currentY = 0;

        if (_touchGesture.ActiveFinger > 0 &&
            TryGetTouchGestureFingerPositionById(_touchGesture.ActiveFingerId, s, out int activeFinger, out currentX, out currentY)) {
            _touchGesture.ActiveFinger = activeFinger;
            return true;
        }

        if (_touchGesture.ActiveFinger > 0 || _touchGesture.ActiveFingerId >= 0) {
            StopTwoFingerContinuationMover(s, now);
        }
        int finger;
        if (!TryGetTwoFingerContinuationMover(_touchGesture.StaticFingerId, s, out finger, out currentX, out currentY)) return false;

        _touchGesture.ActiveFinger = finger;
        _touchGesture.ActiveFingerId = TouchGestureCurrentId(finger, s);
        if (TryGetTouchGestureTrackedStart(finger, out double startX, out double startY)) {
            _touchGesture.RepeatAnchorX = startX;
            _touchGesture.RepeatAnchorY = startY;
        } else {
            _touchGesture.RepeatAnchorX = currentX;
            _touchGesture.RepeatAnchorY = currentY;
        }
        return true;
    }

    private void RefreshTwoFingerContinuationStaticAnchor(int finger, int id, int x, int y) {
        _touchGesture.StaticFinger = finger;
        _touchGesture.StaticFingerId = id;
        _touchGesture.StaticStartX = x;
        _touchGesture.StaticStartY = y;
    }

    private void StopTwoFingerContinuationMover(ControllerState s, double now) {
        _touchGesture.ActiveFinger = 0;
        _touchGesture.ActiveFingerId = -1;
        _touchGesture.Shortcut = TouchGestureShortcut.None;
        _touchGesture.RepeatMode = TouchGestureRepeatMode.None;
        _touchGesture.Direction = TouchGestureDirection.None;
        _touchGesture.Side = TouchGestureSide.None;
        _touchGesture.TwoFingerContinuationAwaitingMoverRelease = false;
        int staticFinger;
        int staticX;
        int staticY;
        if (TryGetTwoFingerContinuationStatic(s, out staticFinger, out staticX, out staticY)) {
            RefreshTwoFingerContinuationStaticAnchor(staticFinger, _touchGesture.StaticFingerId, staticX, staticY);
        }
    }

    private int TouchGestureTrackedId(int finger) {
        if (finger == 1) return _touchGesture.Touch1Tracking ? _touchGesture.Touch1Id : -1;
        if (finger == 2) return _touchGesture.Touch2Tracking ? _touchGesture.Touch2Id : -1;
        return -1;
    }

    private static int TouchGestureCurrentId(int finger, ControllerState s) {
        if (finger == 1 && s.Touch1Active) return s.Touch1Id;
        if (finger == 2 && s.Touch2Active) return s.Touch2Id;
        return -1;
    }

    private bool TryGetTouchGestureTrackedStart(int finger, out double x, out double y) {
        if (finger == 1 && _touchGesture.Touch1Tracking) {
            x = _touchGesture.Touch1StartX;
            y = _touchGesture.Touch1StartY;
            return true;
        }
        if (finger == 2 && _touchGesture.Touch2Tracking) {
            x = _touchGesture.Touch2StartX;
            y = _touchGesture.Touch2StartY;
            return true;
        }

        x = 0.0;
        y = 0.0;
        return false;
    }

    private bool TryGetTwoFingerContinuationStatic(ControllerState s, out int finger, out int x, out int y) {
        if (TryGetTouchGestureFingerPositionById(_touchGesture.StaticFingerId, s, out finger, out x, out y)) {
            _touchGesture.StaticFinger = finger;
            return true;
        }
        finger = 0;
        x = 0;
        y = 0;
        return false;
    }

    private static bool TryGetTwoFingerContinuationMover(int staticFingerId, ControllerState s, out int finger, out int x, out int y) {
        finger = 0;
        x = 0;
        y = 0;
        if (s.Touch1Active && s.Touch1Id != staticFingerId) {
            finger = 1;
            x = s.Touch1X;
            y = s.Touch1Y;
            return true;
        }
        if (s.Touch2Active && s.Touch2Id != staticFingerId) {
            finger = 2;
            x = s.Touch2X;
            y = s.Touch2Y;
            return true;
        }
        return false;
    }

    private void RestartTouchGestureFromCurrentContacts(ControllerState s, double now) {
        ReleaseTouchGestureModifiers();
        _touchGesture.Reset();
        if (s.TouchCount > 0) StartTouchGesture(s, now);
    }

    private void ArmTouchGestureRepeat(TouchGestureShortcut shortcut, TouchGestureRepeatMode repeatMode, int fingers, TouchGestureSide side, TouchGestureDirection direction, int finger, double anchorX, double anchorY, bool twoFingerContinuation, int staticFinger, int staticFingerId, double staticStartX, double staticStartY, double now) {
        _touchGesture.Completed = true;
        _touchGesture.FingerCount = fingers;
        _touchGesture.Shortcut = shortcut;
        _touchGesture.RepeatMode = repeatMode;
        _touchGesture.TwoFingerContinuation = twoFingerContinuation;
        _touchGesture.StaticFinger = staticFinger;
        _touchGesture.StaticFingerId = staticFingerId;
        _touchGesture.StaticStartX = staticStartX;
        _touchGesture.StaticStartY = staticStartY;
        _touchGesture.Direction = direction;
        _touchGesture.Side = side;
        _touchGesture.ActiveFinger = finger;
        _touchGesture.ActiveFingerId = TouchGestureTrackedId(finger);
        _touchGesture.TwoFingerContinuationAwaitingMoverRelease = twoFingerContinuation && repeatMode == TouchGestureRepeatMode.None;
        _touchGesture.RepeatAnchorX = anchorX;
        _touchGesture.RepeatAnchorY = anchorY;
        _touchGesture.NextTimedRepeatMs = repeatMode == TouchGestureRepeatMode.Timed
            ? now + Math.Max(1, TouchGestureTimeRepeatDelayMsFor(shortcut))
            : 0.0;
    }

    private int TouchGestureTimeRepeatDelayMsFor(TouchGestureShortcut shortcut) {
        return IsDesktopSwitchShortcut(shortcut)
            ? _config.TouchGestureDesktopRepeatIntervalMs
            : _config.TouchGestureTimeRepeatDelayMs;
    }

    private int TouchGestureTimeRepeatIntervalMsFor(TouchGestureShortcut shortcut) {
        return IsDesktopSwitchShortcut(shortcut)
            ? _config.TouchGestureDesktopRepeatIntervalMs
            : _config.TouchGestureTimeRepeatIntervalMs;
    }

    private static bool IsDesktopSwitchShortcut(TouchGestureShortcut shortcut) {
        return shortcut == TouchGestureShortcut.PreviousDesktop ||
               shortcut == TouchGestureShortcut.NextDesktop;
    }

    private static bool IsAltTabShortcut(TouchGestureShortcut shortcut) {
        return shortcut == TouchGestureShortcut.PreviousAltTabWindow ||
               shortcut == TouchGestureShortcut.NextAltTabWindow;
    }

    private static double TouchGestureThresholdFor(Config config, TouchGestureDirection direction) {
        return IsVerticalTouchGestureDirection(direction)
            ? Math.Max(1.0, config.TouchGestureVerticalThreshold)
            : Math.Max(1.0, config.TouchGestureHorizontalThreshold);
    }

    private static double TouchGestureRepeatDistanceFor(Config config, TouchGestureDirection direction) {
        return IsVerticalTouchGestureDirection(direction)
            ? Math.Max(1.0, config.TouchGestureVerticalRepeatDistance)
            : Math.Max(1.0, config.TouchGestureHorizontalRepeatDistance);
    }

    private static bool IsVerticalTouchGestureDirection(TouchGestureDirection direction) {
        return direction == TouchGestureDirection.Up || direction == TouchGestureDirection.Down;
    }

    private static bool AreTouchGestureDirectionsOnSameAxis(TouchGestureDirection a, TouchGestureDirection b) {
        if (a == TouchGestureDirection.None || b == TouchGestureDirection.None) return false;
        return IsVerticalTouchGestureDirection(a) == IsVerticalTouchGestureDirection(b);
    }

    private static void SettleTouchGestureStep(ref TouchGestureState gesture, int currentX, int currentY) {
        gesture.RepeatAnchorX = currentX;
        gesture.RepeatAnchorY = currentY;
    }

    private static bool TryResolveTouchGestureMovement(double dx, double dy, Config config, bool repeat, out TouchGestureDirection direction, out double primary, out double requiredDistance) {
        direction = TouchGestureDirection.None;
        primary = 0.0;
        requiredDistance = 0.0;

        double absX = Math.Abs(dx);
        double absY = Math.Abs(dy);
        double horizontal = repeat
            ? TouchGestureRepeatDistanceFor(config, TouchGestureDirection.Left)
            : TouchGestureThresholdFor(config, TouchGestureDirection.Left);
        double vertical = repeat
            ? TouchGestureRepeatDistanceFor(config, TouchGestureDirection.Up)
            : TouchGestureThresholdFor(config, TouchGestureDirection.Up);
        bool horizontalReady = absX >= horizontal;
        bool verticalReady = absY >= vertical;
        if (!horizontalReady && !verticalReady) return false;

        if (horizontalReady && (!verticalReady || absX > absY)) {
            direction = dx < 0.0 ? TouchGestureDirection.Left : TouchGestureDirection.Right;
            primary = absX;
            requiredDistance = horizontal;
            return true;
        }

        direction = dy < 0.0 ? TouchGestureDirection.Up : TouchGestureDirection.Down;
        primary = absY;
        requiredDistance = vertical;
        return true;
    }

    private static bool TryRecognizeTouchGesture(TouchGestureState gesture, ControllerState s, Config config, out TouchGestureRecognition recognition) {
        recognition = new TouchGestureRecognition();

        if (gesture.HadTwoFingers) return TryRecognizeTwoFingerContinuationGesture(gesture, s, config, out recognition);

        double bestDx = 0.0;
        double bestDy = 0.0;
        double bestPrimary = 0.0;
        double bestStartX = 0.0;
        double bestStartY = 0.0;
        int bestCurrentX = 0;
        int bestCurrentY = 0;
        int bestFinger = 0;
        TouchGestureDirection bestDirection = TouchGestureDirection.None;
        ConsiderTouchGestureMovement(s.Touch1Active && gesture.Touch1Tracking, 1, gesture.Touch1StartX, gesture.Touch1StartY, s.Touch1X, s.Touch1Y, config, ref bestDx, ref bestDy, ref bestPrimary, ref bestDirection, ref bestStartX, ref bestStartY, ref bestCurrentX, ref bestCurrentY, ref bestFinger);
        ConsiderTouchGestureMovement(s.Touch2Active && gesture.Touch2Tracking, 2, gesture.Touch2StartX, gesture.Touch2StartY, s.Touch2X, s.Touch2Y, config, ref bestDx, ref bestDy, ref bestPrimary, ref bestDirection, ref bestStartX, ref bestStartY, ref bestCurrentX, ref bestCurrentY, ref bestFinger);
        if (bestFinger == 0) return false;

        recognition.Direction = bestDirection;
        recognition.Side = ResolveTouchGestureSide(bestStartX, bestDirection, config);
        recognition.Finger = bestFinger;
        recognition.StartX = bestStartX;
        recognition.StartY = bestStartY;
        recognition.CurrentX = bestCurrentX;
        recognition.CurrentY = bestCurrentY;
        recognition.PrimaryDistance = bestPrimary;
        return true;
    }

    private static bool TryRecognizeTwoFingerContinuationGesture(TouchGestureState gesture, ControllerState s, Config config, out TouchGestureRecognition recognition) {
        recognition = new TouchGestureRecognition();
        if (!(s.Touch1Active && s.Touch2Active && gesture.Touch1Tracking && gesture.Touch2Tracking)) return false;

        TouchGestureRecognition touch1Candidate;
        TouchGestureRecognition touch2Candidate;
        bool touch1Triggered = TryBuildTwoFingerContinuationRecognition(gesture, 1, gesture.Touch1StartX, gesture.Touch1StartY, s.Touch1X, s.Touch1Y, 2, gesture.Touch2Id, s.Touch2X, s.Touch2Y, config, out touch1Candidate);
        bool touch2Triggered = TryBuildTwoFingerContinuationRecognition(gesture, 2, gesture.Touch2StartX, gesture.Touch2StartY, s.Touch2X, s.Touch2Y, 1, gesture.Touch1Id, s.Touch1X, s.Touch1Y, config, out touch2Candidate);
        if (touch1Triggered == touch2Triggered) return false;

        recognition = touch1Triggered ? touch1Candidate : touch2Candidate;
        return true;
    }

    private static bool TryBuildTwoFingerContinuationRecognition(TouchGestureState gesture, int moverFinger, double moverStartX, double moverStartY, int moverCurrentX, int moverCurrentY, int staticFinger, int staticFingerId, double staticStartX, double staticStartY, Config config, out TouchGestureRecognition recognition) {
        recognition = new TouchGestureRecognition();
        double dx = moverCurrentX - moverStartX;
        double dy = moverCurrentY - moverStartY;
        TouchGestureDirection direction;
        double primary;
        double requiredDistance;
        if (!TryResolveTouchGestureMovement(dx, dy, config, false, out direction, out primary, out requiredDistance)) return false;

        TouchGestureSide side = ResolveTouchGestureSide(moverStartX, direction, config);

        recognition.TwoFingerContinuation = true;
        recognition.StaticFinger = staticFinger;
        recognition.StaticFingerId = staticFingerId;
        recognition.StaticStartX = staticStartX;
        recognition.StaticStartY = staticStartY;
        recognition.Direction = direction;
        recognition.Side = side;
        recognition.Finger = moverFinger;
        recognition.StartX = moverStartX;
        recognition.StartY = moverStartY;
        recognition.CurrentX = moverCurrentX;
        recognition.CurrentY = moverCurrentY;
        recognition.PrimaryDistance = primary;
        return true;
    }

    private static double MaxTouchGesturePrimaryMovement(TouchGestureState gesture, ControllerState s) {
        double bestPrimary = 0.0;
        if (s.Touch1Active && gesture.Touch1Tracking) {
            bestPrimary = Math.Max(bestPrimary, PrimaryMovement(s.Touch1X - gesture.Touch1StartX, s.Touch1Y - gesture.Touch1StartY));
        }
        if (s.Touch2Active && gesture.Touch2Tracking) {
            bestPrimary = Math.Max(bestPrimary, PrimaryMovement(s.Touch2X - gesture.Touch2StartX, s.Touch2Y - gesture.Touch2StartY));
        }
        return bestPrimary;
    }

    private static bool TryGetTouchGestureFingerPosition(int finger, ControllerState s, out int x, out int y) {
        if (finger == 1 && s.Touch1Active) {
            x = s.Touch1X;
            y = s.Touch1Y;
            return true;
        }
        if (finger == 2 && s.Touch2Active) {
            x = s.Touch2X;
            y = s.Touch2Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool TryGetTouchGestureFingerPositionById(int id, ControllerState s, out int finger, out int x, out int y) {
        if (id >= 0 && s.Touch1Active && s.Touch1Id == id) {
            finger = 1;
            x = s.Touch1X;
            y = s.Touch1Y;
            return true;
        }
        if (id >= 0 && s.Touch2Active && s.Touch2Id == id) {
            finger = 2;
            x = s.Touch2X;
            y = s.Touch2Y;
            return true;
        }

        finger = 0;
        x = 0;
        y = 0;
        return false;
    }

    private static void ConsiderTouchGestureMovement(bool active, int finger, double startX, double startY, int currentX, int currentY, Config config, ref double bestDx, ref double bestDy, ref double bestPrimary, ref TouchGestureDirection bestDirection, ref double bestStartX, ref double bestStartY, ref int bestCurrentX, ref int bestCurrentY, ref int bestFinger) {
        if (!active) return;
        double dx = currentX - startX;
        double dy = currentY - startY;
        TouchGestureDirection direction;
        double primary;
        double requiredDistance;
        if (!TryResolveTouchGestureMovement(dx, dy, config, false, out direction, out primary, out requiredDistance)) return;
        if (primary <= bestPrimary) return;
        bestDx = dx;
        bestDy = dy;
        bestPrimary = primary;
        bestDirection = direction;
        bestStartX = startX;
        bestStartY = startY;
        bestCurrentX = currentX;
        bestCurrentY = currentY;
        bestFinger = finger;
    }

    private static double PrimaryMovement(double dx, double dy) {
        double absX = Math.Abs(dx);
        double absY = Math.Abs(dy);
        return Math.Max(absX, absY);
    }

    private static bool IsTouchGestureStill(double primaryMovement, Config config) {
        return primaryMovement < TouchGestureStillDistance(config);
    }

    private static double TouchGestureStillDistance(Config config) {
        return Math.Max(1.0, config.TouchGestureHoldStillDistance);
    }

    private static TouchGestureSide ResolveTouchGestureSide(double startX, TouchGestureDirection initialDirection, Config config) {
        const double touchpadWidth = 1920.0;
        const double centerX = touchpadWidth / 2.0;
        double confirmedWidth = Clamp(config.TouchGestureSideConfirmedWidth, 1.0, touchpadWidth / 2.0);
        double rightBoundary = touchpadWidth - confirmedWidth;
        if (startX < confirmedWidth) return TouchGestureSide.Left;
        if (startX >= rightBoundary) return TouchGestureSide.Right;

        if (startX < centerX) {
            return initialDirection == TouchGestureDirection.Right
                ? TouchGestureSide.Right
                : TouchGestureSide.Left;
        }

        return initialDirection == TouchGestureDirection.Left
            ? TouchGestureSide.Left
            : TouchGestureSide.Right;
    }

    private void InitializeTouchGestureFingerStarts(ref TouchGestureState gesture, ControllerState s, double now) {
        if (s.Touch1Active) {
            gesture.Touch1Tracking = true;
            gesture.Touch1Id = s.Touch1Id;
            gesture.Touch1StartX = s.Touch1X;
            gesture.Touch1StartY = s.Touch1Y;
        }
        if (s.Touch2Active) {
            gesture.Touch2Tracking = true;
            gesture.Touch2Id = s.Touch2Id;
            gesture.Touch2StartX = s.Touch2X;
            gesture.Touch2StartY = s.Touch2Y;
        }
    }

    private void UpdateTouchGestureFingers(ref TouchGestureState gesture, ControllerState s, double now) {
        if (!gesture.Completed && s.TouchCount >= 2) gesture.HadTwoFingers = true;

        if (s.Touch1Active) {
            if (!gesture.Touch1Tracking || gesture.Touch1Id != s.Touch1Id) {
                gesture.Touch1Tracking = true;
                gesture.Touch1Id = s.Touch1Id;
                gesture.Touch1StartX = s.Touch1X;
                gesture.Touch1StartY = s.Touch1Y;
            }
        } else {
            gesture.Touch1Tracking = false;
            gesture.Touch1Id = -1;
        }

        if (s.Touch2Active) {
            if (!gesture.Touch2Tracking || gesture.Touch2Id != s.Touch2Id) {
                gesture.Touch2Tracking = true;
                gesture.Touch2Id = s.Touch2Id;
                gesture.Touch2StartX = s.Touch2X;
                gesture.Touch2StartY = s.Touch2Y;
            }
        } else {
            gesture.Touch2Tracking = false;
            gesture.Touch2Id = -1;
        }
    }

    private static bool TryResolveTouchGestureShortcut(int fingers, TouchGestureSide side, TouchGestureDirection direction, out TouchGestureShortcut shortcut) {
        shortcut = TouchGestureShortcut.None;
        if (fingers == 1) {
            if (side == TouchGestureSide.Left) {
                switch (direction) {
                    case TouchGestureDirection.Up:
                        shortcut = TouchGestureShortcut.PreviousWindow;
                        return true;
                    case TouchGestureDirection.Down:
                        shortcut = TouchGestureShortcut.NextWindow;
                        return true;
                    case TouchGestureDirection.Left:
                        shortcut = TouchGestureShortcut.PreviousAltTabWindow;
                        return true;
                    case TouchGestureDirection.Right:
                        shortcut = TouchGestureShortcut.NextAltTabWindow;
                        return true;
                }
            } else if (side == TouchGestureSide.Right) {
                switch (direction) {
                    case TouchGestureDirection.Left:
                        shortcut = TouchGestureShortcut.PreviousDesktop;
                        return true;
                    case TouchGestureDirection.Right:
                        shortcut = TouchGestureShortcut.NextDesktop;
                        return true;
                    case TouchGestureDirection.Up:
                        shortcut = TouchGestureShortcut.RestoreMinimizedWindows;
                        return true;
                    case TouchGestureDirection.Down:
                        shortcut = TouchGestureShortcut.MinimizeAllWindows;
                        return true;
                }
            }
        } else if (fingers == 2) {
            if (side == TouchGestureSide.Left) {
                switch (direction) {
                    case TouchGestureDirection.Up:
                        shortcut = TouchGestureShortcut.PreviousTab;
                        return true;
                    case TouchGestureDirection.Down:
                        shortcut = TouchGestureShortcut.NextTab;
                        return true;
                    case TouchGestureDirection.Left:
                        shortcut = TouchGestureShortcut.BackNavigation;
                        return true;
                    case TouchGestureDirection.Right:
                        shortcut = TouchGestureShortcut.ForwardNavigation;
                        return true;
                }
            } else if (side == TouchGestureSide.Right) {
                switch (direction) {
                    case TouchGestureDirection.Up:
                        shortcut = TouchGestureShortcut.Screenshot;
                        return true;
                    case TouchGestureDirection.Down:
                        shortcut = TouchGestureShortcut.CloseWindow;
                        return true;
                    case TouchGestureDirection.Left:
                        shortcut = TouchGestureShortcut.SnapWindowLeft;
                        return true;
                    case TouchGestureDirection.Right:
                        shortcut = TouchGestureShortcut.SnapWindowRight;
                        return true;
                }
            }
        }

        return false;
    }

    private static TouchGestureRepeatMode TouchGestureRepeatModeFor(TouchGestureShortcut shortcut) {
        switch (shortcut) {
            case TouchGestureShortcut.PreviousWindow:
            case TouchGestureShortcut.NextWindow:
            case TouchGestureShortcut.PreviousDesktop:
            case TouchGestureShortcut.NextDesktop:
            case TouchGestureShortcut.PreviousTab:
            case TouchGestureShortcut.NextTab:
            case TouchGestureShortcut.BackNavigation:
            case TouchGestureShortcut.ForwardNavigation:
                return TouchGestureRepeatMode.Timed;
            case TouchGestureShortcut.PreviousAltTabWindow:
            case TouchGestureShortcut.NextAltTabWindow:
                return TouchGestureRepeatMode.Distance;
            default:
                return TouchGestureRepeatMode.None;
        }
    }

    private void TriggerTouchGestureShortcut(TouchGestureShortcut shortcut) {
        _injector.CurrentSource = "TouchGesture";
        _injector.CurrentReason = "Touch " + shortcut;

        switch (shortcut) {
            case TouchGestureShortcut.PreviousWindow:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.Escape, true, false, true, false);
                break;
            case TouchGestureShortcut.NextWindow:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.Escape, false, false, true, false);
                break;
            case TouchGestureShortcut.PreviousAltTabWindow:
                EnterTouchGestureAltTabSwitcher(true);
                break;
            case TouchGestureShortcut.NextAltTabWindow:
                EnterTouchGestureAltTabSwitcher(false);
                break;
            case TouchGestureShortcut.PreviousDesktop:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowLeft, false, true, false, true);
                break;
            case TouchGestureShortcut.NextDesktop:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowRight, false, true, false, true);
                break;
            case TouchGestureShortcut.SnapWindowLeft:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowLeft, false, false, false, true);
                break;
            case TouchGestureShortcut.SnapWindowRight:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowRight, false, false, false, true);
                break;
            case TouchGestureShortcut.Screenshot:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.S, true, false, false, true);
                break;
            case TouchGestureShortcut.RestoreMinimizedWindows:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.M, true, false, false, true);
                break;
            case TouchGestureShortcut.CloseWindow:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.F4, false, false, true, false);
                break;
            case TouchGestureShortcut.MinimizeAllWindows:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.M, false, false, false, true);
                break;
            case TouchGestureShortcut.PreviousTab:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.Tab, true, true, false, false);
                break;
            case TouchGestureShortcut.NextTab:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.Tab, false, true, false, false);
                break;
            case TouchGestureShortcut.BackNavigation:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowLeft, false, false, true, false);
                break;
            case TouchGestureShortcut.ForwardNavigation:
                ReleaseTouchGestureModifiers();
                _injector.KeyTapExact(PhysicalKey.ArrowRight, false, false, true, false);
                break;
        }
    }

    private void EnterTouchGestureAltTabSwitcher(bool previous) {
        _injector.CurrentSource = "TouchGesture";
        _injector.CurrentReason = previous ? "Touch Alt+Shift+Tab enter" : "Touch Alt+Tab enter";

        if (!_touchAltTabAltDown) {
            if (!_injector.IsKeyHeld(PhysicalKey.LAlt)) {
                _injector.KeyDown(PhysicalKey.LAlt);
                _touchAltTabAltOwned = true;
            } else {
                _touchAltTabAltOwned = false;
            }
            _touchAltTabAltDown = true;
        }

        _injector.KeyTapExact(PhysicalKey.Tab, previous, false, true, false);
    }

    private void TriggerTouchGestureAltTabNavigation(TouchGestureDirection direction, int count) {
        if (count <= 0) return;

        PhysicalKey key = TouchGestureArrowKey(direction);
        if (key == PhysicalKey.None) return;
        if (!_touchAltTabAltDown) {
            _injector.CurrentSource = "TouchGesture";
            _injector.CurrentReason = "Touch Alt hold for Alt-Tab navigation";
            if (!_injector.IsKeyHeld(PhysicalKey.LAlt)) {
                _injector.KeyDown(PhysicalKey.LAlt);
                _touchAltTabAltOwned = true;
            } else {
                _touchAltTabAltOwned = false;
            }
            _touchAltTabAltDown = true;
        }

        for (int i = 0; i < count; i++) {
            _injector.CurrentSource = "TouchGesture";
            _injector.CurrentReason = "Touch Alt-Tab navigation " + direction;
            _injector.KeyTapExact(key, false, false, true, false);
        }
    }

    private static PhysicalKey TouchGestureArrowKey(TouchGestureDirection direction) {
        switch (direction) {
            case TouchGestureDirection.Left: return PhysicalKey.ArrowLeft;
            case TouchGestureDirection.Right: return PhysicalKey.ArrowRight;
            case TouchGestureDirection.Up: return PhysicalKey.ArrowUp;
            case TouchGestureDirection.Down: return PhysicalKey.ArrowDown;
            default: return PhysicalKey.None;
        }
    }

    private void ReleaseTouchGestureModifiers() {
        if (_touchAltTabAltDown) {
            if (_touchAltTabAltOwned) {
                _injector.CurrentSource = "TouchGesture";
                _injector.CurrentReason = "Touch Alt release";
                _injector.KeyUp(PhysicalKey.LAlt);
            }
            _touchAltTabAltDown = false;
            _touchAltTabAltOwned = false;
        }
    }

    private void UpdateActionButtons(ControllerState s, double now) {
        bool[] currentDown = new bool[] { s.Up, s.Right, s.Square, s.Triangle, s.Left, s.Down, s.Cross, s.Circle };
        Layer rawLayer = _mapping.Resolve(s.L1, s.R1, _l2Pressed, _r2Pressed, _l1DownMs, _r1DownMs, _l2DownMs, _r2DownMs, _config.ComboLayerWindowMs);
        Layer layer = rawLayer;
        ConsumeComboComponents(layer);
        layer = FilterConsumedSingleLayer(layer, s.L1, s.R1, _l2Pressed, _r2Pressed);
        double layerMs = LayerTimestamp(layer);
        RememberReleasedActionLayer(layer, now);

        for (int i = 0; i < 8; i++) {
            bool prev = _prevDown[i];
            bool curr = currentDown[i];
            ButtonHold hold = _holds[i];
            ResolvedActionStroke layerAction = ResolveActionStroke(_mapping.Lookup(layer, (ActionButton)i));
            KeyStroke layerKey = layerAction.Stroke;

            if (hold.Pending) {
                if (!curr && !hold.PendingReleased) {
                    hold.PendingReleased = true;
                }
                UpdatePendingLayer(ref hold, layer, layerMs, now);

                bool shouldFlushPending = now - hold.PendingSinceMs >= PendingActionDecisionDelayMs(_config) &&
                    !ShouldWaitForPendingSingleLayerToSettle(hold, now);
                if (!shouldFlushPending) {
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                bool releasedPending = hold.PendingReleased || !curr;
                Layer resolvedLayer = hold.PendingLayer != Layer.Base && hold.PendingLayer != Layer.Reserved
                    ? hold.PendingLayer
                    : ((releasedPending || hold.PendingLayerSettled) ? hold.PendingLayer : layer);
                ResolvedActionStroke resolvedLayerAction = ResolveActionStroke(_mapping.Lookup(resolvedLayer, (ActionButton)i));
                KeyStroke resolvedLayerKey = resolvedLayerAction.Stroke;
                if (!resolvedLayerKey.IsNone) {
                    if (resolvedLayer != Layer.Base || IsFunctionKey(resolvedLayerKey)) {
                        TapStickyActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i) + " virtual tap", resolvedLayerAction.FnTranslated, hold.PendingModifierMask);
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
                        PressStickyActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i), ref hold, resolvedLayer, false, now, resolvedLayerAction.FnTranslated, hold.PendingModifierMask);
                        ReleaseActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i) + " release after layer settle");
                        ReleaseBoundModifiers(hold.StickyModifierMask, ActionSource(i));
                        _holds[i] = new ButtonHold();
                        _prevDown[i] = curr;
                        continue;
                    }

                    hold.Pending = false;
                    hold.PendingReleased = false;
                    PressStickyActionKey(i, resolvedLayerKey, "Button " + ActionButtonName(i), ref hold, resolvedLayer, IsBaseRepeatableAction(i, resolvedLayer), now, resolvedLayerAction.FnTranslated, hold.PendingModifierMask);
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                _holds[i] = new ButtonHold();
                _prevDown[i] = curr;
                continue;
            }

            if (!prev && curr) {
                Layer initialLayer = InitialActionLayer(layer, now, layerMs);
                ResolvedActionStroke initialAction = ResolveActionStroke(_mapping.Lookup(initialLayer, (ActionButton)i));
                KeyStroke key = initialAction.Stroke;
                if (ShouldDeferInitialAction(initialLayer)) {
                    hold = new ButtonHold();
                    hold.Pending = true;
                    hold.OriginalPendingLayer = initialLayer;
                    hold.PendingLayer = initialLayer;
                    hold.PendingLayerMs = initialLayer == layer ? layerMs : LayerTimestamp(initialLayer);
                    hold.PendingSinceMs = now;
                    hold.PendingModifierMask = CaptureCurrentBindingModifiers();
                    InitializePendingTrace(ref hold, layer, layerMs, now);
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                hold.Key = key;
                hold.KeyIsDown = false;

                if (!key.IsNone) {
                    if (layer != Layer.Base || IsFunctionKey(key)) {
                        TapActionKey(i, key, "Button " + ActionButtonName(i) + " virtual tap", initialAction.FnTranslated);
                        hold.SuppressUntilRelease = true;
                    } else {
                        PressActionKey(i, key, "Button " + ActionButtonName(i), ref hold, layer, IsBaseRepeatableAction(i, layer), now, initialAction.FnTranslated);
                    }
                }

                _holds[i] = hold;
            } else if (prev && !curr) {
                // 1 -> 0: KeyUp edge
                if (hold.KeyIsDown) {
                    ReleaseActionKey(i, hold.Key, "Button " + ActionButtonName(i) + " release");
                    ReleaseBoundModifiers(hold.StickyModifierMask, ActionSource(i));
                }
                _holds[i] = new ButtonHold();
            } else if (prev && curr) {
                if (hold.SuppressUntilRelease) {
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                if (hold.KeyIsDown || hold.BoundRepeatPulse) {
                    UpdateBaseRepeat(i, ref hold, now);
                    _holds[i] = hold;
                    _prevDown[i] = curr;
                    continue;
                }

                KeyStroke currentLayerKey = layerKey;

                if (hold.Key != currentLayerKey) {
                    if (layer != Layer.Base || IsFunctionKey(currentLayerKey)) {
                        if (!currentLayerKey.IsNone) {
                            TapActionKey(i, currentLayerKey, "Button " + ActionButtonName(i) + " layer change virtual tap", layerAction.FnTranslated);
                            hold.Key = currentLayerKey;
                            hold.KeyLayer = layer;
                            hold.KeyIsDown = false;
                            hold.RepeatEnabled = false;
                            hold.SuppressUntilRelease = true;
                            _holds[i] = hold;
                            _prevDown[i] = curr;
                            continue;
                        }
                    }

                    if (!currentLayerKey.IsNone) {
                        PressActionKey(i, currentLayerKey, "Button " + ActionButtonName(i) + " layer change press", ref hold, layer, IsBaseRepeatableAction(i, layer), now, layerAction.FnTranslated);
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

    private bool ShouldDeferInitialAction(Layer initialLayer) {
        return PendingActionDecisionDelayMs(_config) > 0.0;
    }

    internal static double PendingActionDecisionDelayMs(Config config) {
        return Math.Max(Math.Max(0.0, (double)config.ActionLayerGraceMs), Math.Max(0.0, (double)config.ModifierBindingWindowMs));
    }

    internal static bool ShouldQueueDeferredModifierTap(bool wasLogicallyDown, bool outputDown, bool isLogicallyDown) {
        return wasLogicallyDown && !outputDown && !isLogicallyDown;
    }

    internal static bool IsWithinModifierBindingWindow(double actionDownMs, double modifierDownMs, Config config) {
        double windowMs = Math.Max(0.0, (double)config.ModifierBindingWindowMs);
        return windowMs > 0.0 &&
            modifierDownMs >= actionDownMs &&
            modifierDownMs - actionDownMs <= windowMs;
    }

    private bool ShouldWaitForPendingSingleLayerToSettle(ButtonHold hold, double now) {
        if (hold.PendingLayer == Layer.Base || hold.PendingLayer == Layer.Reserved || IsComboLayer(hold.PendingLayer)) return false;
        if (hold.PendingLayerMs <= hold.PendingSinceMs) return false;
        return now - hold.PendingLayerMs <= _config.ComboLayerWindowMs;
    }

    private void ConsumeComboComponents(Layer layer) {
        switch (layer) {
            case Layer.R1L1:
                _r1ConsumedByCombo = true;
                _l1ConsumedByCombo = true;
                break;
            case Layer.R2L2:
                _r2ConsumedByCombo = true;
                _l2ConsumedByCombo = true;
                break;
            case Layer.L1R2:
                _l1ConsumedByCombo = true;
                _r2ConsumedByCombo = true;
                break;
            case Layer.R1L2:
                _r1ConsumedByCombo = true;
                _l2ConsumedByCombo = true;
                break;
        }
    }

    private Layer FilterConsumedSingleLayer(Layer layer, bool l1, bool r1, bool l2, bool r2) {
        if (IsComboLayer(layer) || layer == Layer.Base || layer == Layer.Reserved) return layer;
        if (!IsConsumedSingleLayer(layer)) return layer;
        return LatestUnconsumedSingleLayer(l1, r1, l2, r2);
    }

    private bool IsConsumedSingleLayer(Layer layer) {
        switch (layer) {
            case Layer.L1: return _l1ConsumedByCombo;
            case Layer.R1: return _r1ConsumedByCombo;
            case Layer.L2: return _l2ConsumedByCombo;
            case Layer.R2: return _r2ConsumedByCombo;
            default: return false;
        }
    }

    private Layer LatestUnconsumedSingleLayer(bool l1, bool r1, bool l2, bool r2) {
        Layer layer = Layer.Base;
        double bestMs = double.NegativeInfinity;
        ConsiderUnconsumedSingle(l1 && !_l1ConsumedByCombo, Layer.L1, _l1DownMs, ref layer, ref bestMs);
        ConsiderUnconsumedSingle(r1 && !_r1ConsumedByCombo, Layer.R1, _r1DownMs, ref layer, ref bestMs);
        ConsiderUnconsumedSingle(l2 && !_l2ConsumedByCombo, Layer.L2, _l2DownMs, ref layer, ref bestMs);
        ConsiderUnconsumedSingle(r2 && !_r2ConsumedByCombo, Layer.R2, _r2DownMs, ref layer, ref bestMs);
        return layer;
    }

    private static void ConsiderUnconsumedSingle(bool active, Layer candidate, double timestampMs, ref Layer layer, ref double bestMs) {
        if (!active) return;
        if (timestampMs >= bestMs) {
            layer = candidate;
            bestMs = timestampMs;
        }
    }

    private void MarkActiveLayerOverlap() {
        int activeCount = 0;
        if (_prevL1) activeCount++;
        if (_prevR1) activeCount++;
        if (_l2Pressed) activeCount++;
        if (_r2Pressed) activeCount++;
        if (activeCount <= 1) return;

        if (_prevL1) _l1HadLayerOverlap = true;
        if (_prevR1) _r1HadLayerOverlap = true;
        if (_l2Pressed) _l2HadLayerOverlap = true;
        if (_r2Pressed) _r2HadLayerOverlap = true;
    }

    private void ClearInactiveLayerOverlapFlags(ControllerState s) {
        if (!s.L1) _l1HadLayerOverlap = false;
        if (!s.R1) _r1HadLayerOverlap = false;
        if (!_l2Pressed) _l2HadLayerOverlap = false;
        if (!_r2Pressed) _r2HadLayerOverlap = false;
    }

    private bool LayerHadInputOverlap(Layer layer) {
        switch (layer) {
            case Layer.L1: return _l1HadLayerOverlap;
            case Layer.R1: return _r1HadLayerOverlap;
            case Layer.L2: return _l2HadLayerOverlap;
            case Layer.R2: return _r2HadLayerOverlap;
            case Layer.R1L1: return _r1HadLayerOverlap || _l1HadLayerOverlap;
            case Layer.R2L2: return _r2HadLayerOverlap || _l2HadLayerOverlap;
            case Layer.L1R2: return _l1HadLayerOverlap || _r2HadLayerOverlap;
            case Layer.R1L2: return _r1HadLayerOverlap || _l2HadLayerOverlap;
            default: return false;
        }
    }

    private void ClearLastReleasedActionLayer() {
        _lastReleasedActionLayer = Layer.Base;
        _lastReleasedActionLayerUpMs = 0.0;
        _lastReleasedActionLayerDownMs = 0.0;
    }

    internal static bool IsSubsetOf(Layer sub, Layer super) {
        if (sub == super) return true;
        if (sub == Layer.Base || sub == Layer.Reserved) return true;
        if (super == Layer.Base || super == Layer.Reserved) return false;

        if (super == Layer.R1L1) return sub == Layer.R1 || sub == Layer.L1;
        if (super == Layer.R2L2) return sub == Layer.R2 || sub == Layer.L2;
        if (super == Layer.L1R2) return sub == Layer.L1 || sub == Layer.R2;
        if (super == Layer.R1L2) return sub == Layer.R1 || sub == Layer.L2;

        return false;
    }

    private void RememberReleasedActionLayer(Layer layer, double now) {
        if (_previousActionLayer != layer && _previousActionLayer != Layer.Base && _previousActionLayer != Layer.Reserved) {
            double upMs = LayerUpTimestamp(_previousActionLayer);
            if (upMs <= 0.0) {
                _previousActionLayer = layer;
                return;
            }

            if (LayerHadInputOverlap(_previousActionLayer)) {
                ClearLastReleasedActionLayer();
                _previousActionLayer = layer;
                return;
            }
            
            bool shouldOverwrite = true;
            if (IsComboLayer(_lastReleasedActionLayer)) {
                if (IsSubsetOf(_previousActionLayer, _lastReleasedActionLayer)) {
                    if (LayerTimestamp(_previousActionLayer) <= _lastReleasedActionLayerUpMs) {
                        shouldOverwrite = false;
                    }
                }
            }

            if (shouldOverwrite && now - upMs < 100.0) {
                _lastReleasedActionLayer = _previousActionLayer;
                _lastReleasedActionLayerUpMs = upMs;
                _lastReleasedActionLayerDownMs = LayerTimestamp(_previousActionLayer);
            }
        }
        _previousActionLayer = layer;
    }

    private Layer InitialActionLayer(Layer layer, double now, double layerMs) {
        return ResolveInitialActionLayer(layer, layerMs, _lastReleasedActionLayer, _lastReleasedActionLayerDownMs, now, _lastReleasedActionLayerUpMs, _config.ActionLayerPostGraceMs);
    }

    internal static Layer ResolveInitialActionLayer(Layer layer, double layerDownMs, Layer lastReleasedLayer, double lastReleasedLayerDownMs, double now, double lastReleasedLayerUpMs, double postGraceMs) {
        if (lastReleasedLayer != Layer.Base && lastReleasedLayer != Layer.Reserved && (now - lastReleasedLayerUpMs <= postGraceMs)) {
            if (IsSubsetOf(layer, lastReleasedLayer)) {
                return lastReleasedLayer;
            }
            if (lastReleasedLayerDownMs > layerDownMs) {
                return lastReleasedLayer;
            }
        }
        return layer;
    }

    private double LayerTimestamp(Layer layer) {
        switch (layer) {
            case Layer.L1: return _l1DownMs;
            case Layer.R1: return _r1DownMs;
            case Layer.L2: return _l2DownMs;
            case Layer.R2: return _r2DownMs;
            case Layer.R1L1: return Math.Max(_r1DownMs, _l1DownMs);
            case Layer.R2L2: return Math.Max(_r2DownMs, _l2DownMs);
            case Layer.L1R2: return Math.Max(_l1DownMs, _r2DownMs);
            case Layer.R1L2: return Math.Max(_r1DownMs, _l2DownMs);
            default: return 0.0;
        }
    }

    private double ComboUpTimestamp(double t1, double t2) {
        if (t1 == 0 && t2 == 0) return 0.0;
        if (t1 == 0) return t2;
        if (t2 == 0) return t1;
        return Math.Min(t1, t2);
    }

    private double LayerUpTimestamp(Layer layer) {
        switch (layer) {
            case Layer.L1: return _l1UpMs;
            case Layer.R1: return _r1UpMs;
            case Layer.L2: return _l2UpMs;
            case Layer.R2: return _r2UpMs;
            case Layer.R1L1: return ComboUpTimestamp(_r1UpMs, _l1UpMs);
            case Layer.R2L2: return ComboUpTimestamp(_r2UpMs, _l2UpMs);
            case Layer.L1R2: return ComboUpTimestamp(_l1UpMs, _r2UpMs);
            case Layer.R1L2: return ComboUpTimestamp(_r1UpMs, _l2UpMs);
            default: return 0.0;
        }
    }

    private void UpdatePendingLayer(ref ButtonHold hold, Layer layer, double layerMs, double now) {
        List<PendingLayerOccupancySegment> occupiedSegments = PendingTraceSegmentsThrough(hold, layer, layerMs, now);
        PendingLayerOccupancyTrace trace = TracePendingLayerOccupancy(occupiedSegments, layer, _config.ActionLayerGraceMs);
        Layer next = ResolvePendingLayer(
            hold.PendingLayer,
            hold.OriginalPendingLayer,
            hold.PendingSinceMs,
            layer,
            layerMs,
            trace.ReachesPendingStart,
            _config.ActionLayerGraceMs);

        CommitPendingTraceTransition(ref hold, layer, layerMs, now);

        if (next == hold.PendingLayer) return;
        hold.PendingLayerSettled = next == Layer.Base && hold.PendingLayer != Layer.Base;
        hold.PendingLayer = next;
        hold.PendingLayerMs = next == layer ? layerMs : LayerTimestamp(next);
    }

    private void InitializePendingTrace(ref ButtonHold hold, Layer layer, double layerMs, double now) {
        ClearPendingLayerOccupancy(ref hold);
        hold.PendingTraceLayer = layer;
        hold.PendingTraceLayerMs = layerMs;
        hold.PendingTraceStartMs = now;
    }

    private List<PendingLayerOccupancySegment> PendingTraceSegmentsThrough(ButtonHold hold, Layer layer, double layerMs, double now) {
        List<PendingLayerOccupancySegment> segments = new List<PendingLayerOccupancySegment>();
        if (hold.PendingLayerOccupancySegments != null) {
            segments.AddRange(hold.PendingLayerOccupancySegments);
        }

        if (layer != hold.PendingTraceLayer) {
            AddPendingTraceSegment(segments, hold.PendingTraceLayer, hold.PendingTraceLayerMs, hold.PendingTraceStartMs, PendingTraceTransitionMs(hold, layer, layerMs, now));
        }

        return segments;
    }

    private void CommitPendingTraceTransition(ref ButtonHold hold, Layer layer, double layerMs, double now) {
        if (layer == hold.PendingTraceLayer) return;

        List<PendingLayerOccupancySegment> segments = PendingTraceSegmentsThrough(hold, layer, layerMs, now);
        SetPendingLayerOccupancySegments(ref hold, segments, SumPendingLayerOccupancy(segments));
        double transitionMs = PendingTraceTransitionMs(hold, layer, layerMs, now);
        hold.PendingTraceLayer = layer;
        hold.PendingTraceLayerMs = layerMs;
        hold.PendingTraceStartMs = transitionMs;
    }

    private double PendingTraceTransitionMs(ButtonHold hold, Layer layer, double layerMs, double now) {
        if (layer != Layer.Base && layer != Layer.Reserved && layerMs > 0.0) return layerMs;

        double traceLayerUpMs = LayerUpTimestamp(hold.PendingTraceLayer);
        if (traceLayerUpMs > 0.0) return traceLayerUpMs;
        return now;
    }

    private static void AddPendingTraceSegment(List<PendingLayerOccupancySegment> segments, Layer layer, double layerDownMs, double startMs, double endMs) {
        if (endMs <= startMs) return;
        bool isLayerBody = layer != Layer.Base && layer != Layer.Reserved;
        segments.Add(new PendingLayerOccupancySegment(endMs - startMs, isLayerBody, layer, layerDownMs));
    }

    private static void ClearPendingLayerOccupancy(ref ButtonHold hold) {
        hold.PendingLayerOccupancyMs = 0.0;
        if (hold.PendingLayerOccupancySegments != null) {
            hold.PendingLayerOccupancySegments.Clear();
        }
    }

    private static void AddPendingLayerOccupancySegment(ref ButtonHold hold, double durationMs, bool isLayerBody, Layer layer, double layerDownMs) {
        if (durationMs <= 0.0) return;
        if (hold.PendingLayerOccupancySegments == null) {
            hold.PendingLayerOccupancySegments = new List<PendingLayerOccupancySegment>();
        }
        hold.PendingLayerOccupancySegments.Add(new PendingLayerOccupancySegment(durationMs, isLayerBody, layer, layerDownMs));
        hold.PendingLayerOccupancyMs += durationMs;
    }

    private void NormalizePendingLayerOccupancy(ref ButtonHold hold) {
        if (hold.PendingLayerOccupancySegments == null) return;
        List<PendingLayerOccupancySegment> segments = new List<PendingLayerOccupancySegment>(hold.PendingLayerOccupancySegments);
        SetPendingLayerOccupancySegments(ref hold, segments, SumPendingLayerOccupancy(segments));
    }

    private void SetPendingLayerOccupancySegments(ref ButtonHold hold, List<PendingLayerOccupancySegment> segments, double occupiedMs) {
        if (segments == null || segments.Count == 0 || occupiedMs <= 0.0) {
            ClearPendingLayerOccupancy(ref hold);
            return;
        }
        hold.PendingLayerOccupancySegments = segments;
        hold.PendingLayerOccupancyMs = occupiedMs;
    }

    private static double SumPendingLayerOccupancy(List<PendingLayerOccupancySegment> segments) {
        double total = 0.0;
        for (int i = 0; i < segments.Count; i++) {
            total += segments[i].DurationMs;
        }
        return total;
    }

    private PendingLayerOccupancyTrace TracePendingLayerOccupancy(List<PendingLayerOccupancySegment> segments, Layer targetLayer, double actionLayerGraceMs) {
        double cutoffMs = Math.Max(0.0, (double)_config.LayerOccupancyCarryCutoffMs);
        double bodyTakeoverMs = Math.Max(0.0, (double)_config.LayerTakeoverWindowMs);
        double totalWindowMs = Math.Max(0.0, actionLayerGraceMs);
        if (totalWindowMs <= 0.0) return new PendingLayerOccupancyTrace(0.0, segments.Count == 0);

        double actualMs = 0.0;
        double cumulativeBodyMs = 0.0;
        for (int i = segments.Count - 1; i >= 0; i--) {
            PendingLayerOccupancySegment segment = segments[i];
            double remainingMs = totalWindowMs - actualMs;
            if (remainingMs <= 0.0) return new PendingLayerOccupancyTrace(actualMs, false);

            bool countsAsLayerBody = segment.IsLayerBody && !IsComboComponentBodyForTarget(targetLayer, segment);
            if (countsAsLayerBody && cutoffMs > 0.0 && cumulativeBodyMs + segment.DurationMs >= cutoffMs) {
                double cumulativeBodyBudgetMs = bodyTakeoverMs > cumulativeBodyMs ? bodyTakeoverMs - cumulativeBodyMs : 0.0;
                double readableBoundaryBodyMs = Math.Min(segment.DurationMs, cumulativeBodyBudgetMs);
                double takenMs = Math.Min(readableBoundaryBodyMs, remainingMs);
                actualMs += takenMs;
                bool reachesPendingStart = i == 0 && takenMs >= segment.DurationMs;
                return new PendingLayerOccupancyTrace(actualMs, reachesPendingStart);
            }

            if (segment.DurationMs > remainingMs) {
                actualMs += remainingMs;
                return new PendingLayerOccupancyTrace(actualMs, false);
            }

            actualMs += segment.DurationMs;
            if (countsAsLayerBody) cumulativeBodyMs += segment.DurationMs;
        }

        return new PendingLayerOccupancyTrace(actualMs, true);
    }

    private bool IsComboComponentBodyForTarget(Layer targetLayer, PendingLayerOccupancySegment segment) {
        if (!IsComboLayer(targetLayer) || IsComboLayer(segment.Layer) || !IsComboComponent(targetLayer, segment.Layer)) return false;
        double currentComponentDownMs = LayerTimestamp(segment.Layer);
        return currentComponentDownMs > 0.0 && currentComponentDownMs == segment.LayerDownMs;
    }

    private static bool IsComboComponent(Layer combo, Layer single) {
        if (combo == Layer.R1L1 && (single == Layer.R1 || single == Layer.L1)) return true;
        if (combo == Layer.R2L2 && (single == Layer.R2 || single == Layer.L2)) return true;
        if (combo == Layer.L1R2 && (single == Layer.L1 || single == Layer.R2)) return true;
        if (combo == Layer.R1L2 && (single == Layer.R1 || single == Layer.L2)) return true;
        return false;
    }

    internal static Layer ResolvePendingLayer(Layer pendingLayer, Layer originalLayer, double pendingSinceMs, Layer layer, double layerMs, bool reachesPendingStart, double actionLayerGraceMs) {
        if (layer == Layer.Base || layer == Layer.Reserved) return pendingLayer;
        if (layer == pendingLayer) return pendingLayer;
        if (layerMs < pendingSinceMs) return pendingLayer;

        bool layerCombo = IsComboLayer(layer);

        // 如果当前层是组合层，并且之前的 pendingLayer 是这个组合层的一个组件（即它的单层意图被组合层覆盖了）
        // 那么必须剥夺 pendingLayer 的资格，将其回退到 originalLayer 进行判定。
        Layer effectivePendingLayer = pendingLayer;
        if (layerCombo && IsComboComponent(layer, pendingLayer)) {
            if (layerMs - pendingSinceMs > actionLayerGraceMs) {
                return originalLayer;
            }
            if (!IsComboLayer(originalLayer)) {
                return layer;
            }
            effectivePendingLayer = originalLayer;
        }

        if (layerMs - pendingSinceMs > actionLayerGraceMs) return pendingLayer;

        if (!reachesPendingStart) {
            return effectivePendingLayer;
        }

        return layer;
    }

    private static bool IsComboLayer(Layer layer) {
        return layer == Layer.R1L1 || layer == Layer.R2L2 || layer == Layer.L1R2 || layer == Layer.R1L2;
    }

    private void PressStickyActionKey(int index, KeyStroke key, string reason, ref ButtonHold hold, Layer keyLayer, bool repeatable, double now, bool fnTranslated, int modifierMask) {
        if (ShouldPulseBoundRepeat(repeatable, modifierMask)) {
            EmitBoundActionPulse(index, key, reason + " bound pulse", modifierMask);
            CompleteAction(fnTranslated, ClutchConsumer.ActionPosition);
            hold.Key = key;
            hold.KeyLayer = keyLayer;
            hold.KeyIsDown = false;
            hold.StickyModifierMask = modifierMask;
            hold.RepeatEnabled = true;
            hold.BoundRepeatPulse = true;
            hold.KeyDownMs = now;
            hold.RepeatStartedMs = now;
            hold.NextRepeatMs = now + Math.Max(1, _config.RepeatDelayMs);
            return;
        }

        AcquireBoundModifiers(modifierMask, ActionSource(index));
        PressActionKey(index, key, reason, ref hold, keyLayer, repeatable, now, fnTranslated);
        hold.StickyModifierMask = modifierMask;
    }

    internal static bool ShouldPulseBoundRepeat(bool repeatable, int modifierMask) {
        return repeatable && modifierMask != 0;
    }

    private void EmitBoundActionPulse(int index, KeyStroke key, string reason, int modifierMask) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        AcquireBoundModifiers(modifierMask, source);
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        DebugSources("Source=" + source + " Button=" + btn + " Mode=BoundPulse -> " + MappingEngine.KeyName(key));
        if (key.Shift) _injector.KeyDown(PhysicalKey.LShift);
        _injector.KeyDown(key.Key);
        _injector.KeyUp(key.Key);
        if (key.Shift) _injector.KeyUp(PhysicalKey.LShift);
        ReleaseBoundModifiers(modifierMask, source);
    }

    private void TapStickyActionKey(int index, KeyStroke key, string reason, bool fnTranslated, int modifierMask) {
        AcquireBoundModifiers(modifierMask, ActionSource(index));
        TapActionKey(index, key, reason, fnTranslated);
        ReleaseBoundModifiers(modifierMask, ActionSource(index));
    }

    private void RegisterModifierBinding(PhysicalKey key, double now) {
        int bit = ModifierBindingBit(key);
        if (bit == 0) return;

        for (int i = 0; i < _holds.Length; i++) {
            ButtonHold hold = _holds[i];
            if (!hold.Pending || !IsWithinModifierBindingWindow(hold.PendingSinceMs, now, _config)) continue;
            hold.PendingModifierMask |= bit;
            _holds[i] = hold;
        }

        RegisterPendingMouseModifier(bit, now, ref _leftMouseButton);
        RegisterPendingMouseModifier(bit, now, ref _rightMouseButton);
    }

    private int CaptureCurrentBindingModifiers() {
        int mask = 0;
        foreach (PhysicalKey key in _desiredLeftStickKeys) {
            mask |= ModifierBindingBit(key);
        }
        if (_prevCreate) mask |= ModifierBindingBit(PhysicalKey.RAlt);
        if (_prevOptions) mask |= ModifierBindingBit(PhysicalKey.RCtrl);
        return mask;
    }

    private void RegisterPendingMouseModifier(int bit, double now, ref MouseButtonHold hold) {
        if (!hold.Pending || !IsWithinModifierBindingWindow(hold.PendingSinceMs, now, _config)) return;
        hold.PendingModifierMask |= bit;
    }

    private void AcquireBoundModifiers(int mask, string source) {
        PhysicalKey[] keys = ModifierBindingKeys();
        for (int i = 0; i < keys.Length; i++) {
            PhysicalKey key = keys[i];
            if ((mask & ModifierBindingBit(key)) == 0) continue;
            _injector.CurrentSource = source;
            _injector.CurrentReason = "Sticky modifier acquire " + key;
            _injector.KeyDown(key);
        }
    }

    private void ReleaseBoundModifiers(int mask, string source) {
        PhysicalKey[] keys = ModifierBindingKeys();
        for (int i = keys.Length - 1; i >= 0; i--) {
            PhysicalKey key = keys[i];
            if ((mask & ModifierBindingBit(key)) == 0) continue;
            _injector.CurrentSource = source;
            _injector.CurrentReason = "Sticky modifier release " + key;
            _injector.KeyUp(key);
        }
    }

    private static PhysicalKey[] ModifierBindingKeys() {
        return new PhysicalKey[] {
            PhysicalKey.LShift,
            PhysicalKey.LCtrl,
            PhysicalKey.LWin,
            PhysicalKey.LAlt,
            PhysicalKey.RAlt,
            PhysicalKey.RCtrl
        };
    }

    internal static bool IsModifierBindingKey(PhysicalKey key) {
        return ModifierBindingBit(key) != 0;
    }

    private static int ModifierBindingBit(PhysicalKey key) {
        switch (key) {
            case PhysicalKey.LShift: return 1 << 0;
            case PhysicalKey.LCtrl: return 1 << 1;
            case PhysicalKey.LWin: return 1 << 2;
            case PhysicalKey.LAlt: return 1 << 3;
            case PhysicalKey.RAlt: return 1 << 4;
            case PhysicalKey.RCtrl: return 1 << 5;
            default: return 0;
        }
    }

    private void PressActionKey(int index, KeyStroke key, string reason, ref ButtonHold hold, Layer keyLayer, bool repeatable, double now, bool fnTranslated) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Held -> " + MappingEngine.KeyName(key) + "Down");
        if (key.Shift) _injector.KeyDown(PhysicalKey.LShift);
        _injector.KeyDown(key.Key);
        hold.Key = key;
        hold.KeyLayer = keyLayer;
        hold.KeyIsDown = true;
        hold.RepeatEnabled = repeatable;
        hold.BoundRepeatPulse = false;
        hold.KeyDownMs = now;
        hold.RepeatStartedMs = now;
        hold.NextRepeatMs = now + Math.Max(1, _config.RepeatDelayMs);
        CompleteAction(fnTranslated, ClutchConsumer.ActionPosition);
    }

    private void TapActionKey(int index, KeyStroke key, string reason, bool fnTranslated) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Tap -> " + MappingEngine.KeyName(key));
        _injector.KeyTap(key.Key, key.Shift, false, false, false);
        CompleteAction(fnTranslated, ClutchConsumer.ActionPosition);
    }

    private void CompleteAction(bool fnTranslated, ClutchConsumer clutchConsumer) {
        if (_capsFnLayerActive && ShouldDeactivateCapsFnAfterAction(fnTranslated)) {
            DeactivateCapsFnLayer("Caps/Fn translated action complete");
        }
        ReleaseClutchAfterAction(clutchConsumer);
    }

    internal static bool ShouldDeactivateCapsFnAfterAction(bool fnTranslated) {
        return fnTranslated;
    }

    internal static bool CanConsumeClutchToggle(ClutchConsumer consumer) {
        return consumer == ClutchConsumer.ActionPosition || consumer == ClutchConsumer.MouseButton;
    }

    private void ReleaseClutchAfterAction(ClutchConsumer consumer) {
        if (!CanConsumeClutchToggle(consumer)) return;
        if (_clutchButton.Toggled && _clutchToggleActionReleases) {
            _clutchButton.DeactivateToggle();
            _clutchToggleActionReleases = false;
        }
    }

    private void ReleaseActionKey(int index, KeyStroke key, string reason) {
        string source = ActionSource(index);
        string btn = ActionButtonName(index);
        DebugSources("Source=" + source + " Button=" + btn + " Mode=Held -> " + MappingEngine.KeyName(key) + "Up");
        _injector.CurrentSource = source;
        _injector.CurrentReason = reason;
        _injector.KeyUp(key.Key);
        if (key.Shift) _injector.KeyUp(PhysicalKey.LShift);
    }

    private void UpdateBaseRepeat(int index, ref ButtonHold hold, double now) {
        if (!hold.RepeatEnabled || (!hold.KeyIsDown && !hold.BoundRepeatPulse) || hold.Key.IsNone) return;
        if (now < hold.NextRepeatMs) return;
        if (hold.BoundRepeatPulse) {
            EmitBoundActionPulse(index, hold.Key, "Button " + ActionButtonName(index) + " progressive bound repeat", hold.StickyModifierMask);
        } else {
            string source = ActionSource(index);
            string btn = ActionButtonName(index);
            _injector.CurrentSource = source;
            _injector.CurrentReason = "Button " + btn + " progressive repeat";
            DebugSources("Source=" + source + " Button=" + btn + " Mode=Repeat -> " + MappingEngine.KeyName(hold.Key) + "Down");
            _injector.KeyDown(hold.Key.Key);
        }

        double heldMs = Math.Max(0.0, now - hold.RepeatStartedMs);
        double interval = BaseRepeatIntervalMs(heldMs);
        hold.NextRepeatMs = now + interval;
    }

    private static bool IsBaseRepeatableAction(int index, Layer layer) {
        return layer == Layer.Base && ActionSource(index) == "DPad";
    }

    private double BaseRepeatIntervalMs(double heldMs) {
        double fastFreq = 1000.0 / Math.Max(5.0, (double)_config.RepeatIntervalMs);
        double slowFreq = 1000.0 / Math.Max(5.0, (double)_config.BaseRepeatSlowIntervalMs);
        double ramp = Math.Max(1.0, (double)_config.BaseRepeatRampMs);
        double t = Clamp((heldMs - _config.RepeatDelayMs) / ramp, 0.0, 1.0);
        double freq = slowFreq + (fastFreq - slowFreq) * Math.Pow(t, 3.0);
        return 1000.0 / Math.Max(0.1, freq);
    }

    private static string ActionSource(int index) {
        return (index < 2 || index == 4 || index == 5) ? "DPad" : "FaceButton";
    }

    private static string ActionButtonName(int index) {
        return ((ActionButton)index).ToString();
    }

    private void UpdateMouseButtons(ControllerState s, double now) {
        UpdateMouseButton(0, "L3", s.L3, now, ref _leftMouseButton);
        UpdateMouseButton(1, "R3", s.R3, now, ref _rightMouseButton);
    }

    private void UpdateMouseButton(int button, string name, bool down, double now, ref MouseButtonHold hold) {
        if (down && !hold.PreviousPhysicalDown) {
            hold.Pending = true;
            hold.PendingReleased = false;
            hold.PendingSinceMs = now;
            hold.PendingModifierMask = CaptureCurrentBindingModifiers();
        } else if (!down && hold.PreviousPhysicalDown) {
            if (hold.Pending) {
                hold.PendingReleased = true;
            } else if (hold.OutputDown) {
                ReleaseMouseButton(button, name, ref hold);
            }
        }
        hold.PreviousPhysicalDown = down;
    }

    private void FlushPendingMouseButtons(ControllerState s, double now) {
        FlushPendingMouseButton(0, "L3", s.L3, now, ref _leftMouseButton);
        FlushPendingMouseButton(1, "R3", s.R3, now, ref _rightMouseButton);
    }

    private void FlushPendingMouseButton(int button, string name, bool physicallyDown, double now, ref MouseButtonHold hold) {
        if (!hold.Pending || now - hold.PendingSinceMs < Math.Max(0, _config.ModifierBindingWindowMs)) return;
        bool released = hold.PendingReleased || !physicallyDown;
        int modifierMask = hold.PendingModifierMask;
        hold.Pending = false;
        hold.PendingReleased = false;
        hold.PendingSinceMs = 0.0;
        hold.PendingModifierMask = 0;

        AcquireBoundModifiers(modifierMask, "StickClick");
        _injector.CurrentSource = "StickClick";
        _injector.CurrentReason = name;
        _injector.MouseButton(button, true);
        hold.OutputDown = true;
        hold.StickyModifierMask = modifierMask;
        if (button == 1) _mouseFreezeUntilMs = now + _config.R3FreezeMs;
        ReleaseClutchAfterAction(ClutchConsumer.MouseButton);

        if (released) ReleaseMouseButton(button, name, ref hold);
    }

    private void ReleaseMouseButton(int button, string name, ref MouseButtonHold hold) {
        _injector.CurrentSource = "StickClick";
        _injector.CurrentReason = name + " release";
        _injector.MouseButton(button, false);
        ReleaseBoundModifiers(hold.StickyModifierMask, "StickClick");
        hold.OutputDown = false;
        hold.StickyModifierMask = 0;
    }

    private void UpdateRightStick(ControllerState s, double now, double deltaSec, out int ix, out int iy) {
        ix = 0;
        iy = 0;
        if (now < _mouseFreezeUntilMs) {
            _rightStickMouse.Reset();
            return;
        }
        if (_rightStickMouse.TryUpdate(s.RX, s.RY, deltaSec, _config, out ix, out iy)) {
        }
    }

    private void SendContinuousMouseOutputs(int pointerX, int pointerY, int wheelDelta) {
        bool hasPointer = pointerX != 0 || pointerY != 0;
        if (!hasPointer && wheelDelta == 0) return;

        if (wheelDelta != 0) {
            _injector.CurrentSource = "LeftStick";
            _injector.CurrentReason = "AnalogScroll";
            _injector.MouseWheelDelta(wheelDelta);
        }
        if (hasPointer) {
            _injector.CurrentSource = "RightStick";
            _injector.CurrentReason = "Mouse Move";
            _injector.MouseMove(pointerX, pointerY);
        }
    }

    private void UpdateSystemButtonPresses(ControllerState s, double now) {
        UpdateMappedSystemButtonPress(s.Create, PhysicalKey.RAlt, now, ref _prevCreate);
        UpdateMappedSystemButtonPress(s.Options, PhysicalKey.RCtrl, now, ref _prevOptions);
    }

    private void UpdateSystemButtonReleases(ControllerState s) {
        UpdateMappedSystemButtonRelease("Share/Create", s.Create, PhysicalKey.RAlt, ref _prevCreate, ref _createKeyDown, ref _createPendingTap);
        UpdateMappedSystemButtonRelease("Options/Menu", s.Options, PhysicalKey.RCtrl, ref _prevOptions, ref _optionsKeyDown, ref _optionsPendingTap);
    }

    private void UpdateMappedSystemButtonPress(bool down, PhysicalKey key, double now, ref bool prevDown) {
        if (down && !prevDown) {
            prevDown = true;
            RegisterModifierBinding(key, now);
        }
    }

    private void UpdateMappedSystemButtonRelease(string source, bool down, PhysicalKey key, ref bool prevDown, ref bool keyDown, ref bool pendingTap) {
        if (!down && prevDown) {
            if (keyDown) {
                _injector.CurrentSource = source;
                _injector.CurrentReason = source + " release";
                _injector.KeyUp(key);
                keyDown = false;
            } else if (ShouldQueueDeferredModifierTap(prevDown, keyDown, down)) {
                pendingTap = true;
            }
            prevDown = false;
        }
    }

    private void ActivateCapsFnLayer(string source) {
        _injector.CurrentSource = source;
        _injector.CurrentReason = "Caps/Fn layer on";
        _capsFnLayerActive = true;
    }

    private void ToggleCapsFnLayer(string source) {
        if (_capsFnLayerActive) {
            DeactivateCapsFnLayer("Mute short press Caps/Fn layer off");
        } else {
            ActivateCapsFnLayer(source);
        }
    }

    private void DeactivateCapsFnLayer(string reason) {
        if (!_capsFnLayerActive) return;
        _injector.CurrentSource = "CapsFnLayer";
        _injector.CurrentReason = reason;
        _capsFnLayerActive = false;
    }

    private void UpdateEmergency(ControllerState s, double now) {
        bool down = s.Mute;
        if (down && !_prevMute) {
            _muteDownMs = now;
            _muteLongPressTriggered = false;
        } else if (down && _prevMute) {
            if (!_muteLongPressTriggered && now - _muteDownMs >= Math.Max(1, _config.ClutchLongPressMs)) {
                _muteLongPressTriggered = true;
                _enabled = !_enabled;
                if (!_enabled) {
                    ReleaseRuntimeHolds();
                    _runtimeReleased = true;
                }
            }
        } else if (!down && _prevMute) {
            if (!_muteLongPressTriggered && _enabled) {
                ToggleCapsFnLayer("Mute");
            }
            _muteDownMs = 0.0;
            _muteLongPressTriggered = false;
        }
        _prevMute = down;
    }

    private void ReleaseRuntimeHolds() {
        ReleaseHeldActionKeys();
        ReleaseTouchGestureModifiers();
        ReleaseTouchpadClickKey();
        if (_leftMouseButton.OutputDown) ReleaseMouseButton(0, "L3 runtime", ref _leftMouseButton);
        if (_rightMouseButton.OutputDown) ReleaseMouseButton(1, "R3 runtime", ref _rightMouseButton);
        _injector.ReleaseAll();
        _leftMouseButton = new MouseButtonHold();
        _rightMouseButton = new MouseButtonHold();
        _leftDirection = StickDirection.None;
        _leftStickScroll.Reset();
        _desiredLeftStickKeys.Clear();
        _pressedLeftStickKeys.Clear();
        _pendingLeftStickModifierTaps.Clear();
        _accumulatedModifiers.Clear();
        DeactivateCapsFnLayer("Runtime release Caps/Fn layer");
        for (int i = 0; i < _holds.Length; i++) _holds[i] = new ButtonHold();
        for (int i = 0; i < _prevDown.Length; i++) _prevDown[i] = false;
        _prevL1 = false;
        _prevR1 = false;
        _l1DownMs = 0;
        _r1DownMs = 0;
        _l2DownMs = 0;
        _r2DownMs = 0;
        _l1UpMs = 0;
        _r1UpMs = 0;
        _l2UpMs = 0;
        _r2UpMs = 0;
        _previousActionLayer = Layer.Base;
        _lastReleasedActionLayer = Layer.Base;
        _lastReleasedActionLayerUpMs = 0;
        _lastReleasedActionLayerDownMs = 0;
        _l1ConsumedByCombo = false;
        _r1ConsumedByCombo = false;
        _l2ConsumedByCombo = false;
        _r2ConsumedByCombo = false;
        _l1HadLayerOverlap = false;
        _r1HadLayerOverlap = false;
        _l2HadLayerOverlap = false;
        _r2HadLayerOverlap = false;
        _l2Pressed = false;
        _r2Pressed = false;
        _clutchButton.Reset();
        _clutchToggleActionReleases = false;
        _prevClutchActive = false;
        _touchGesture.Reset();
        _touchGestureBlockedUntilRelease = false;
        _touchGestureBlockOwnsTouchpad = false;
        _touchClickBlockedByGesture = false;
        _prevTouchClick = false;
        _prevCreate = false;
        _prevOptions = false;
        _mouseFreezeUntilMs = 0;
        _rightStickMouse.Reset();
        _createKeyDown = false;
        _optionsKeyDown = false;
        _createPendingTap = false;
        _optionsPendingTap = false;
        _muteDownMs = 0.0;
    }

    private void ReleaseHeldActionKeys() {
        for (int i = 0; i < _holds.Length; i++) {
            if (_holds[i].KeyIsDown) {
                ReleaseActionKey(i, _holds[i].Key, "Runtime release " + ActionButtonName(i));
                ReleaseBoundModifiers(_holds[i].StickyModifierMask, ActionSource(i));
            }
            _holds[i] = new ButtonHold();
        }
    }

    private double NowMs() { return _clock.Elapsed.TotalMilliseconds; }

    public static StickDirection Sector(double x, double y) {
        double angle = Math.Atan2(-y, x);
        if (angle < 0.0) angle += Math.PI * 2.0;

        double degrees = angle * 180.0 / Math.PI;
        if (degrees < 65.0) return StickDirection.UpRight;
        if (degrees < 115.0) return StickDirection.Up;
        if (angle < Math.PI) return StickDirection.UpLeft;
        if (degrees < 245.0) return StickDirection.DownLeft;
        if (degrees < 295.0) return StickDirection.Down;
        return StickDirection.DownRight;
    }

    private static StickDirection VerticalScrollDirection(double y) {
        const double verticalNeutral = 0.05;
        if (y < -verticalNeutral) return StickDirection.Up;
        if (y > verticalNeutral) return StickDirection.Down;
        return StickDirection.None;
    }


    private void DebugSources(string message) {
        if (!_debugSources) return;
        Console.WriteLine(message);
    }


    private static double Clamp(double value, double min, double max) { return value < min ? min : (value > max ? max : value); }

    private enum TouchGestureDirection {
        None,
        Up,
        Down,
        Left,
        Right
    }

    private enum TouchGestureSide {
        None,
        Left,
        Right
    }

    internal enum ClutchConsumer {
        ActionPosition,
        MouseButton,
        TouchpadClick
    }

    private enum TouchGestureShortcut {
        None,
        PreviousWindow,
        NextWindow,
        PreviousAltTabWindow,
        NextAltTabWindow,
        PreviousDesktop,
        NextDesktop,
        SnapWindowLeft,
        SnapWindowRight,
        Screenshot,
        RestoreMinimizedWindows,
        CloseWindow,
        MinimizeAllWindows,
        PreviousTab,
        NextTab,
        BackNavigation,
        ForwardNavigation
    }

    private enum TouchGestureRepeatMode {
        None,
        Distance,
        Timed
    }

    private enum TouchpadClickKind {
        None,
        Key
    }

    private struct TouchpadClickResolution {
        public readonly TouchpadClickKind Kind;
        public readonly PhysicalKey Key;

        private TouchpadClickResolution(TouchpadClickKind kind, PhysicalKey key) {
            Kind = kind;
            Key = key;
        }

        public static TouchpadClickResolution None() {
            return new TouchpadClickResolution(TouchpadClickKind.None, PhysicalKey.None);
        }

        public static TouchpadClickResolution ForKey(PhysicalKey key) {
            return new TouchpadClickResolution(TouchpadClickKind.Key, key);
        }

    }

    private struct TouchGestureRecognition {
        public TouchGestureDirection Direction;
        public TouchGestureSide Side;
        public int Finger;
        public bool TwoFingerContinuation;
        public int StaticFinger;
        public int StaticFingerId;
        public double StaticStartX;
        public double StaticStartY;
        public double StartX;
        public double StartY;
        public int CurrentX;
        public int CurrentY;
        public double PrimaryDistance;
    }

    private struct TouchGestureState {
        public bool Active;
        public int FingerCount;
        public bool HadTwoFingers;
        public bool Touch1Tracking;
        public bool Touch2Tracking;
        public int Touch1Id;
        public int Touch2Id;
        public double Touch1StartX;
        public double Touch1StartY;
        public double Touch2StartX;
        public double Touch2StartY;
        public bool Moving;
        public bool Completed;
        public TouchGestureShortcut Shortcut;
        public TouchGestureRepeatMode RepeatMode;
        public bool TwoFingerContinuation;
        public int StaticFinger;
        public int StaticFingerId;
        public double StaticStartX;
        public double StaticStartY;
        public TouchGestureDirection Direction;
        public TouchGestureSide Side;
        public int ActiveFinger;
        public int ActiveFingerId;
        public bool TwoFingerContinuationAwaitingMoverRelease;
        public double RepeatAnchorX;
        public double RepeatAnchorY;
        public double NextTimedRepeatMs;

        public void Reset() {
            this = new TouchGestureState();
        }
    }

    private struct ResolvedActionStroke {
        public readonly KeyStroke Stroke;
        public readonly bool FnTranslated;

        public ResolvedActionStroke(KeyStroke stroke, bool fnTranslated) {
            Stroke = stroke;
            FnTranslated = fnTranslated;
        }
    }

    private struct ButtonHold {
        public KeyStroke Key;
        public Layer KeyLayer;
        public bool KeyIsDown;
        public bool SuppressUntilRelease;
        public bool Pending;
        public bool PendingReleased;
        public double PendingSinceMs;
        public Layer OriginalPendingLayer;
        public Layer PendingLayer;
        public double PendingLayerMs;
        public bool PendingLayerSettled;
        public double PendingLayerOccupancyMs;
        public int PendingModifierMask;
        public List<PendingLayerOccupancySegment> PendingLayerOccupancySegments;
        public Layer PendingTraceLayer;
        public double PendingTraceLayerMs;
        public double PendingTraceStartMs;
        public double KeyDownMs;
        public int StickyModifierMask;
        public bool RepeatEnabled;
        public bool BoundRepeatPulse;
        public double RepeatStartedMs;
        public double NextRepeatMs;
    }

    private struct MouseButtonHold {
        public bool PreviousPhysicalDown;
        public bool Pending;
        public bool PendingReleased;
        public double PendingSinceMs;
        public int PendingModifierMask;
        public bool OutputDown;
        public int StickyModifierMask;
    }

    private struct PendingLayerOccupancySegment {
        public readonly double DurationMs;
        public readonly bool IsLayerBody;
        public readonly Layer Layer;
        public readonly double LayerDownMs;

        public PendingLayerOccupancySegment(double durationMs, bool isLayerBody, Layer layer, double layerDownMs) {
            DurationMs = durationMs;
            IsLayerBody = isLayerBody;
            Layer = layer;
            LayerDownMs = layerDownMs;
        }
    }

    private struct PendingLayerOccupancyTrace {
        public readonly double DurationMs;
        public readonly bool ReachesPendingStart;

        public PendingLayerOccupancyTrace(double durationMs, bool reachesPendingStart) {
            DurationMs = durationMs;
            ReachesPendingStart = reachesPendingStart;
        }
    }
}
