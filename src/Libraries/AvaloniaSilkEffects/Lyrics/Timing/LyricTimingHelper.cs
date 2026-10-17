using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// 统一的歌词时序计算工具：提供渲染结束时间推算（renderHints）、字形时序平分与播放高亮进度采样

namespace AvaloniaSilkEffects.Lyrics.Timing;

/// <summary>
/// 歌词播放时钟与字形时序计算工具。
/// </summary>
public static class LyricTimingHelper
{
    /// <summary>
    /// Folia renderHints: 计算视觉呈现的安全结束时间（保证行末退场动画与微小行时长安全）。
    /// </summary>
    public static double CalculateRenderEndTime(double startTime, double endTime, double? lastWordEndTime = null)
    {
        var raw = Math.Max(endTime - startTime, 0);
        if (raw < 0.10) return Math.Max(endTime, startTime + 0.067);

        var lastEnd = lastWordEndTime ?? endTime;
        if (raw < 0.18)
        {
            var enter = Math.Clamp(raw * 0.45, 0.045, 0.06);
            var exitFast = Math.Clamp(raw * 0.22, 0.03, 0.04);
            var passFast = Math.Max(lastEnd, startTime) + 0.03;
            return Math.Max(endTime, Math.Max(startTime + enter + 0.01, Math.Max(passFast, endTime - exitFast)) + exitFast);
        }

        var exit = Math.Min(0.32, Math.Max(0.18, Math.Max(raw, 0.12) * 0.18));
        var pass = Math.Max(lastEnd, startTime) + 0.06;
        return Math.Max(endTime, Math.Max(pass, endTime - exit) + exit);
    }

    /// <summary>
    /// 将起始和结束时间均分给若干个字形。
    /// </summary>
    public static (double Start, double End)[] DistributeEvenly(int count, double start, double end)
    {
        if (count <= 0) return [];
        var unit = Math.Max(end - start, 0) / count;
        var result = new (double Start, double End)[count];
        for (var i = 0; i < count; i++)
            result[i] = (start + unit * i, i == count - 1 ? end : start + unit * (i + 1));
        return result;
    }

    /// <summary>
    /// 获取当前播放时间在起止区间内的归一化进度 (0..1)。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetProgress(double time, double start, double end)
    {
        if (end <= start) return time >= start ? 1.0 : 0.0;
        return Math.Clamp((time - start) / (end - start), 0.0, 1.0);
    }
}
