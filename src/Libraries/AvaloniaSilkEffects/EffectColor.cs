using Avalonia.Media;
using System.Numerics;

namespace AvaloniaSilkEffects;

public readonly record struct EffectColor(float R, float G, float B, float A = 1)
{
    public static EffectColor Transparent => new(0, 0, 0, 0);
    public static EffectColor White => new(1, 1, 1, 1);

    public static EffectColor FromAvalonia(Color color) => new(
        color.R / 255f,
        color.G / 255f,
        color.B / 255f,
        color.A / 255f);

    public EffectColor Premultiplied() => new(R * A, G * A, B * A, A);

    /// <summary>
    /// 将颜色打包为 24 位 RGB 整数（0xRRGGBB）。
    /// </summary>
    public uint ToRgb() =>
        (uint)(Math.Clamp((int)MathF.Round(R * 255), 0, 255) << 16
            | Math.Clamp((int)MathF.Round(G * 255), 0, 255) << 8
            | Math.Clamp((int)MathF.Round(B * 255), 0, 255));

    /// <summary>
    /// 从 24 位 RGB 整数（0xRRGGBB）创建 EffectColor。
    /// </summary>
    public static EffectColor FromRgb(uint rgb, float alpha = 1) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    internal Vector4 ToVector4() => new(R, G, B, A);
}

/// <summary>
/// 主题关键词与高亮色彩映射（Folia theme.wordColors）。
/// </summary>
public record EffectWordColor(string Word, EffectColor Color);

/// <summary>
/// 特效主题的全局动效强度等级（Folia theme.animationIntensity）。
/// </summary>
public enum EffectAnimationIntensity
{
    Calm,
    Normal,
    Chaotic,
}

/// <summary>
/// 特效主题通用动效强度换算扩展。
/// </summary>
public static class EffectAnimationIntensityExtensions
{
    public static double ToMotionScale(this EffectAnimationIntensity intensity) => intensity switch
    {
        EffectAnimationIntensity.Calm => 0.65,
        EffectAnimationIntensity.Chaotic => 1.35,
        _ => 1.0,
    };
}
