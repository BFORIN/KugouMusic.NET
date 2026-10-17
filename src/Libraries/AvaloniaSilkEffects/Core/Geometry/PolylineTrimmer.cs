using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

// 统一的折线截断与描边显露工具：计算折线累计长度并在动画进度下采样插值截断点

namespace AvaloniaSilkEffects;

/// <summary>
/// 2D 折线累计长度计算与进度截断服务。
/// </summary>
public static class PolylineTrimmer
{
    /// <summary>
    /// 计算 2D 顶点序列的累计弧长数组，首元素为 0。
    /// </summary>
    public static float[] CalculateDistances(IReadOnlyList<Vector2> points)
    {
        if (points.Count == 0) return [];
        var distances = new float[points.Count];
        for (var i = 1; i < distances.Length; i++)
            distances[i] = distances[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        return distances;
    }

    /// <summary>
    /// 根据目标距离在累计弧长数组中查找截断线段索引与末端插值点。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int EndIndex, Vector2 Position) SampleTrimmedEnd(
        IReadOnlyList<Vector2> points,
        float[] distances,
        float targetDistance)
    {
        if (points.Count == 0) return (0, Vector2.Zero);
        if (points.Count == 1 || targetDistance <= 0f) return (1, points[0]);
        if (targetDistance >= distances[^1]) return (points.Count - 1, points[^1]);

        var end = 1;
        while (end < distances.Length - 1 && distances[end] < targetDistance)
            end++;

        var segmentLength = Math.Max(0.000001f, distances[end] - distances[end - 1]);
        var fraction = Math.Clamp((targetDistance - distances[end - 1]) / segmentLength, 0f, 1f);
        var position = Vector2.Lerp(points[end - 1], points[end], fraction);
        return (end, position);
    }
}
