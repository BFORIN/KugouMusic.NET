using System;
using System.Runtime.CompilerServices;

// 统一的缓动计算工具：通过二分迭代法求解 CSS 三次贝塞尔曲线，并提供常用的动效曲线函数

namespace AvaloniaSilkEffects;

/// <summary>
/// 特效管线通用的缓动曲线工具。
/// </summary>
public static class EffectEasing
{
    private static double Coordinate(double point1, double point2, double time)
    {
        var inverse = 1 - time;
        return 3 * inverse * inverse * time * point1 + 3 * inverse * time * time * point2 + time * time * time;
    }

    /// <summary>
    /// 通过 12 次二分查找逼近求解参数化三次贝塞尔曲线在指定进度的值。
    /// </summary>
    public static double CubicBezier(double x1, double y1, double x2, double y2, double value)
    {
        var target = EffectMath.Clamp01(value);
        if (target is 0 or 1) return target;
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

    /// <summary>长减速平滑入场曲线（Folia 默认标准入场：CubicBezier(0.22, 1, 0.36, 1)）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double EaseEnter(double value) => CubicBezier(0.22, 1, 0.36, 1, value);

    /// <summary>两端平滑曲线（CubicBezier(0.65, 0, 0.35, 1)）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double EaseInOut(double value) => CubicBezier(0.65, 0, 0.35, 1, value);

    /// <summary>两端平滑曲线（CubicBezier(0.62, 0, 0.32, 1)）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double EaseInOutTempera(double value) => CubicBezier(0.62, 0, 0.32, 1, value);

    /// <summary>指数衰减减速曲线（ExpoOut）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ExpoOut(double value) => value >= 1 ? 1 : 1 - Math.Pow(2, -10 * value);

    /// <summary>弹性振荡回弹曲线（ElasticOut）。</summary>
    public static double ElasticOut(double value)
    {
        const double period = 0.3;
        return Math.Pow(2, -10 * value) * Math.Sin((value - period / 4) * (2 * Math.PI) / period) + 1;
    }

    /// <summary>轻微回弹过度曲线，适合字形尺寸落地。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SoftBack(double value, double overshoot = 1.42)
    {
        var t = EffectMath.Clamp01(value);
        return 1 + (overshoot + 1) * Math.Pow(t - 1, 3) + overshoot * Math.Pow(t - 1, 2);
    }

    /// <summary>三次缓动两端对称曲线。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double EaseInOutCubic(double value)
    {
        var t = EffectMath.Clamp01(value);
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    /// <summary>三次缓出减速曲线（CubicOut / 1 - (1 - t)^3）。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CubicOut(double value)
    {
        var inv = 1 - EffectMath.Clamp01(value);
        return 1 - inv * inv * inv;
    }
}
