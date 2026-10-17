using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// 统一的色彩计算工具：提供 Rec.709 亮度计算、线性插值、24位RGB编解码、对比度保障与发光色调增强

namespace AvaloniaSilkEffects;

/// <summary>
/// 特效管线通用的色彩数学与色彩空间工具。
/// </summary>
public static class ColorUtils
{
    public const float Rec709R = 0.2126f;
    public const float Rec709G = 0.7152f;
    public const float Rec709B = 0.0722f;

    /// <summary>
    /// 计算 0..1 浮点 RGB 的 Rec.709 相对亮度 (0..1)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Luminance(float r, float g, float b) =>
        Rec709R * r + Rec709G * g + Rec709B * b;

    /// <summary>
    /// 计算 0..255 整数 RGB 的 Rec.709 亮度 (0..255)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Luminance255(int r, int g, int b) =>
        r * 0.2126 + g * 0.7152 + b * 0.0722;

    /// <summary>
    /// 对两个 RGB 向量进行线性混合。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Mix(Vector3 a, Vector3 b, float t) =>
        a + (b - a) * Math.Clamp(t, 0f, 1f);

    /// <summary>
    /// 将通道浮点数按 8 位整型四舍五入并量化回 0..1。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuantizeChannel(float value) =>
        MathF.Round(Math.Clamp(value, 0f, 1f) * 255f) / 255f;

    /// <summary>
    /// 缩放到最高通道亮度（保留色相并剔除暗度；过暗时回退到 fallback）。
    /// </summary>
    public static Vector3 GlowOf(Vector3 rgb, Vector3 fallback, float minPeak = 0.04f)
    {
        var peak = MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));
        return peak < minPeak ? fallback : rgb / peak;
    }

    /// <summary>
    /// 将 24 位 0xRRGGBB 整数打包。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint PackRgb(int r, int g, int b) =>
        (uint)((Math.Clamp(r, 0, 255) << 16) | (Math.Clamp(g, 0, 255) << 8) | Math.Clamp(b, 0, 255));

    /// <summary>
    /// 解包 24 位 0xRRGGBB 为 (R, G, B) 整数。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int R, int G, int B) UnpackRgb(uint rgb) =>
        ((int)(rgb >> 16) & 0xFF, (int)(rgb >> 8) & 0xFF, (int)rgb & 0xFF);
}
