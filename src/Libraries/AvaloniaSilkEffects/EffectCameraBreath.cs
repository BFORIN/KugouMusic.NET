using System;

// 统一的手持摄像机呼吸微动发生器：提供多频正弦叠加的手持微晃动与歌词展开后的平滑淡入加权

namespace AvaloniaSilkEffects;

public readonly record struct EffectCameraBreathFrame(double X, double Y, double Scale, double Rotation);

/// <summary>
/// Folia 手持相机微动（Camera Breath）与浮动生成器。
/// </summary>
public static class EffectCameraBreath
{
    public const double DefaultMaxOffset = 0.006;
    public const double DefaultMaxScale = 0.002;
    public const double DefaultMaxRotation = 0.0015;

    /// <summary>
    /// 计算指定绝对时间与相位下的手持摄像机呼吸微晃动。
    /// </summary>
    public static EffectCameraBreathFrame Calculate(
        double time,
        double phase = 0,
        double maxOffset = DefaultMaxOffset,
        double maxScale = DefaultMaxScale,
        double maxRotation = DefaultMaxRotation)
    {
        var tau = time * Math.PI * 2;
        return new EffectCameraBreathFrame(
            (Math.Sin(tau * 0.13 + phase) * 0.65 + Math.Sin(tau * 0.31 + phase * 1.7) * 0.35) * maxOffset,
            (Math.Cos(tau * 0.11 + phase * 2.3) * 0.65 + Math.Sin(tau * 0.29 + phase * 0.9) * 0.35) * maxOffset,
            Math.Sin(tau * 0.09 + phase * 1.3) * maxScale,
            Math.Sin(tau * 0.07 + phase * 2.9) * maxRotation);
    }

    /// <summary>
    /// 歌词展开完成后的呼吸权重平滑爬升，避免在中途突然介入晃动。
    /// </summary>
    public static double Weight(
        double time,
        double revealDoneTime,
        double rampDuration = 1.2,
        bool useCubic = false)
    {
        if (rampDuration <= 0)
            return time >= revealDoneTime ? 1 : 0;

        var progress = EffectMath.Clamp01((time - revealDoneTime) / rampDuration);
        return useCubic ? EffectEasing.EaseInOutCubic(progress) : EffectEasing.EaseInOut(progress);
    }
}
