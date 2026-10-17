using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// 统一的歌词段落组织调度器：基于时间间隙中位数自适应切分段落、超长段落拆解与副歌/间奏分类

namespace AvaloniaSilkEffects.Lyrics;

public enum EffectParagraphBoundary
{
    SongStart,
    Metadata,
    TimeGap,
    LineCap,
    DurationCap,
}

public enum EffectParagraphKind
{
    Verse,
    Chorus,
    Break,
    Outro,
    Breath,
    Lift,
}

public sealed record EffectParagraphDraft<T>(IReadOnlyList<T> Lines, EffectParagraphBoundary Boundary);

/// <summary>
/// Folia 歌词结构调度核心：负责段落边界检测、时间间隙阈值计算与镜头段落分类。
/// </summary>
public static partial class LyricParagraphScheduler
{
    public const int DefaultMaxParagraphLines = 6;
    public const double DefaultMaxParagraphDuration = 18.0;

    [GeneratedRegex(@"(?i)chorus|hook|副歌|サビ", RegexOptions.Compiled)]
    private static partial Regex ChorusRegex();

    [GeneratedRegex(@"(?i)break|bridge|solo|outro|intro|间奏|間奏|桥段|前奏|尾奏", RegexOptions.Compiled)]
    private static partial Regex BreakRegex();

    [GeneratedRegex(@"[!！?？]", RegexOptions.Compiled)]
    private static partial Regex ExclamationRegex();

    /// <summary>
    /// 计算歌词行间隙的中位数，并自适应推导段落切分的时间阈值 (1.25s .. 3.5s)。
    /// </summary>
    public static double ResolveParagraphGapThreshold<T>(
        IReadOnlyList<T> lines,
        Func<T, double> getStartTime,
        Func<T, double> getEndTime)
    {
        if (lines.Count <= 1) return 2.0;

        var gaps = new List<double>(lines.Count);
        for (var i = 1; i < lines.Count; i++)
        {
            var prevEnd = getEndTime(lines[i - 1]);
            var currStart = getStartTime(lines[i]);
            var gap = currStart - Math.Min(prevEnd, currStart);
            if (gap > 0 && !double.IsNaN(gap))
                gaps.Add(gap);
        }

        if (gaps.Count == 0) return 2.0;
        gaps.Sort();

        var median = gaps.Count % 2 == 0
            ? (gaps[gaps.Count / 2 - 1] + gaps[gaps.Count / 2]) / 2.0
            : gaps[gaps.Count / 2];

        return Math.Clamp(median * 2.5, 1.25, 3.5);
    }

    /// <summary>
    /// 根据时间间隙与歌曲元数据变化切分初始歌词草稿段落，并自动对超长或超时段落进行最优切割。
    /// </summary>
    public static List<EffectParagraphDraft<T>> PartitionParagraphs<T>(
        IReadOnlyList<T> lines,
        double threshold,
        Func<T, double> getStartTime,
        Func<T, double> getEndTime,
        Func<T, T, bool> metadataChanged,
        int maxLines = DefaultMaxParagraphLines,
        double maxDuration = DefaultMaxParagraphDuration)
    {
        if (lines.Count == 0) return [];

        var drafts = new List<EffectParagraphDraft<T>>();
        var current = new List<T>();
        var boundary = EffectParagraphBoundary.SongStart;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var previous = i > 0 ? lines[i - 1] : default;
            var gap = previous is null ? 0 : getStartTime(line) - getEndTime(previous);

            EffectParagraphBoundary? cut = previous is not null && metadataChanged(previous, line)
                ? EffectParagraphBoundary.Metadata
                : previous is not null && gap >= threshold
                    ? EffectParagraphBoundary.TimeGap
                    : null;

            if (cut.HasValue && current.Count > 0)
            {
                drafts.AddRange(SplitOversized(current, boundary, getStartTime, getEndTime, maxLines, maxDuration));
                current = [];
                boundary = cut.Value;
            }

            current.Add(line);
        }

        if (current.Count > 0)
            drafts.AddRange(SplitOversized(current, boundary, getStartTime, getEndTime, maxLines, maxDuration));

        return drafts;
    }

    /// <summary>
    /// 递归或迭代拆分超过行数或时长上限的草稿段落。
    /// </summary>
    private static List<EffectParagraphDraft<T>> SplitOversized<T>(
        IReadOnlyList<T> lines,
        EffectParagraphBoundary boundary,
        Func<T, double> getStartTime,
        Func<T, double> getEndTime,
        int maxLines,
        double maxDuration)
    {
        var output = new List<EffectParagraphDraft<T>>();
        var remaining = lines;
        var currentBoundary = boundary;
        var guard = 0;

        while (remaining.Count > maxLines ||
               (remaining.Count > 1 && getEndTime(remaining[^1]) - getStartTime(remaining[0]) > maxDuration))
        {
            if (guard++ > 1000) break;

            var bestIndex = -1;
            var bestGap = double.NegativeInfinity;

            for (var offset = 0; offset < remaining.Count - 3; offset++)
            {
                var gap = getStartTime(remaining[offset + 2]) - getEndTime(remaining[offset + 1]);
                if (double.IsNaN(gap)) continue;
                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestIndex = offset + 2;
                }
            }

            var splitIndex = Math.Max(1, bestIndex >= 0 ? bestIndex : Math.Min(4, remaining.Count - 1));
            var head = remaining.Take(splitIndex).ToList();
            output.Add(new EffectParagraphDraft<T>(head, currentBoundary));

            remaining = remaining.Skip(splitIndex).ToList();
            currentBoundary = head.Count >= maxLines
                ? EffectParagraphBoundary.LineCap
                : EffectParagraphBoundary.DurationCap;
        }

        output.Add(new EffectParagraphDraft<T>(remaining, currentBoundary));
        return output;
    }

    /// <summary>
    /// 识别并分类段落性质（副歌、间奏、尾声、呼吸微动、升调高潮或普通主歌）。
    /// </summary>
    public static EffectParagraphKind ClassifyParagraph<T>(
        IReadOnlyList<T> lines,
        int paragraphIndex,
        int totalParagraphs,
        Func<T, string> getText,
        Func<T, double> getStartTime,
        Func<T, double> getEndTime,
        Func<T, string?> getSongPart,
        Func<T, bool> getIsChorus,
        Func<T, int> countWords)
    {
        if (lines.Count == 0) return EffectParagraphKind.Verse;

        if (lines.Any(item => getIsChorus(item) || ChorusRegex().IsMatch(getSongPart(item) ?? "")))
            return EffectParagraphKind.Chorus;

        if (lines.Any(item => BreakRegex().IsMatch(getSongPart(item) ?? "")))
            return EffectParagraphKind.Break;

        if (paragraphIndex == totalParagraphs - 1)
            return EffectParagraphKind.Outro;

        var duration = getEndTime(lines[^1]) - getStartTime(lines[0]);
        var segmentCount = lines.Sum(countWords);
        var punctuation = lines.Sum(line => ExclamationRegex().Matches(getText(line)).Count);

        if (duration <= 3.5 || segmentCount <= 3)
            return EffectParagraphKind.Breath;

        if (punctuation >= 2 || segmentCount / Math.Max(duration, 1.0) > 2.5)
            return EffectParagraphKind.Lift;

        return EffectParagraphKind.Verse;
    }
}
