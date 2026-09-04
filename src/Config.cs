using System;

internal sealed class Config {
    public bool Enabled = true;
    public double MouseSensitivity = 1.0;
    public double MouseMaxSpeed = 20.0;
    public double RightStickDeadzone = 0.015;
    public string RightStickCurve = "power";
    public double RightStickCurveExponent = 3.0;
    public double RightStickLowSpeedAssist = 0.05;
    public double RightStickSmoothingMs = 5.0;
    public double MouseScrollCurveExponent = 3.0;
    public double MouseScrollSmoothingMs = 5.0;
    public double LeftStickEnterDeadzone = 0.30;
    public double LeftStickModifierEnterDeadzone = 0.50;
    public double TriggerPressThreshold = 0.25;
    public double TriggerReleaseThreshold = 0.15;
    public int RepeatDelayMs = 300;
    public int RepeatIntervalMs = 12;
    public int BaseRepeatSlowIntervalMs = 120;
    public int BaseRepeatRampMs = 1500;
    public int ActionLayerGraceMs = 45;
    public int ModifierBindingWindowMs = 45;
    public int ActionLayerPostGraceMs = 15;
    public int LayerTakeoverWindowMs = 30;
    public int LayerOccupancyCarryCutoffMs = 20;
    public int ComboLayerWindowMs = 35;
    public int ScrollSlowIntervalMs = 1500;
    public int ScrollFastIntervalMs = 15;
    public int ScrollReportIntervalMs = 4;
    public double TouchGestureHoldStillDistance = 100.0;
    public double TouchGestureVerticalThreshold = 150.0;
    public double TouchGestureHorizontalThreshold = 180.0;
    public double TouchGestureVerticalRepeatDistance = 150.0;
    public double TouchGestureHorizontalRepeatDistance = 180.0;
    public int TouchGestureTimeRepeatDelayMs = 450;
    public int TouchGestureTimeRepeatIntervalMs = 450;
    public int TouchGestureDesktopRepeatIntervalMs = 550;
    public int TouchGestureSideConfirmedWidth = 550;
    public int R3FreezeMs = 60;
    public int ClutchLongPressMs = 250;

    public void Validate() {
        RequireFiniteNonNegative(MouseSensitivity, nameof(MouseSensitivity));
        RequireFiniteNonNegative(MouseMaxSpeed, nameof(MouseMaxSpeed));
        RequireUnitIntervalExclusiveUpper(RightStickDeadzone, nameof(RightStickDeadzone));
        if (!String.Equals(RightStickCurve, "power", StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(RightStickCurve, "linear", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(nameof(RightStickCurve) + " 只支持 power 或 linear。");
        }
        RequireFinitePositive(RightStickCurveExponent, nameof(RightStickCurveExponent));
        RequireUnitIntervalInclusive(RightStickLowSpeedAssist, nameof(RightStickLowSpeedAssist));
        RequireFiniteNonNegative(RightStickSmoothingMs, nameof(RightStickSmoothingMs));
        RequireFinitePositive(MouseScrollCurveExponent, nameof(MouseScrollCurveExponent));
        RequireFiniteNonNegative(MouseScrollSmoothingMs, nameof(MouseScrollSmoothingMs));
        RequireUnitIntervalExclusiveUpper(LeftStickEnterDeadzone, nameof(LeftStickEnterDeadzone));
        RequireUnitIntervalExclusiveUpper(LeftStickModifierEnterDeadzone, nameof(LeftStickModifierEnterDeadzone));
        RequireUnitIntervalInclusive(TriggerPressThreshold, nameof(TriggerPressThreshold));
        RequireUnitIntervalInclusive(TriggerReleaseThreshold, nameof(TriggerReleaseThreshold));
        if (TriggerReleaseThreshold > TriggerPressThreshold) {
            throw new InvalidOperationException(nameof(TriggerReleaseThreshold) + " 不能大于 " + nameof(TriggerPressThreshold) + "。");
        }

        RequireNonNegative(RepeatDelayMs, nameof(RepeatDelayMs));
        RequirePositive(RepeatIntervalMs, nameof(RepeatIntervalMs));
        RequirePositive(BaseRepeatSlowIntervalMs, nameof(BaseRepeatSlowIntervalMs));
        RequireNonNegative(BaseRepeatRampMs, nameof(BaseRepeatRampMs));
        if (RepeatIntervalMs > BaseRepeatSlowIntervalMs) {
            throw new InvalidOperationException(nameof(RepeatIntervalMs) + " 不能大于 " + nameof(BaseRepeatSlowIntervalMs) + "。");
        }
        RequireNonNegative(ActionLayerGraceMs, nameof(ActionLayerGraceMs));
        RequireNonNegative(ModifierBindingWindowMs, nameof(ModifierBindingWindowMs));
        RequireNonNegative(ActionLayerPostGraceMs, nameof(ActionLayerPostGraceMs));
        RequireNonNegative(LayerTakeoverWindowMs, nameof(LayerTakeoverWindowMs));
        RequireNonNegative(LayerOccupancyCarryCutoffMs, nameof(LayerOccupancyCarryCutoffMs));
        RequireNonNegative(ComboLayerWindowMs, nameof(ComboLayerWindowMs));
        if (LayerOccupancyCarryCutoffMs > LayerTakeoverWindowMs) {
            throw new InvalidOperationException(nameof(LayerOccupancyCarryCutoffMs) + " 不能大于 " + nameof(LayerTakeoverWindowMs) + "。");
        }
        RequirePositive(ScrollSlowIntervalMs, nameof(ScrollSlowIntervalMs));
        RequirePositive(ScrollFastIntervalMs, nameof(ScrollFastIntervalMs));
        RequirePositive(ScrollReportIntervalMs, nameof(ScrollReportIntervalMs));
        if (ScrollFastIntervalMs > ScrollSlowIntervalMs) {
            throw new InvalidOperationException(nameof(ScrollFastIntervalMs) + " 不能大于 " + nameof(ScrollSlowIntervalMs) + "。");
        }
        RequireFinitePositive(TouchGestureHoldStillDistance, nameof(TouchGestureHoldStillDistance));
        RequireFinitePositive(TouchGestureVerticalThreshold, nameof(TouchGestureVerticalThreshold));
        RequireFinitePositive(TouchGestureHorizontalThreshold, nameof(TouchGestureHorizontalThreshold));
        RequireFinitePositive(TouchGestureVerticalRepeatDistance, nameof(TouchGestureVerticalRepeatDistance));
        RequireFinitePositive(TouchGestureHorizontalRepeatDistance, nameof(TouchGestureHorizontalRepeatDistance));
        if (TouchGestureHoldStillDistance >= Math.Min(TouchGestureVerticalThreshold, TouchGestureHorizontalThreshold)) {
            throw new InvalidOperationException(nameof(TouchGestureHoldStillDistance) + " 必须小于两个首次触发阈值。");
        }
        RequireNonNegative(TouchGestureTimeRepeatDelayMs, nameof(TouchGestureTimeRepeatDelayMs));
        RequirePositive(TouchGestureTimeRepeatIntervalMs, nameof(TouchGestureTimeRepeatIntervalMs));
        RequirePositive(TouchGestureDesktopRepeatIntervalMs, nameof(TouchGestureDesktopRepeatIntervalMs));
        if (TouchGestureSideConfirmedWidth < 1 || TouchGestureSideConfirmedWidth > 960) {
            throw new InvalidOperationException(nameof(TouchGestureSideConfirmedWidth) + " 必须在 1..960 之间。");
        }
        RequireNonNegative(R3FreezeMs, nameof(R3FreezeMs));
        RequirePositive(ClutchLongPressMs, nameof(ClutchLongPressMs));
    }

    private static void RequireFinitePositive(double value, string name) {
        if (Double.IsNaN(value) || Double.IsInfinity(value) || value <= 0.0) {
            throw new InvalidOperationException(name + " 必须是有限正数。");
        }
    }

    private static void RequireFiniteNonNegative(double value, string name) {
        if (Double.IsNaN(value) || Double.IsInfinity(value) || value < 0.0) {
            throw new InvalidOperationException(name + " 必须是有限非负数。");
        }
    }

    private static void RequireUnitIntervalExclusiveUpper(double value, string name) {
        if (Double.IsNaN(value) || Double.IsInfinity(value) || value < 0.0 || value >= 1.0) {
            throw new InvalidOperationException(name + " 必须在 [0, 1) 内。");
        }
    }

    private static void RequireUnitIntervalInclusive(double value, string name) {
        if (Double.IsNaN(value) || Double.IsInfinity(value) || value < 0.0 || value > 1.0) {
            throw new InvalidOperationException(name + " 必须在 [0, 1] 内。");
        }
    }

    private static void RequirePositive(int value, string name) {
        if (value <= 0) throw new InvalidOperationException(name + " 必须大于 0。");
    }

    private static void RequireNonNegative(int value, string name) {
        if (value < 0) throw new InvalidOperationException(name + " 不能小于 0。");
    }
}
