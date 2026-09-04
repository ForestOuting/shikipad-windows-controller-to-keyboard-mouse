using System.Reflection;
using System.Runtime.CompilerServices;

static class Program {
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    public static void Main() {
        Assembly assembly = Assembly.Load("ShikiPad");
        Type mapper = RequiredType(assembly, "MapperForm");
        Type sideType = RequiredType(assembly, "MapperForm+TouchGestureSide");
        Type directionType = RequiredType(assembly, "MapperForm+TouchGestureDirection");

        VerifyMappings(mapper, sideType, directionType);
        VerifyStartZoneResolution(assembly, mapper, sideType, directionType);
        VerifyRepeatStepSettlement(assembly, mapper, directionType);
        VerifyTwoFingerContinuationKeepsStaticFinger(assembly, mapper);
        VerifyTouchpadClicks(assembly, mapper);
        VerifyHidReportParsing(assembly);
        VerifyModifierActionWindow(assembly, mapper);
        VerifyCapsFnTranslation(assembly, mapper);
        VerifyLeftStickSectors(mapper);
        VerifyIndependentOutputFlow(assembly, mapper);
        VerifyContinuousOutputsAdvanceTogether(assembly);
        VerifyConfigurationGuards(assembly);
        VerifyPureBaseAndLayerSelection(assembly, mapper);
        NotNull(mapper.GetMethod("UpdateTwoFingerContinuationRepeat", BindingFlags.NonPublic | BindingFlags.Instance), "two-finger continuation update remains available");
        NotNull(mapper.GetMethod("TryRecognizeTwoFingerContinuationGesture", PrivateStatic), "two-finger continuation recognition remains available");
        Equal(null, assembly.GetType("MapperForm+TouchGesturePressType"), "hold gesture enum removed");
        Equal(null, RequiredType(assembly, "Config").GetField("TouchGestureHoldMs"), "hold gesture timer removed");
        Console.WriteLine("ShikiPad logic checks passed.");
    }

    private static void VerifyPureBaseAndLayerSelection(Assembly assembly, Type mapper) {
        Type mappingType = RequiredType(assembly, "MappingEngine");
        Type layerType = RequiredType(assembly, "Layer");
        Type actionType = RequiredType(assembly, "ActionButton");
        object mapping = Activator.CreateInstance(mappingType);
        MethodInfo resolve = mappingType.GetMethod("Resolve");
        object Base = Enum.Parse(layerType, "Base");
        object L1 = Enum.Parse(layerType, "L1");
        object R1 = Enum.Parse(layerType, "R1");
        object R1L1 = Enum.Parse(layerType, "R1L1");
        Equal(Base, resolve.Invoke(mapping, new object[] { false, false, false, false, 0.0, 0.0, 0.0, 0.0, 35.0 }), "no shoulder or trigger resolves to Base");
        Equal(L1, resolve.Invoke(mapping, new object[] { true, false, false, false, 100.0, 0.0, 0.0, 0.0, 35.0 }), "one held shoulder resolves its own layer");
        Equal(R1L1, resolve.Invoke(mapping, new object[] { true, true, false, false, 100.0, 130.0, 0.0, 0.0, 35.0 }), "two compatible shoulders inside 35 ms resolve a combo");
        Equal(R1, resolve.Invoke(mapping, new object[] { true, true, false, false, 100.0, 136.0, 0.0, 0.0, 35.0 }), "combo outside 35 ms resolves the latest single layer");

        string[] expected = { "ArrowUp", "ArrowRight", "Tab", "Escape", "ArrowLeft", "ArrowDown", "Space", "Enter" };
        MethodInfo lookup = mappingType.GetMethod("Lookup");
        for (int i = 0; i < expected.Length; i++) {
            object stroke = lookup.Invoke(mapping, new[] { Base, Enum.ToObject(actionType, i) });
            Equal(expected[i], stroke.GetType().GetField("Key").GetValue(stroke).ToString(), "Base contains only the eight documented action mappings " + i);
        }

        MethodInfo initialLayer = RequiredMethod(mapper, "ResolveInitialActionLayer");
        Equal(R1, initialLayer.Invoke(null, new[] { Base, (object)0.0, R1, (object)80.0, (object)115.0, (object)100.0, (object)15.0 }), "15 ms released-layer post-grace boundary is included");
        Equal(Base, initialLayer.Invoke(null, new[] { Base, (object)0.0, R1, (object)80.0, (object)115.01, (object)100.0, (object)15.0 }), "released layer no longer owns an action after 15 ms");
    }

    private static void VerifyModifierActionWindow(Assembly assembly, Type mapper) {
        Type configType = RequiredType(assembly, "Config");
        Type keyType = RequiredType(assembly, "PhysicalKey");
        Type layerType = RequiredType(assembly, "Layer");
        object config = Activator.CreateInstance(configType);
        Equal(0.30, configType.GetField("LeftStickEnterDeadzone").GetValue(config), "left-stick scroll deadzone default");
        Equal(0.50, configType.GetField("LeftStickModifierEnterDeadzone").GetValue(config), "left-stick modifier sector deadzone default");
        Equal(45, configType.GetField("ActionLayerGraceMs").GetValue(config), "pure layer grace window default");
        Equal(15, configType.GetField("ActionLayerPostGraceMs").GetValue(config), "pure layer post-grace default");
        Equal(35, configType.GetField("ComboLayerWindowMs").GetValue(config), "combo-layer window default");
        Equal(20, configType.GetField("LayerOccupancyCarryCutoffMs").GetValue(config), "layer body carry cutoff default");
        Equal(30, configType.GetField("LayerTakeoverWindowMs").GetValue(config), "layer body takeover cap default");
        Equal(45, configType.GetField("ModifierBindingWindowMs").GetValue(config), "independent modifier binding window default");
        Equal(null, configType.GetField("ActionDecisionWindowMs"), "coupled action decision window removed");
        NotNull(mapper.GetMethod("ShouldWaitForPendingSingleLayerToSettle", BindingFlags.NonPublic | BindingFlags.Instance), "pure layer settle wait restored");

        MethodInfo withinWindow = RequiredMethod(mapper, "IsWithinModifierBindingWindow");
        Equal(true, withinWindow.Invoke(null, new[] { (object)100.0, 145.0, configWithWindow(configType, 45) }), "45 ms boundary is included");
        Equal(false, withinWindow.Invoke(null, new[] { (object)100.0, 145.01, configWithWindow(configType, 45) }), "later modifier is excluded");
        Equal(false, withinWindow.Invoke(null, new[] { (object)100.0, 99.0, configWithWindow(configType, 45) }), "modifier before action is not a late-window arrival");

        MethodInfo decisionDelay = RequiredMethod(mapper, "PendingActionDecisionDelayMs");
        object independentWindowConfig = Activator.CreateInstance(configType);
        configType.GetField("ActionLayerGraceMs").SetValue(independentWindowConfig, 0);
        configType.GetField("ModifierBindingWindowMs").SetValue(independentWindowConfig, 45);
        Equal(45.0, decisionDelay.Invoke(null, new[] { independentWindowConfig }), "modifier binding remains a full 45 ms when pure layer grace is zero");
        configType.GetField("ActionLayerGraceMs").SetValue(independentWindowConfig, 60);
        Equal(60.0, decisionDelay.Invoke(null, new[] { independentWindowConfig }), "longer pure layer grace remains fully observed");

        MethodInfo retainDeferredTap = RequiredMethod(mapper, "ShouldQueueDeferredModifierTap");
        Equal(true, retainDeferredTap.Invoke(null, new object[] { true, false, false }), "a modifier released before physical output is retained as a deferred tap");
        Equal(false, retainDeferredTap.Invoke(null, new object[] { true, true, false }), "an already-output modifier uses its ordinary KeyUp path");
        Equal(false, retainDeferredTap.Invoke(null, new object[] { true, false, true }), "a still-held deferred modifier remains a pending hold");

        MethodInfo isModifier = RequiredMethod(mapper, "IsModifierBindingKey");
        string[] included = { "LShift", "LCtrl", "LWin", "LAlt", "RAlt", "RCtrl" };
        foreach (string name in included) {
            Equal(true, isModifier.Invoke(null, new[] { Enum.Parse(keyType, name) }), name + " belongs to modifier class");
        }
        string[] excluded = { "Home", "CapsLock", "Delete", "Backspace", "A", "Num1", "ArrowUp" };
        foreach (string name in excluded) {
            Equal(false, isModifier.Invoke(null, new[] { Enum.Parse(keyType, name) }), name + " stays outside modifier class");
        }

        Equal(null, mapper.GetMethod("IsModifierWindowEligibleAction", PrivateStatic), "old modifier eligibility classification removed");
        Equal(null, mapper.GetMethod("ResolvePendingActionLayer", PrivateStatic), "modifier cannot override resolved layer");
        MethodInfo resolveLayer = RequiredMethod(mapper, "ResolvePendingLayer");
        Equal("R1", resolveLayer.Invoke(null, new[] { Enum.Parse(layerType, "Base"), Enum.Parse(layerType, "Base"), (object)100.0, Enum.Parse(layerType, "R1"), (object)120.0, true, (object)45.0 }).ToString(), "layer pressed inside 45 ms takes over without consulting modifiers");
        Equal("Base", resolveLayer.Invoke(null, new[] { Enum.Parse(layerType, "Base"), Enum.Parse(layerType, "Base"), (object)100.0, Enum.Parse(layerType, "R1"), (object)146.0, true, (object)45.0 }).ToString(), "layer pressed after 45 ms cannot take over");

        MethodInfo touchRepeat = RequiredMethod(mapper, "IsTouchpadClickRepeatKey");
        Equal(true, touchRepeat.Invoke(null, new[] { Enum.Parse(keyType, "Delete") }), "Delete is immediate repeat input");
        Equal(true, touchRepeat.Invoke(null, new[] { Enum.Parse(keyType, "Backspace") }), "Backspace is immediate repeat input");
        Equal(false, touchRepeat.Invoke(null, new[] { Enum.Parse(keyType, "CapsLock") }), "CapsLock remains a non-repeat action");
        Equal(null, mapper.GetMethod("ArmTouchpadClickPending", BindingFlags.NonPublic | BindingFlags.Instance), "touchpad clicks do not enter modifier binding");
        Equal(null, mapper.GetField("_touchClickPendingModifierMask", BindingFlags.NonPublic | BindingFlags.Instance), "touchpad has no modifier pending state");
        Equal(null, mapper.GetMethod("RegisterHomeModifier", BindingFlags.NonPublic | BindingFlags.Instance), "Home does not enter modifier binding");
        Equal(null, mapper.GetField("_homeModifierBindingWindowActive", BindingFlags.NonPublic | BindingFlags.Instance), "Home has no modifier pending state");

        MethodInfo pulseBoundRepeat = RequiredMethod(mapper, "ShouldPulseBoundRepeat");
        Equal(true, pulseBoundRepeat.Invoke(null, new object[] { true, 1 }), "a modifier-bound repeat action emits complete chord pulses");
        Equal(false, pulseBoundRepeat.Invoke(null, new object[] { true, 0 }), "an unbound repeat action retains normal held repeat");
        Equal(false, pulseBoundRepeat.Invoke(null, new object[] { false, 1 }), "a non-repeat action retains normal sticky hold semantics");
        Type holdType = RequiredType(mapper.Assembly, "MapperForm+ButtonHold");
        NotNull(holdType.GetField("BoundRepeatPulse"), "action hold records bound repeat pulse mode");

        Type clutchConsumerType = RequiredType(mapper.Assembly, "MapperForm+ClutchConsumer");
        MethodInfo canConsumeClutch = RequiredMethod(mapper, "CanConsumeClutchToggle");
        Equal(true, canConsumeClutch.Invoke(null, new[] { Enum.Parse(clutchConsumerType, "ActionPosition") }), "all eight mapped action positions can consume a primed Home clutch");
        Equal(true, canConsumeClutch.Invoke(null, new[] { Enum.Parse(clutchConsumerType, "MouseButton") }), "L3/R3 mouse buttons can consume a primed Home clutch");
        Equal(false, canConsumeClutch.Invoke(null, new[] { Enum.Parse(clutchConsumerType, "TouchpadClick") }), "touchpad Delete, Backspace, and Caps Lock cannot consume a primed Home clutch");

        MethodInfo shouldDeactivateCapsFn = RequiredMethod(mapper, "ShouldDeactivateCapsFnAfterAction");
        Equal(true, shouldDeactivateCapsFn.Invoke(null, new object[] { true }), "a translated letter or F1-F12 action exits Caps/Fn mode");
        Equal(false, shouldDeactivateCapsFn.Invoke(null, new object[] { false }), "an untranslated action preserves Caps/Fn mode");

        VerifyPendingModifierRegistration(mapper, configType, keyType);
        NotNull(mapper.GetField("_pendingLeftStickModifierTaps", BindingFlags.NonPublic | BindingFlags.Instance), "left-stick deferred modifier taps have persistent state");
        NotNull(mapper.GetField("_createPendingTap", BindingFlags.NonPublic | BindingFlags.Instance), "Create deferred tap has persistent state");
        NotNull(mapper.GetField("_optionsPendingTap", BindingFlags.NonPublic | BindingFlags.Instance), "Options deferred tap has persistent state");
    }

    private static void VerifyPendingModifierRegistration(Type mapper, Type configType, Type keyType) {
        Type holdType = RequiredType(mapper.Assembly, "MapperForm+ButtonHold");
        object instance = RuntimeHelpers.GetUninitializedObject(mapper);
        object config = configWithWindow(configType, 45);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, config);
        object desiredModifiers = Activator.CreateInstance(typeof(List<>).MakeGenericType(keyType));
        mapper.GetField("_desiredLeftStickKeys", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, desiredModifiers);

        MethodInfo capture = mapper.GetMethod("CaptureCurrentBindingModifiers", BindingFlags.NonPublic | BindingFlags.Instance);
        ((System.Collections.IList)desiredModifiers).Add(Enum.Parse(keyType, "LShift"));
        Equal(1 << 0, capture.Invoke(instance, null), "logical left-stick modifier is capturable before its deferred physical KeyDown");
        mapper.GetField("_prevCreate", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, true);
        Equal((1 << 0) | (1 << 4), capture.Invoke(instance, null), "logical Create state is capturable before its deferred physical KeyDown");
        mapper.GetField("_prevOptions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, true);
        Equal((1 << 0) | (1 << 4) | (1 << 5), capture.Invoke(instance, null), "logical Options state is capturable before its deferred physical KeyDown");
        ((System.Collections.IList)desiredModifiers).Clear();
        mapper.GetField("_prevCreate", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, false);
        mapper.GetField("_prevOptions", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, false);

        Type layerType = RequiredType(mapper.Assembly, "Layer");
        MethodInfo shouldDefer = mapper.GetMethod("ShouldDeferInitialAction", BindingFlags.NonPublic | BindingFlags.Instance);
        Equal(true, shouldDefer.Invoke(instance, new[] { Enum.Parse(layerType, "Base") }), "base actions use the pure layer window");
        Equal(true, shouldDefer.Invoke(instance, new[] { Enum.Parse(layerType, "R1") }), "character actions use the same pure layer window");

        Array holds = Array.CreateInstance(holdType, 8);
        object hold = Activator.CreateInstance(holdType);
        SetField(holdType, hold, "Pending", true);
        SetField(holdType, hold, "PendingSinceMs", 100.0);
        holds.SetValue(hold, 0);
        mapper.GetField("_holds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, holds);

        Type mouseHoldType = RequiredType(mapper.Assembly, "MapperForm+MouseButtonHold");
        object mouseHold = Activator.CreateInstance(mouseHoldType);
        SetField(mouseHoldType, mouseHold, "Pending", true);
        SetField(mouseHoldType, mouseHold, "PendingSinceMs", 100.0);
        mapper.GetField("_leftMouseButton", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, mouseHold);
        MethodInfo register = mapper.GetMethod("RegisterModifierBinding", BindingFlags.NonPublic | BindingFlags.Instance);
        register.Invoke(instance, new[] { Enum.Parse(keyType, "RAlt"), (object)145.0 });
        hold = holds.GetValue(0);
        Equal(1 << 4, holdType.GetField("PendingModifierMask").GetValue(hold), "late Create modifier is captured by action pending at 45 ms");
        mouseHold = mapper.GetField("_leftMouseButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(1 << 4, mouseHoldType.GetField("PendingModifierMask").GetValue(mouseHold), "late Create modifier is captured by mouse-button pending");
        register.Invoke(instance, new[] { Enum.Parse(keyType, "RCtrl"), (object)145.01 });
        hold = holds.GetValue(0);
        Equal(1 << 4, holdType.GetField("PendingModifierMask").GetValue(hold), "modifier after 45 ms is not captured by action pending");
        mouseHold = mapper.GetField("_leftMouseButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(1 << 4, mouseHoldType.GetField("PendingModifierMask").GetValue(mouseHold), "modifier after 45 ms is not captured by mouse-button pending");
    }

    private static void VerifyCapsFnTranslation(Assembly assembly, Type mapper) {
        Type keyType = RequiredType(assembly, "PhysicalKey");
        Type strokeType = RequiredType(assembly, "KeyStroke");
        object instance = RuntimeHelpers.GetUninitializedObject(mapper);
        mapper.GetField("_capsFnLayerActive", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, true);
        MethodInfo resolve = mapper.GetMethod("ResolveActionStroke", BindingFlags.NonPublic | BindingFlags.Instance);

        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "A", false, "A", true, true);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Num1", false, "F1", false, true);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Num0", false, "F10", false, true);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Minus", false, "F11", false, true);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Equals", false, "F12", false, true);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Space", false, "Space", false, false);
        VerifyCapsFnStroke(resolve, instance, strokeType, keyType, "Num1", true, "Num1", true, false);
    }

    private static void VerifyLeftStickSectors(Type mapper) {
        MethodInfo sector = mapper.GetMethod("Sector", BindingFlags.Public | BindingFlags.Static);
        NotNull(sector, "left-stick sector resolver");

        Equal("Up", SectorAtDegrees(sector, 90.0), "wheel-up sector remains centered on straight up");
        Equal("Up", SectorAtDegrees(sector, 66.0), "wheel-up sector includes its clockwise interior edge");
        Equal("Up", SectorAtDegrees(sector, 114.0), "wheel-up sector includes its counterclockwise interior edge");
        Equal("UpRight", SectorAtDegrees(sector, 64.0), "upper-right modifier receives the area below the wheel-up boundary");
        Equal("UpLeft", SectorAtDegrees(sector, 116.0), "upper-left modifier receives the area above the wheel-up boundary");

        Equal("Down", SectorAtDegrees(sector, 270.0), "wheel-down sector remains centered on straight down");
        Equal("Down", SectorAtDegrees(sector, 246.0), "wheel-down sector includes its clockwise interior edge");
        Equal("Down", SectorAtDegrees(sector, 294.0), "wheel-down sector includes its counterclockwise interior edge");
        Equal("DownLeft", SectorAtDegrees(sector, 244.0), "lower-left modifier receives the area before the wheel-down boundary");
        Equal("DownRight", SectorAtDegrees(sector, 296.0), "lower-right modifier receives the area after the wheel-down boundary");
    }

    private static string SectorAtDegrees(MethodInfo sector, double degrees) {
        double radians = degrees * Math.PI / 180.0;
        return sector.Invoke(null, new object[] { Math.Cos(radians), -Math.Sin(radians) }).ToString();
    }

    private static void VerifyIndependentOutputFlow(Assembly assembly, Type mapper) {
        Equal(true, mapper.GetMethod("FlushPendingModifierKeys", BindingFlags.NonPublic | BindingFlags.Instance) != null, "logical modifier registration is separated from physical modifier output");
        Equal(null, mapper.GetField("_tickOutputOwner", BindingFlags.NonPublic | BindingFlags.Instance), "no module can exclusively own and suppress an entire polling frame");
        Equal(null, mapper.GetMethod("CanUseTickOutputLane", BindingFlags.NonPublic | BindingFlags.Static), "the obsolete global output-lane gate is removed");
        Equal(null, mapper.GetNestedType("OutputModule", BindingFlags.NonPublic), "no residual global priority or stage type can gate module output");
        Equal(null, mapper.GetMethod("RequireOutputStage", BindingFlags.NonPublic | BindingFlags.Instance), "no runtime stage guard can turn harmless overlap into a mapper exception");
        Equal(null, mapper.GetField("_tickLock", BindingFlags.NonPublic | BindingFlags.Instance), "the single fixed scheduler no longer contends on a mapper tick lock");
        Equal(null, mapper.GetMethod("OnStateUpdated", BindingFlags.NonPublic | BindingFlags.Instance), "HID report callbacks no longer execute a second mapper tick");
        Equal(null, RequiredType(assembly, "DirectHidController").GetEvent("StateUpdated"), "the HID reader only publishes the latest state reference");

        Type injector = RequiredType(assembly, "InputInjector");
        Equal(null, injector.GetMethod("BeginFrame"), "input injection has no deferred logical-frame queue");
        Equal(null, injector.GetMethod("FlushFrame"), "input injection cannot postpone native delivery until frame end");
        Equal(null, injector.GetMethod("AbortFrame"), "failure recovery cannot blindly release a partially sent frame");
        Equal(null, injector.GetMethod("MouseMoveAndWheel"), "pointer and wheel use independent verified driver reports");
        Equal(null, injector.GetField("_frameStrokes", BindingFlags.NonPublic | BindingFlags.Instance), "held-state bookkeeping cannot diverge from a deferred native queue");
        MethodInfo restoreTap = RequiredMethod(injector, "ShouldRestoreHeldKeyForTap");
        Type keyType = RequiredType(assembly, "PhysicalKey");
        Equal(true, restoreTap.Invoke(null, new[] { (object)true, Enum.Parse(keyType, "M") }), "a tap restores an already-held ordinary key after its pulse");
        Equal(false, restoreTap.Invoke(null, new[] { (object)false, Enum.Parse(keyType, "M") }), "an unheld ordinary key needs no restoration");
        Equal(false, restoreTap.Invoke(null, new[] { (object)true, Enum.Parse(keyType, "LShift") }), "reference-counted modifiers keep their existing ownership path");

        MethodInfo suspendModifier = RequiredMethod(injector, "ShouldSuspendModifierForExactTap");
        Equal(true, suspendModifier.Invoke(null, new[] { Enum.Parse(keyType, "LShift"), (object)false, false, false, false }), "an unrelated held Shift is suspended around an exact shortcut");
        Equal(false, suspendModifier.Invoke(null, new[] { Enum.Parse(keyType, "RShift"), (object)true, false, false, false }), "a requested Shift group stays held for an exact shortcut");
        Equal(false, suspendModifier.Invoke(null, new[] { Enum.Parse(keyType, "RAlt"), (object)false, false, true, false }), "either Alt side can satisfy an exact Alt shortcut");
        Equal(true, suspendModifier.Invoke(null, new[] { Enum.Parse(keyType, "LWin"), (object)false, false, false, false }), "an unrelated held Win key is suspended around an exact shortcut");

        Type interception = RequiredType(assembly, "InterceptionDriver");
        Equal(typeof(bool), interception.GetMethod("SendKey").ReturnType, "keyboard injection reports native send success");
        Equal(typeof(bool), interception.GetMethod("SendMouse").ReturnType, "mouse injection reports native send success");
        Equal(typeof(bool), interception.GetMethod("SendMouseDelta").ReturnType, "pointer injection reports native send success");
        Equal(typeof(bool), interception.GetMethod("SendMouseWheel").ReturnType, "wheel injection reports native send success");
        Equal(null, interception.GetMethod("SendMouseDeltaAndWheel"), "the driver path does not depend on an unverified combined mouse stroke");
        Equal(null, interception.GetMethod("SendStrokes"), "the driver path does not expose partial multi-stroke sends");
    }

    private static void VerifyContinuousOutputsAdvanceTogether(Assembly assembly) {
        Type configType = RequiredType(assembly, "Config");
        object config = Activator.CreateInstance(configType);

        Type scrollType = RequiredType(assembly, "LeftStickScrollIntegrator");
        object scroll = Activator.CreateInstance(scrollType);
        MethodInfo updateScroll = scrollType.GetMethod("TryUpdate");
        MethodInfo scrollInterval = RequiredMethod(scrollType, "ScrollIntervalMs");
        Equal(15.0, scrollInterval.Invoke(null, new object[] { 1.0, config }), "maximum wheel speed keeps the documented 15 ms interval");

        Type pointerType = RequiredType(assembly, "RightStickMouseIntegrator");
        object pointer = Activator.CreateInstance(pointerType);
        MethodInfo updatePointer = pointerType.GetMethod("TryUpdate");

        int wheelReports = 0;
        int wheelDeltaTotal = 0;
        int pointerReports = 0;
        int sameCycleOutputTicks = 0;
        for (int tick = 0; tick < 300; tick++) {
            object[] scrollArgs = { 1.0, 0.001, config, 1, 0 };
            object[] pointerArgs = { 1.0, 0.0, 0.001, config, 0, 0 };
            bool wheelOutput = (bool)updateScroll.Invoke(scroll, scrollArgs);
            bool pointerOutput = (bool)updatePointer.Invoke(pointer, pointerArgs);
            if (wheelOutput) {
                wheelReports++;
                int delta = (int)scrollArgs[4];
                Equal(true, delta > 0 && delta <= 120, "wheel emits a bounded positive high-resolution delta");
                wheelDeltaTotal += delta;
            }
            if (pointerOutput) pointerReports++;
            if (wheelOutput && pointerOutput) sameCycleOutputTicks++;
        }

        Equal(75, wheelReports, "wheel reporting is capped at one output every 4 ms instead of every 1 ms");
        Equal(2376, wheelDeltaTotal, "paced wheel output preserves accumulated distance apart from the pending sub-report remainder");
        Equal(300, pointerReports, "pointer movement remains available on every fixed scheduler tick during sustained simultaneous input");
        Equal(75, sameCycleOutputTicks, "paced wheel reports and pointer movement remain available in the same scheduler cycle");

        object[] neutralScrollArgs = { 0.0, 0.001, config, 1, 0 };
        object[] neutralPointerArgs = { 0.0, 0.0, 0.001, config, 0, 0 };
        Equal(false, updateScroll.Invoke(scroll, neutralScrollArgs), "releasing the left stick clears pending wheel output immediately");
        Equal(false, updatePointer.Invoke(pointer, neutralPointerArgs), "releasing the right stick clears pending pointer output immediately");

        object[] resumedWheelStep = { 1.0, 0.001, config, 1, 0 };
        Equal(true, updateScroll.Invoke(scroll, resumedWheelStep), "wheel produces a fine delta on the first tick after release");
        Equal(8, resumedWheelStep[4], "the first maximum-speed wheel delta is fine-grained rather than a full 120-unit jump");

        object[] reverseStart = { 1.0, 0.001, config, -1, 0 };
        Equal(true, updateScroll.Invoke(scroll, reverseStart), "reversing wheel direction starts a fresh fine-delta report immediately");
        Equal(-8, reverseStart[4], "reverse wheel clears the previous accumulator and emits the correct signed delta");

        scroll = Activator.CreateInstance(scrollType);
        for (int tick = 0; tick < 6; tick++) {
            object[] lowSpeedPending = { 0.31, 0.001, config, 1, 0 };
            Equal(false, updateScroll.Invoke(scroll, lowSpeedPending), "low-speed wheel accumulates only until the first fine unit is available");
        }
        object[] lowSpeedStep = { 0.31, 0.001, config, 1, 0 };
        Equal(true, updateScroll.Invoke(scroll, lowSpeedStep), "low-speed wheel begins without waiting for a full notch");
        Equal(1, lowSpeedStep[4], "low-speed wheel starts with one high-resolution unit");

        double deadzone = (double)configType.GetField("RightStickDeadzone").GetValue(config);
        double lowRadius = deadzone + (1.0 - deadzone) * 0.05;
        int firstLowSpeedPointerTick = 0;
        for (int tick = 1; tick <= 100; tick++) {
            object[] lowPointerArgs = { lowRadius, 0.0, 0.001, config, 0, 0 };
            if ((bool)updatePointer.Invoke(pointer, lowPointerArgs)) {
                firstLowSpeedPointerTick = tick;
                Equal(1, lowPointerArgs[4], "low-speed pointer starts with one pixel rather than a large jump");
                break;
            }
        }
        Equal(true, firstLowSpeedPointerTick > 0 && firstLowSpeedPointerTick <= 100, "5% normalized pointer movement responds within 100 ms");
    }

    private static void VerifyConfigurationGuards(Assembly assembly) {
        Type configType = RequiredType(assembly, "Config");
        MethodInfo validate = configType.GetMethod("Validate");
        object config = Activator.CreateInstance(configType);
        validate.Invoke(config, null);

        configType.GetField("RightStickDeadzone").SetValue(config, 1.0);
        ExpectInvocationFailure(() => validate.Invoke(config, null), "right-stick deadzone cannot make normalization divide by zero");

        config = Activator.CreateInstance(configType);
        configType.GetField("RightStickCurve").SetValue(config, "unknown");
        ExpectInvocationFailure(() => validate.Invoke(config, null), "unknown right-stick response curves are rejected");

        config = Activator.CreateInstance(configType);
        configType.GetField("RightStickLowSpeedAssist").SetValue(config, 1.1);
        ExpectInvocationFailure(() => validate.Invoke(config, null), "right-stick low-speed assist remains a bounded curve adjustment");

        config = Activator.CreateInstance(configType);
        configType.GetField("ScrollReportIntervalMs").SetValue(config, 0);
        ExpectInvocationFailure(() => validate.Invoke(config, null), "wheel report pacing must stay positive");

        Type integrator = RequiredType(assembly, "RightStickMouseIntegrator");
        MethodInfo curve = RequiredMethod(integrator, "ApplyResponseCurve");
        Equal(0.0625, curve.Invoke(null, new object[] { 0.25, "power", 2.0, 0.0 }), "power response curve uses the configured exponent");
        Equal(0.06953125, curve.Invoke(null, new object[] { 0.25, "power", 2.0, 0.05 }), "low-speed assist adds a center-weighted power-curve component");
        Equal(1.0, curve.Invoke(null, new object[] { 1.0, "power", 3.0, 0.05 }), "low-speed assist does not change full-stick speed");
        Equal(0.25, curve.Invoke(null, new object[] { 0.25, "linear", 2.0, 0.05 }), "linear response curve remains unchanged by power-curve assistance");
    }

    private static void VerifyCapsFnStroke(MethodInfo resolve, object instance, Type strokeType, Type keyType, string inputKey, bool inputShift, string expectedKey, bool expectedShift, bool expectedTranslated) {
        object stroke = Activator.CreateInstance(strokeType, new[] { Enum.Parse(keyType, inputKey), (object)inputShift });
        object result = resolve.Invoke(instance, new[] { stroke });
        object resolvedStroke = result.GetType().GetField("Stroke").GetValue(result);
        Equal(expectedKey, strokeType.GetField("Key").GetValue(resolvedStroke).ToString(), "Caps/Fn resolved key for " + inputKey);
        Equal(expectedShift, strokeType.GetField("Shift").GetValue(resolvedStroke), "Caps/Fn resolved shift for " + inputKey);
        Equal(expectedTranslated, result.GetType().GetField("FnTranslated").GetValue(result), "Caps/Fn translation flag for " + inputKey);
    }

    private static object configWithWindow(Type configType, int windowMs) {
        object config = Activator.CreateInstance(configType);
        configType.GetField("ModifierBindingWindowMs").SetValue(config, windowMs);
        return config;
    }

    private static void VerifyMappings(Type mapper, Type sideType, Type directionType) {
        MethodInfo resolve = RequiredMethod(mapper, "TryResolveTouchGestureShortcut");
        MethodInfo repeatFor = RequiredMethod(mapper, "TouchGestureRepeatModeFor");
        (int Fingers, string Side, string Direction, string Shortcut, string Repeat)[] cases = {
            (1, "Right", "Left", "PreviousDesktop", "Timed"),
            (1, "Right", "Right", "NextDesktop", "Timed"),
            (1, "Right", "Up", "MaximizeWindow", "None"),
            (1, "Right", "Down", "RestoreOrMinimizeWindow", "None"),
            (1, "Left", "Right", "NextAltTabWindow", "Distance"),
            (1, "Left", "Left", "PreviousAltTabWindow", "Distance"),
            (1, "Left", "Up", "PreviousWindow", "Timed"),
            (1, "Left", "Down", "NextWindow", "Timed"),
            (2, "Left", "Up", "PreviousTab", "Timed"),
            (2, "Left", "Down", "NextTab", "Timed"),
            (2, "Left", "Left", "BackNavigation", "Timed"),
            (2, "Left", "Right", "ForwardNavigation", "Timed"),
            (2, "Right", "Right", "CloseWindow", "None"),
            (2, "Right", "Left", "Screenshot", "None"),
            (2, "Right", "Up", "RestoreMinimizedWindows", "None"),
            (2, "Right", "Down", "MinimizeAllWindows", "None")
        };

        foreach (var test in cases) {
            object[] args = {
                test.Fingers,
                Enum.Parse(sideType, test.Side),
                Enum.Parse(directionType, test.Direction),
                null
            };
            bool mapped = (bool)resolve.Invoke(null, args);
            Equal(true, mapped, $"map {test.Fingers}/{test.Side}/{test.Direction}");
            Equal(test.Shortcut, args[3].ToString(), $"shortcut {test.Fingers}/{test.Side}/{test.Direction}");
            Equal(test.Repeat, repeatFor.Invoke(null, new[] { args[3] }).ToString(), $"repeat {test.Shortcut}");
        }
    }

    private static void VerifyStartZoneResolution(Assembly assembly, Type mapper, Type sideType, Type directionType) {
        Type configType = RequiredType(assembly, "Config");
        MethodInfo resolve = RequiredMethod(mapper, "ResolveTouchGestureSide");
        object config = Activator.CreateInstance(configType);

        Equal(-1, Array.IndexOf(Enum.GetNames(sideType), "Buffer"), "gesture buffer side removed");
        Equal("Left", ResolveZone(resolve, directionType, config, 100.0, "Right"), "left confirmed ignores direction");
        Equal("Left", ResolveZone(resolve, directionType, config, 700.0, "Left"), "left buffer moving left resolves left");
        Equal("Left", ResolveZone(resolve, directionType, config, 700.0, "Up"), "left buffer moving up resolves left");
        Equal("Left", ResolveZone(resolve, directionType, config, 700.0, "Down"), "left buffer moving down resolves left");
        Equal("Right", ResolveZone(resolve, directionType, config, 700.0, "Right"), "left buffer moving right resolves right");
        Equal("Right", ResolveZone(resolve, directionType, config, 1100.0, "Right"), "right buffer moving right resolves right");
        Equal("Right", ResolveZone(resolve, directionType, config, 1100.0, "Up"), "right buffer moving up resolves right");
        Equal("Right", ResolveZone(resolve, directionType, config, 1100.0, "Down"), "right buffer moving down resolves right");
        Equal("Left", ResolveZone(resolve, directionType, config, 1100.0, "Left"), "right buffer moving left resolves left");
        Equal("Right", ResolveZone(resolve, directionType, config, 1800.0, "Left"), "right confirmed ignores direction");
        Equal("Left", ResolveZone(resolve, directionType, config, 549.0, "Right"), "left confirmed edge");
        Equal("Right", ResolveZone(resolve, directionType, config, 550.0, "Right"), "left buffer edge");
        Equal("Left", ResolveZone(resolve, directionType, config, 960.0, "Left"), "right buffer center edge");
        Equal("Right", ResolveZone(resolve, directionType, config, 1370.0, "Left"), "right confirmed edge");
    }

    private static string ResolveZone(MethodInfo resolve, Type directionType, object config, double startX, string direction) {
        return resolve.Invoke(null, new[] { (object)startX, Enum.Parse(directionType, direction), config }).ToString();
    }

    private static void VerifyRepeatStepSettlement(Assembly assembly, Type mapper, Type directionType) {
        object config = Activator.CreateInstance(RequiredType(assembly, "Config"));
        MethodInfo resolve = RequiredMethod(mapper, "TryResolveTouchGestureMovement");
        MethodInfo sameAxis = RequiredMethod(mapper, "AreTouchGestureDirectionsOnSameAxis");

        var vertical = ResolveMovement(resolve, directionType, config, 0.0, -150.0);
        Equal(true, vertical.Ready, "vertical repeat step reaches 150");
        Equal("Up", vertical.Direction, "vertical repeat direction");
        Equal(150.0, vertical.RequiredDistance, "vertical repeat threshold");

        var horizontal = ResolveMovement(resolve, directionType, config, 180.0, 149.0);
        Equal(true, horizontal.Ready, "horizontal repeat step reaches 180");
        Equal("Right", horizontal.Direction, "horizontal repeat direction");
        Equal(180.0, horizontal.RequiredDistance, "horizontal repeat threshold");

        Equal(true, sameAxis.Invoke(null, new[] { Enum.Parse(directionType, "Up"), Enum.Parse(directionType, "Down") }), "vertical reverse shares axis");
        Equal(true, sameAxis.Invoke(null, new[] { Enum.Parse(directionType, "Left"), Enum.Parse(directionType, "Right") }), "horizontal reverse shares axis");
        Equal(false, sameAxis.Invoke(null, new[] { Enum.Parse(directionType, "Up"), Enum.Parse(directionType, "Left") }), "perpendicular step has no timed shortcut feedback");

        Type stateType = RequiredType(assembly, "MapperForm+TouchGestureState");
        object state = Activator.CreateInstance(stateType);
        SetField(stateType, state, "RepeatAnchorX", 10.0);
        SetField(stateType, state, "RepeatAnchorY", 20.0);
        object[] settleArgs = { state, 700, 800 };
        RequiredMethod(mapper, "SettleTouchGestureStep").Invoke(null, settleArgs);
        state = settleArgs[0];
        Equal(700.0, stateType.GetField("RepeatAnchorX").GetValue(state), "settlement clears horizontal accumulation");
        Equal(800.0, stateType.GetField("RepeatAnchorY").GetValue(state), "settlement clears vertical accumulation");
    }

    private static (bool Ready, string Direction, double RequiredDistance) ResolveMovement(MethodInfo resolve, Type directionType, object config, double dx, double dy) {
        object[] args = { dx, dy, config, true, null, 0.0, 0.0 };
        bool ready = (bool)resolve.Invoke(null, args);
        return (ready, args[4].ToString(), (double)args[6]);
    }

    private static void VerifyTwoFingerContinuationKeepsStaticFinger(Assembly assembly, Type mapper) {
        Type configType = RequiredType(assembly, "Config");
        Type controllerType = RequiredType(assembly, "ControllerState");
        Type stateType = RequiredType(assembly, "MapperForm+TouchGestureState");
        object instance = RuntimeHelpers.GetUninitializedObject(mapper);
        object config = Activator.CreateInstance(configType);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, config);
        Equal(100.0, configType.GetField("TouchGestureHoldStillDistance").GetValue(config), "two-finger static tolerance default");
        MethodInfo isStill = RequiredMethod(mapper, "IsTouchGestureStill");
        Equal(true, isStill.Invoke(null, new[] { (object)99.0, config }), "static movement below 100 remains still");
        Equal(false, isStill.Invoke(null, new[] { (object)100.0, config }), "static movement at 100 is outside tolerance");
        VerifyInitialTwoFingerSettlesNonTriggeredFinger(mapper, configType, controllerType, stateType);

        object gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "TwoFingerContinuation", true);
        SetField(stateType, gesture, "StaticFinger", 1);
        SetField(stateType, gesture, "StaticFingerId", 10);
        SetField(stateType, gesture, "StaticStartX", 100.0);
        SetField(stateType, gesture, "StaticStartY", 100.0);
        SetField(stateType, gesture, "ActiveFinger", 2);
        SetField(stateType, gesture, "ActiveFingerId", 20);
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);

        object controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 1);
        SetField(controllerType, controller, "Touch1Active", true);
        SetField(controllerType, controller, "Touch1Id", 10);
        SetField(controllerType, controller, "Touch1X", 100);
        SetField(controllerType, controller, "Touch1Y", 100);
        mapper.GetMethod("StopTwoFingerContinuationMover", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new[] { controller, (object)100.0 });

        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(true, stateType.GetField("TwoFingerContinuation").GetValue(gesture), "two-finger continuation stays active after mover lifts");
        Equal(10, stateType.GetField("StaticFingerId").GetValue(gesture), "held static finger remains continuation anchor");
        Equal(100.0, stateType.GetField("StaticStartX").GetValue(gesture), "mover release refreshes the static baseline");

        SetField(stateType, gesture, "Touch2Tracking", true);
        SetField(stateType, gesture, "Touch2Id", 30);
        SetField(stateType, gesture, "Touch2StartX", 700.0);
        SetField(stateType, gesture, "Touch2StartY", 500.0);
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);
        SetField(controllerType, controller, "TouchCount", 2);
        SetField(controllerType, controller, "Touch2Active", true);
        SetField(controllerType, controller, "Touch2Id", 30);
        SetField(controllerType, controller, "Touch2X", 710);
        SetField(controllerType, controller, "Touch2Y", 500);

        object[] moverArgs = { controller, 110.0, 0, 0 };
        bool accepted = (bool)mapper.GetMethod("TryGetActiveTwoFingerContinuationMover", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, moverArgs);
        Equal(true, accepted, "new mover is accepted while static finger stays held");
        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(10, stateType.GetField("StaticFingerId").GetValue(gesture), "static finger did not need a new touch-down");
        Equal(30, stateType.GetField("ActiveFingerId").GetValue(gesture), "new moving finger starts next two-finger segment");
        Equal(700.0, stateType.GetField("RepeatAnchorX").GetValue(gesture), "new segment starts at returning mover touch-down");

        VerifyTwoFingerStaticIdentityIsStrict(mapper, configType, controllerType, stateType);
        VerifyTwoFingerStaticInvalidation(mapper, configType, controllerType, stateType);
    }

    private static void VerifyInitialTwoFingerSettlesNonTriggeredFinger(Type mapper, Type configType, Type controllerType, Type stateType) {
        object config = Activator.CreateInstance(configType);
        object gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "HadTwoFingers", true);
        SetField(stateType, gesture, "Touch1Tracking", true);
        SetField(stateType, gesture, "Touch1Id", 10);
        SetField(stateType, gesture, "Touch1StartX", 700.0);
        SetField(stateType, gesture, "Touch1StartY", 500.0);
        SetField(stateType, gesture, "Touch2Tracking", true);
        SetField(stateType, gesture, "Touch2Id", 20);
        SetField(stateType, gesture, "Touch2StartX", 1700.0);
        SetField(stateType, gesture, "Touch2StartY", 500.0);

        object controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 2);
        SetField(controllerType, controller, "Touch1Active", true);
        SetField(controllerType, controller, "Touch1Id", 10);
        SetField(controllerType, controller, "Touch1X", 880);
        SetField(controllerType, controller, "Touch1Y", 500);
        SetField(controllerType, controller, "Touch2Active", true);
        SetField(controllerType, controller, "Touch2Id", 20);
        SetField(controllerType, controller, "Touch2X", 1800);
        SetField(controllerType, controller, "Touch2Y", 500);

        MethodInfo recognize = RequiredMethod(mapper, "TryRecognizeTouchGesture");
        object[] args = { gesture, controller, config, null };
        Equal(true, recognize.Invoke(null, args), "180 mover plus 100 non-triggered finger is a valid two-finger gesture");
        object recognition = args[3];
        Type recognitionType = recognition.GetType();
        Equal(true, recognitionType.GetField("TwoFingerContinuation").GetValue(recognition), "valid first trigger arms two-finger continuation");
        Equal(1, recognitionType.GetField("Finger").GetValue(recognition), "180-distance finger becomes mover");
        Equal(2, recognitionType.GetField("StaticFinger").GetValue(recognition), "non-triggered finger becomes static");
        Equal(1800.0, recognitionType.GetField("StaticStartX").GetValue(recognition), "static movement is settled at its current position");

        SetField(controllerType, controller, "Touch2X", 1880);
        args = new object[] { gesture, controller, config, null };
        Equal(false, recognize.Invoke(null, args), "two simultaneously triggered fingers do not create a two-finger gesture");
    }

    private static void VerifyTwoFingerStaticIdentityIsStrict(Type mapper, Type configType, Type controllerType, Type stateType) {
        object instance = RuntimeHelpers.GetUninitializedObject(mapper);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, Activator.CreateInstance(configType));

        object gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "TwoFingerContinuation", true);
        SetField(stateType, gesture, "StaticFinger", 1);
        SetField(stateType, gesture, "StaticFingerId", 10);
        SetField(stateType, gesture, "StaticStartX", 1800.0);
        SetField(stateType, gesture, "StaticStartY", 500.0);
        SetField(stateType, gesture, "ActiveFinger", 2);
        SetField(stateType, gesture, "ActiveFingerId", 20);
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);

        object controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 1);
        SetField(controllerType, controller, "Touch1Active", true);
        SetField(controllerType, controller, "Touch1Id", 30);
        SetField(controllerType, controller, "Touch1X", 700);
        SetField(controllerType, controller, "Touch1Y", 500);

        object[] staticArgs = { controller, 0, 0, 0 };
        bool found = (bool)mapper.GetMethod("TryGetTwoFingerContinuationStatic", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, staticArgs);
        Equal(false, found, "far returning mover is never adopted as the static finger");
        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(10, stateType.GetField("StaticFingerId").GetValue(gesture), "missing static finger identity is preserved while waiting");

        SetField(controllerType, controller, "Touch1Id", 11);
        SetField(controllerType, controller, "Touch1X", 1750);
        staticArgs = new object[] { controller, 0, 0, 0 };
        found = (bool)mapper.GetMethod("TryGetTwoFingerContinuationStatic", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, staticArgs);
        Equal(false, found, "nearby replacement contact cannot replace a released static finger");
        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(10, stateType.GetField("StaticFingerId").GetValue(gesture), "static identity is never transferred");
    }

    private static void VerifyTwoFingerStaticInvalidation(Type mapper, Type configType, Type controllerType, Type stateType) {
        object instance = RuntimeHelpers.GetUninitializedObject(mapper);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, Activator.CreateInstance(configType));

        object gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "Active", true);
        SetField(stateType, gesture, "Completed", true);
        SetField(stateType, gesture, "FingerCount", 2);
        SetField(stateType, gesture, "HadTwoFingers", true);
        SetField(stateType, gesture, "TwoFingerContinuation", true);
        SetField(stateType, gesture, "StaticFinger", 1);
        SetField(stateType, gesture, "StaticFingerId", 10);
        SetField(stateType, gesture, "StaticStartX", 1800.0);
        SetField(stateType, gesture, "StaticStartY", 500.0);
        SetField(stateType, gesture, "ActiveFinger", 2);
        SetField(stateType, gesture, "ActiveFingerId", 20);
        SetField(stateType, gesture, "RepeatAnchorX", 700.0);
        SetField(stateType, gesture, "RepeatAnchorY", 500.0);
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);

        object controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 2);
        SetField(controllerType, controller, "Touch1Active", true);
        SetField(controllerType, controller, "Touch1Id", 10);
        SetField(controllerType, controller, "Touch1X", 1900);
        SetField(controllerType, controller, "Touch1Y", 500);
        SetField(controllerType, controller, "Touch2Active", true);
        SetField(controllerType, controller, "Touch2Id", 20);
        SetField(controllerType, controller, "Touch2X", 700);
        SetField(controllerType, controller, "Touch2Y", 500);

        MethodInfo update = mapper.GetMethod("UpdateTwoFingerContinuationRepeat", BindingFlags.NonPublic | BindingFlags.Instance);
        update.Invoke(instance, new[] { controller, (object)100.0 });
        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(false, stateType.GetField("Active").GetValue(gesture), "static movement at the tolerance ends the gesture");
        Equal(true, mapper.GetField("_touchGestureBlockedUntilRelease", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "static drift blocks every later gesture until release");
        Equal(true, mapper.GetField("_touchGestureBlockOwnsTouchpad", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "invalidated two-finger gesture retains touchpad ownership");

        instance = RuntimeHelpers.GetUninitializedObject(mapper);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, Activator.CreateInstance(configType));
        gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "Active", true);
        SetField(stateType, gesture, "Completed", true);
        SetField(stateType, gesture, "FingerCount", 2);
        SetField(stateType, gesture, "HadTwoFingers", true);
        SetField(stateType, gesture, "TwoFingerContinuation", true);
        SetField(stateType, gesture, "StaticFinger", 1);
        SetField(stateType, gesture, "StaticFingerId", 10);
        SetField(stateType, gesture, "StaticStartX", 1800.0);
        SetField(stateType, gesture, "StaticStartY", 500.0);
        SetField(stateType, gesture, "ActiveFinger", 2);
        SetField(stateType, gesture, "ActiveFingerId", 20);
        SetField(stateType, gesture, "RepeatAnchorX", 700.0);
        SetField(stateType, gesture, "RepeatAnchorY", 500.0);
        SetField(stateType, gesture, "Side", Enum.Parse(stateType.GetField("Side").FieldType, "Left"));
        SetField(stateType, gesture, "Direction", Enum.Parse(stateType.GetField("Direction").FieldType, "Right"));
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);
        controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 2);
        SetField(controllerType, controller, "Touch1Active", true);
        SetField(controllerType, controller, "Touch1Id", 10);
        SetField(controllerType, controller, "Touch1X", 1899);
        SetField(controllerType, controller, "Touch1Y", 500);
        SetField(controllerType, controller, "Touch2Active", true);
        SetField(controllerType, controller, "Touch2Id", 20);
        SetField(controllerType, controller, "Touch2X", 880);
        SetField(controllerType, controller, "Touch2Y", 500);
        update.Invoke(instance, new[] { controller, (object)100.5 });
        gesture = mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        Equal(1899.0, stateType.GetField("StaticStartX").GetValue(gesture), "each physical mover step refreshes the static baseline");
        Equal(880.0, stateType.GetField("RepeatAnchorX").GetValue(gesture), "the same step settles the mover axis anchor");
        Equal(false, mapper.GetField("_touchGestureBlockedUntilRelease", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "static movement below 100 is accepted before the mover settles");
        SetField(controllerType, controller, "Touch1X", 1998);
        update.Invoke(instance, new[] { controller, (object)100.6 });
        Equal(false, mapper.GetField("_touchGestureBlockedUntilRelease", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "static tolerance is measured from the refreshed baseline");
        SetField(controllerType, controller, "Touch1X", 1999);
        update.Invoke(instance, new[] { controller, (object)100.7 });
        Equal(true, mapper.GetField("_touchGestureBlockedUntilRelease", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "100 movement from the refreshed static baseline ends the gesture");

        instance = RuntimeHelpers.GetUninitializedObject(mapper);
        mapper.GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, Activator.CreateInstance(configType));
        gesture = Activator.CreateInstance(stateType);
        SetField(stateType, gesture, "Active", true);
        SetField(stateType, gesture, "Completed", true);
        SetField(stateType, gesture, "FingerCount", 2);
        SetField(stateType, gesture, "HadTwoFingers", true);
        SetField(stateType, gesture, "TwoFingerContinuation", true);
        SetField(stateType, gesture, "StaticFinger", 1);
        SetField(stateType, gesture, "StaticFingerId", 10);
        SetField(stateType, gesture, "StaticStartX", 1800.0);
        SetField(stateType, gesture, "StaticStartY", 500.0);
        SetField(stateType, gesture, "ActiveFinger", 2);
        SetField(stateType, gesture, "ActiveFingerId", 20);
        mapper.GetField("_touchGesture", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, gesture);
        controller = Activator.CreateInstance(controllerType);
        SetField(controllerType, controller, "TouchCount", 1);
        SetField(controllerType, controller, "Touch2Active", true);
        SetField(controllerType, controller, "Touch2Id", 20);
        SetField(controllerType, controller, "Touch2X", 880);
        SetField(controllerType, controller, "Touch2Y", 500);
        update.Invoke(instance, new[] { controller, (object)101.0 });
        Equal(true, mapper.GetField("_touchGestureBlockedUntilRelease", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance), "released static finger blocks the moving finger from becoming another gesture");
    }

    private static void VerifyTouchpadClicks(Assembly assembly, Type mapper) {
        Type configType = RequiredType(assembly, "Config");
        Type controllerType = RequiredType(assembly, "ControllerState");
        MethodInfo resolve = RequiredMethod(mapper, "ResolveTouchpadClick");
        object config = Activator.CreateInstance(configType);

        Equal("Backspace", ResolveClick(controllerType, resolve, config, 0, 0, false), "click without touch");
        Equal("Delete", ResolveClick(controllerType, resolve, config, 1, 100, true), "left click");
        Equal("CapsLock", ResolveClick(controllerType, resolve, config, 1, 960, true), "buffer click");
        Equal("Backspace", ResolveClick(controllerType, resolve, config, 1, 1800, true), "right click");

        object twoTouches = Activator.CreateInstance(controllerType);
        SetField(controllerType, twoTouches, "TouchCount", 2);
        SetField(controllerType, twoTouches, "Touch1Active", true);
        SetField(controllerType, twoTouches, "Touch2Active", true);
        object result = resolve.Invoke(null, new[] { twoTouches, config });
        Equal("Backspace", result.GetType().GetField("Key").GetValue(result).ToString(), "two-finger click");
    }

    private static void VerifyHidReportParsing(Assembly assembly) {
        Type hidType = RequiredType(assembly, "DirectHidController");
        MethodInfo parse = RequiredMethod(hidType, "TryParseDualSenseReport");
        byte[] report = new byte[64];
        report[0] = 0x01;
        report[1] = 127;
        report[2] = 127;
        report[3] = 127;
        report[4] = 127;
        report[5] = 128;
        report[6] = 255;
        report[8] = 0x10;
        report[9] = 0x51;
        report[10] = 0x07;
        WriteTouchPoint(report, 33, 5, true, 100, 200);
        WriteTouchPoint(report, 37, 6, false, 0, 0);

        object[] args = { report, null };
        Equal(true, parse.Invoke(null, args), "a 64-byte USB DualSense report is accepted");
        object state = args[1];
        Type stateType = RequiredType(assembly, "ControllerState");
        Equal(true, stateType.GetField("Connected").GetValue(state), "parsed controller is connected");
        Equal(true, stateType.GetField("Up").GetValue(state), "USB report d-pad is parsed");
        Equal(true, stateType.GetField("Square").GetValue(state), "USB report face button is parsed");
        Equal(true, stateType.GetField("L1").GetValue(state), "USB report shoulder is parsed");
        Equal(true, stateType.GetField("Create").GetValue(state), "USB report Create is parsed");
        Equal(true, stateType.GetField("L3").GetValue(state), "USB report L3 is parsed");
        Equal(true, stateType.GetField("Home").GetValue(state), "USB report Home is parsed");
        Equal(true, stateType.GetField("TouchClick").GetValue(state), "USB report touch click is parsed");
        Equal(true, stateType.GetField("Mute").GetValue(state), "USB report Mute is parsed");
        Equal(1, stateType.GetField("TouchCount").GetValue(state), "one active touch point is counted");
        Equal(5, stateType.GetField("Touch1Id").GetValue(state), "touch point identity is parsed");
        Equal(100, stateType.GetField("Touch1X").GetValue(state), "touch point X is parsed");
        Equal(200, stateType.GetField("Touch1Y").GetValue(state), "touch point Y is parsed");

        args = new object[] { new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0 }, null };
        Equal(false, parse.Invoke(null, args), "an unsupported report ID is rejected rather than replaying stale state");
        Equal(null, args[1], "a rejected report produces no controller state");
    }

    private static void WriteTouchPoint(byte[] report, int offset, int id, bool active, int x, int y) {
        report[offset] = (byte)((active ? 0 : 0x80) | (id & 0x7F));
        report[offset + 1] = (byte)(x & 0xFF);
        report[offset + 2] = (byte)(((x >> 8) & 0x0F) | ((y & 0x0F) << 4));
        report[offset + 3] = (byte)((y >> 4) & 0xFF);
    }

    private static string ResolveClick(Type controllerType, MethodInfo resolve, object config, int count, int x, bool active) {
        object state = Activator.CreateInstance(controllerType);
        SetField(controllerType, state, "TouchCount", count);
        SetField(controllerType, state, "Touch1Active", active);
        SetField(controllerType, state, "Touch1X", x);
        object result = resolve.Invoke(null, new[] { state, config });
        return result.GetType().GetField("Key").GetValue(result).ToString();
    }

    private static Type RequiredType(Assembly assembly, string name) {
        return assembly.GetType(name, true);
    }

    private static MethodInfo RequiredMethod(Type type, string name) {
        return type.GetMethod(name, PrivateStatic) ?? throw new InvalidOperationException($"Missing method {name}");
    }

    private static void SetField(Type type, object target, string name, object value) {
        (type.GetField(name) ?? throw new InvalidOperationException($"Missing field {name}")).SetValue(target, value);
    }

    private static void Equal(object expected, object actual, string name) {
        if (!Equals(expected, actual)) throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    private static void NotNull(object value, string name) {
        if (value == null) throw new InvalidOperationException($"{name}: expected non-null value");
    }

    private static void ExpectInvocationFailure(Action action, string name) {
        try {
            action();
        } catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) {
            return;
        }
        throw new InvalidOperationException($"{name}: expected InvalidOperationException");
    }
}
