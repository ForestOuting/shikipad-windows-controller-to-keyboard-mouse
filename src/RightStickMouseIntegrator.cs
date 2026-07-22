using System;

internal sealed class RightStickMouseIntegrator {

    private double _accumX;
    private double _accumY;
    private double _smoothedX;
    private double _smoothedY;
    private bool _hasSmoothedInput;

    public void Reset() {
        _accumX = 0.0;
        _accumY = 0.0;
        _smoothedX = 0.0;
        _smoothedY = 0.0;
        _hasSmoothedInput = false;
    }

    public bool TryUpdate(double x, double y, double deltaSec, Config config, out int dx, out int dy) {
        dx = 0;
        dy = 0;

        double rawRadius = Math.Sqrt(x * x + y * y);
        if (rawRadius <= config.RightStickDeadzone) {
            Reset();
            return false;
        }

        SmoothInput(ref x, ref y, deltaSec, config.RightStickSmoothingMs);
        double actualRadius = Math.Sqrt(x * x + y * y);
        double radius = Clamp(actualRadius, 0.0, 1.0);
        if (radius <= config.RightStickDeadzone) {
            Reset();
            return false;
        }

        double normalizedRadius = Clamp((radius - config.RightStickDeadzone) / (1.0 - config.RightStickDeadzone), 0.0, 1.0);
        double dirX = x / actualRadius;
        double dirY = y / actualRadius;
        double powerRatio = ApplyResponseCurve(normalizedRadius, config.RightStickCurve, config.RightStickCurveExponent);
        double speed = config.MouseMaxSpeed * deltaSec * 120.0 * config.MouseSensitivity;
        double rawDx = dirX * powerRatio * speed;
        double rawDy = dirY * powerRatio * speed;
        if (Math.Abs(rawDx) + Math.Abs(rawDy) < 0.000001) return false;

        _accumX += rawDx;
        _accumY += rawDy;
        dx = TakeRoundedMouseDelta(ref _accumX);
        dy = TakeRoundedMouseDelta(ref _accumY);
        return dx != 0 || dy != 0;
    }

    internal static double ApplyResponseCurve(double normalizedRadius, string curve, double exponent) {
        if (String.Equals(curve, "linear", StringComparison.OrdinalIgnoreCase)) return normalizedRadius;
        return Math.Pow(normalizedRadius, exponent);
    }

    private void SmoothInput(ref double x, ref double y, double deltaSec, double smoothingMs) {
        if (smoothingMs <= 0.0 || deltaSec <= 0.0) {
            _smoothedX = x;
            _smoothedY = y;
            _hasSmoothedInput = true;
            return;
        }

        if (!_hasSmoothedInput) {
            _smoothedX = x;
            _smoothedY = y;
            _hasSmoothedInput = true;
        } else {
            double alpha = SmoothingAlpha(deltaSec, smoothingMs);
            _smoothedX += (x - _smoothedX) * alpha;
            _smoothedY += (y - _smoothedY) * alpha;
        }

        x = _smoothedX;
        y = _smoothedY;
    }

    private static double SmoothingAlpha(double deltaSec, double smoothingMs) {
        double smoothingSec = Math.Max(0.001, smoothingMs / 1000.0);
        return 1.0 - Math.Exp(-deltaSec / smoothingSec);
    }

    private static int TakeRoundedMouseDelta(ref double accumulator) {
        int delta = 0;
        if (accumulator >= 0.5) {
            delta = (int)Math.Floor(accumulator + 0.5);
        } else if (accumulator <= -0.5) {
            delta = (int)Math.Ceiling(accumulator - 0.5);
        }
        accumulator -= delta;
        return delta;
    }

    private static double Clamp(double value, double min, double max) {
        return value < min ? min : (value > max ? max : value);
    }

}
