namespace AvaloniaSilkEffects.Tempera;

/// <summary>Folia temperaMotionEasing.ts: CSS cubic-bezier solved by bisection, and the mode's three curves.</summary>
public static class TemperaEasing
{
    public static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));

    private static double Coordinate(double point1, double point2, double time)
    {
        var inverse = 1 - time;
        return 3 * inverse * inverse * time * point1 + 3 * inverse * time * time * point2 + time * time * time;
    }

    public static double CubicBezier(double x1, double y1, double x2, double y2, double value)
    {
        var target = Clamp01(value);
        if (target == 0 || target == 1) return target;
        double low = 0, high = 1, parameter = target;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var x = Coordinate(x1, x2, parameter);
            if (x < target) low = parameter;
            else high = parameter;
            parameter = (low + high) / 2;
        }
        return Coordinate(y1, y2, parameter);
    }

    /// <summary>Long soft deceleration: decisive at the start, then a long creeping tail.</summary>
    public static double Enter(double value) => CubicBezier(0.22, 1, 0.36, 1, value);

    public static double InOut(double value) => CubicBezier(0.62, 0, 0.32, 1, value);

    /// <summary>Mild anticipation for scale, so glyphs settle with a small overshoot.</summary>
    public static double SoftBack(double value)
    {
        var t = Clamp01(value);
        const double c = 1.42;
        return 1 + (c + 1) * Math.Pow(t - 1, 3) + c * Math.Pow(t - 1, 2);
    }

    /// <summary>A shot-relative fraction mapped onto seconds, clamped to a watchable speed.</summary>
    public static double ShotPaced(double shotDuration, double fraction, double minSeconds, double maxSeconds) =>
        Math.Min(maxSeconds, Math.Max(minSeconds, shotDuration * fraction));
}

public enum TemperaEnterStyle
{
    Slide,
    FromLeft,
    FromRight,
    FromAbove,
    FromBelow,
    Swing,
    Stamp,
}

/// <summary>Everything the per-frame glyph solver needs; the runtime never reads layout again.</summary>
public readonly record struct TemperaGlyphMotionInput(
    double StartTime,
    double SettleTime,
    double EndTime,
    double EnterX,
    double EnterY,
    double EnterRotation,
    double EnterScale,
    double Rotation,
    TemperaEnterStyle EnterStyle,
    double ReleaseTime,
    double TrackingX,
    double TrackingY);

public readonly record struct TemperaGlyphMotionFrame(
    bool Visible,
    double Alpha,
    double X,
    double Y,
    double Rotation,
    double ScaleX,
    double ScaleY,
    double EchoX,
    double EchoY,
    double EchoAlpha);

/// <summary>Folia temperaEnterStyles.ts + temperaMotion.ts: absolute-time glyph motion, seek-safe by construction.</summary>
public static class TemperaMotion
{
    public static readonly TemperaEnterStyle[] EnterStyles =
    [
        TemperaEnterStyle.Slide, TemperaEnterStyle.FromLeft, TemperaEnterStyle.FromRight, TemperaEnterStyle.FromAbove,
        TemperaEnterStyle.FromBelow, TemperaEnterStyle.Swing, TemperaEnterStyle.Stamp,
    ];

    private const double CurrentEmphasis = 0.05;
    private const double EmphasisDecay = 0.26;
    private const double EchoAlpha = 0.5;
    /// <summary>Longest window opacity and the motion echo may run over: a glyph has to be readable while it travels.</summary>
    private const double MaxRevealWindow = 1.35;
    private const double ReleaseTracking = 0.055;

    private readonly record struct EnterFrame(double X, double Y, double Rotation, double ScaleX, double ScaleY, double Echo);

    private static EnterFrame Directional(in TemperaGlyphMotionInput input, double travel, double uniform, double dirX, double dirY,
        double rotationScale)
    {
        var magnitude = Math.Sqrt(input.EnterX * input.EnterX + input.EnterY * input.EnterY);
        var length = Math.Sqrt(dirX * dirX + dirY * dirY);
        if (length == 0) length = 1;
        return new EnterFrame(dirX / length * magnitude * travel, dirY / length * magnitude * travel,
            input.EnterRotation * rotationScale * travel, uniform, uniform, travel);
    }

    private static EnterFrame ResolveEnter(TemperaEnterStyle style, in TemperaGlyphMotionInput input, double travel, double linear)
    {
        var settle = TemperaEasing.SoftBack(linear);
        var uniform = input.EnterScale + (1 - input.EnterScale) * settle;
        return style switch
        {
            TemperaEnterStyle.FromLeft => Directional(input, travel, uniform, -1, 0.12, 0.4),
            TemperaEnterStyle.FromRight => Directional(input, travel, uniform, 1, -0.12, 0.4),
            TemperaEnterStyle.FromAbove => Directional(input, travel, uniform, 0.12, -1, 0.4),
            TemperaEnterStyle.FromBelow => Directional(input, travel, uniform, -0.12, 1, 0.4),
            TemperaEnterStyle.Swing => new EnterFrame(input.EnterX * 0.6 * travel, input.EnterY * 0.6 * travel,
                (input.EnterRotation + Math.Sign(input.EnterRotation == 0 ? 1 : input.EnterRotation) * 0.95) * travel, uniform, uniform,
                travel * 0.8),
            TemperaEnterStyle.Stamp => new EnterFrame(0, 0, input.EnterRotation * 0.6 * travel, 1 + travel * 0.7, 1 + travel * 0.7, 0),
            _ => new EnterFrame(input.EnterX * travel, input.EnterY * travel, input.EnterRotation * travel, uniform, uniform, travel),
        };
    }

    /// <summary>One glyph's entrance plus the post-sung tracking release; <paramref name="motion"/> 0 pins it to its layout.</summary>
    public static TemperaGlyphMotionFrame Resolve(in TemperaGlyphMotionInput glyph, double time, double motion)
    {
        var window = Math.Max(glyph.SettleTime - glyph.StartTime, 0.08);
        var linear = TemperaEasing.Clamp01((time - glyph.StartTime) / window);
        var travel = 1 - TemperaEasing.Enter(linear);
        var entrance = ResolveEnter(glyph.EnterStyle, glyph, travel, linear);
        var reveal = TemperaEasing.Clamp01((time - glyph.StartTime) / Math.Min(window, MaxRevealWindow));
        var alpha = TemperaEasing.InOut(TemperaEasing.Clamp01(reveal * 2.4));

        var sungWindow = glyph.EndTime - glyph.StartTime;
        var attack = Math.Min(0.12, sungWindow * 0.5);
        var emphasis = sungWindow <= 0
            ? 0
            : TemperaEasing.InOut(TemperaEasing.Clamp01((time - glyph.StartTime) / Math.Max(attack, 0.02)))
              * (1 - TemperaEasing.InOut(TemperaEasing.Clamp01((time - glyph.EndTime) / EmphasisDecay)));

        var releaseStart = Math.Max(glyph.EndTime, glyph.SettleTime);
        var release = TemperaEasing.InOut(TemperaEasing.Clamp01((time - releaseStart) / Math.Max(glyph.ReleaseTime - releaseStart, 0.001)));
        var spread = release * TemperaEasing.Clamp01(motion) * ReleaseTracking;
        var amount = TemperaEasing.Clamp01(motion);
        var swell = emphasis * CurrentEmphasis * amount;
        return new TemperaGlyphMotionFrame(
            time >= glyph.StartTime,
            alpha,
            entrance.X * motion + glyph.TrackingX * spread,
            entrance.Y * motion + glyph.TrackingY * spread,
            glyph.Rotation + entrance.Rotation * motion,
            entrance.ScaleX + (1 - entrance.ScaleX) * (1 - amount) + swell,
            entrance.ScaleY + (1 - entrance.ScaleY) * (1 - amount) + swell,
            entrance.X * motion,
            entrance.Y * motion,
            entrance.Echo * (1 - TemperaEasing.Enter(reveal)) * EchoAlpha * amount);
    }
}
