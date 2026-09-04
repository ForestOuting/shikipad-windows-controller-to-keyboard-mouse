using System;

internal sealed class LeftStickScrollIntegrator {
    internal const int WheelDelta = 120;
    private const int MaxWheelDeltaPerFrame = 120;

    private double _accumulatedWheelDelta;
    private double _secondsSinceLastReport;
    private double _smoothedNormalizedRadius;
    private int _lastDirection;
    private bool _hasActiveDirection;
    private bool _hasSmoothedRadius;

    public void Reset() {
        _accumulatedWheelDelta = 0.0;
        _secondsSinceLastReport = 0.0;
        _smoothedNormalizedRadius = 0.0;
        _lastDirection = 0;
        _hasActiveDirection = false;
        _hasSmoothedRadius = false;
    }

    public bool TryUpdate(double radius, double deltaSec, Config config, int direction, out int wheelDelta) {
        wheelDelta = 0;
        if (direction == 0 || deltaSec <= 0.0) return false;

        double clampedRadius = Clamp(radius, 0.0, 1.0);
        if (clampedRadius <= config.LeftStickEnterDeadzone) {
            Reset();
            return false;
        }

        double normalized = NormalizeRadius(clampedRadius, config);
        if (normalized <= 0.0) {
            Reset();
            return false;
        }

        if (!_hasActiveDirection || _lastDirection != direction) {
            _hasActiveDirection = true;
            _lastDirection = direction;
            _accumulatedWheelDelta = 0.0;
            _secondsSinceLastReport = Double.PositiveInfinity;
            _hasSmoothedRadius = false;
        }

        double smoothedNormalized = SmoothNormalizedRadius(normalized, deltaSec, config.MouseScrollSmoothingMs);
        _accumulatedWheelDelta += direction * WheelDeltaPerSecond(smoothedNormalized, config) * deltaSec;
        _secondsSinceLastReport += deltaSec;
        if (_secondsSinceLastReport * 1000.0 < Math.Max(1, config.ScrollReportIntervalMs)) return false;
        wheelDelta = TakeRoundedWheelDelta(ref _accumulatedWheelDelta);
        if (wheelDelta != 0) _secondsSinceLastReport = 0.0;
        return wheelDelta != 0;
    }

    internal static double NormalizeRadius(double radius, Config config) {
        double enterDeadzone = Clamp(config.LeftStickEnterDeadzone, 0.0, 0.99);
        double normalized = (radius - enterDeadzone) / (1.0 - enterDeadzone);
        return Clamp(normalized, 0.0, 1.0);
    }

    internal static double ScrollIntervalMs(double normalizedRadius, Config config) {
        double rate = WheelDeltaPerSecond(normalizedRadius, config);
        if (rate <= 0.0) return double.PositiveInfinity;
        return WheelDelta * 1000.0 / rate;
    }

    internal static double WheelDeltaPerSecond(double normalizedRadius, Config config) {
        double normalized = Clamp(normalizedRadius, 0.0, 1.0);
        if (normalized <= 0.0) return 0.0;

        double slowInterval = Math.Max(1.0, (double)config.ScrollSlowIntervalMs);
        double fastInterval = Math.Max(1.0, Math.Min((double)config.ScrollFastIntervalMs, slowInterval));
        double slowRate = WheelDelta * 1000.0 / slowInterval;
        double fastRate = WheelDelta * 1000.0 / fastInterval;
        double powerRatio = Math.Pow(normalized, Math.Max(0.001, config.MouseScrollCurveExponent));
        double rate = fastRate * powerRatio;
        if (rate < slowRate) rate = slowRate;
        if (rate > fastRate) rate = fastRate;
        return rate;
    }

    private double SmoothNormalizedRadius(double normalized, double deltaSec, double smoothingMs) {
        if (smoothingMs <= 0.0 || deltaSec <= 0.0) {
            _smoothedNormalizedRadius = normalized;
            _hasSmoothedRadius = true;
            return normalized;
        }

        if (!_hasSmoothedRadius) {
            _smoothedNormalizedRadius = normalized;
            _hasSmoothedRadius = true;
        } else {
            double alpha = SmoothingAlpha(deltaSec, smoothingMs);
            _smoothedNormalizedRadius += (normalized - _smoothedNormalizedRadius) * alpha;
        }

        return _smoothedNormalizedRadius;
    }

    private static double SmoothingAlpha(double deltaSec, double smoothingMs) {
        double smoothingSec = Math.Max(0.001, smoothingMs / 1000.0);
        return 1.0 - Math.Exp(-deltaSec / smoothingSec);
    }

    private static int TakeRoundedWheelDelta(ref double accumulator) {
        int delta = 0;
        if (accumulator >= 0.5) {
            delta = (int)Math.Floor(accumulator + 0.5);
            if (delta > MaxWheelDeltaPerFrame) delta = MaxWheelDeltaPerFrame;
        } else if (accumulator <= -0.5) {
            delta = (int)Math.Ceiling(accumulator - 0.5);
            if (delta < -MaxWheelDeltaPerFrame) delta = -MaxWheelDeltaPerFrame;
        }
        accumulator -= delta;
        return delta;
    }

    private static double Clamp(double value, double min, double max) {
        return value < min ? min : (value > max ? max : value);
    }

}
