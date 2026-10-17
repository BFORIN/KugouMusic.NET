using System;
using System.Runtime.CompilerServices;

// 统一的通用数学工具：提供 FNV-1a 哈希、黄金比例混淆散列、插值及截断函数

namespace AvaloniaSilkEffects;

/// <summary>
/// 特效管线通用的基础数学与散列工具。
/// </summary>
public static class EffectMath
{
    public const uint FnvOffsetBasis = 2166136261u;
    public const uint FnvPrime = 16777619u;
    public const uint GoldenRatioKnuth = 2654435761u;
    public const double GoldenRatioFraction = 0.6180339887498949;

    /// <summary>
    /// Knuth 乘法黄金比例散列取 0..1 归一化值。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double KnuthHash01(uint value) =>
        unchecked(value * GoldenRatioKnuth) / 4294967296d;

    /// <summary>
    /// 对字符串计算 32 位 FNV-1a 哈希值（匹配 JavaScript charCodeAt 遍历行为）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Hash(value.AsSpan());
    }

    /// <summary>
    /// 对字符切片计算 32 位 FNV-1a 哈希值。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(ReadOnlySpan<char> value)
    {
        var hash = FnvOffsetBasis;
        foreach (var c in value)
        {
            hash ^= c;
            hash = unchecked(hash * FnvPrime);
        }
        return hash;
    }

    /// <summary>
    /// 基于 Knuth 乘法黄金比例常数的 32 位散列混淆。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mix(uint seed, uint salt) => unchecked((seed ^ salt) * GoldenRatioKnuth);

    /// <summary>
    /// 基于 Knuth 乘法黄金比例常数的 64 位种子散列混淆（兼容 JS 32 位溢出）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mix(long seed, long salt) => unchecked(((uint)seed ^ (uint)salt) * GoldenRatioKnuth);

    /// <summary>
    /// 基于种子与元素序号生成确定的 0..1 浮点抖动散列值。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Hash01(uint seed, int index, uint salt) =>
        Mix(unchecked(seed + (uint)((index + 1) * 97)), salt) / 4294967296d;

    /// <summary>
    /// 基于 long 种子与序号生成确定的 0..1 浮点抖动散列值。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Hash01(long seed, long index, long salt) =>
        Mix(seed + unchecked((int)((index + 1) * 97)), salt) / 4294967296d;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double from, double to, double amount) => from + (to - from) * amount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    /// <summary>
    /// 标准正余数取模（始终返回 [0, modulo) 范围内的非负余数）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Mod(int value, int modulo) => (value % modulo + modulo) % modulo;

    /// <summary>
    /// 浮点数正余数取模（始终返回 [0, modulo) 范围内的非负余数）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Mod(double value, double modulo) => (value % modulo + modulo) % modulo;
}
