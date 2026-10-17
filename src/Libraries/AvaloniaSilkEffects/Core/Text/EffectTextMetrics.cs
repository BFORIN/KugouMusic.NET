using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SkiaSharp;

// 统一的 SkiaSharp 文本测量服务：字形包围盒度量、带 LRU 缓存的文本宽度测量与 Unicode 语义分类

namespace AvaloniaSilkEffects;

/// <summary>
/// 特效管线通用的文本排版度量与字符解析工具。
/// </summary>
public static class EffectTextMetrics
{
    private const string MetricsSampleString = "|ÉqÅM";
    private const int DefaultMeasureCacheLimit = 20000;

    private static readonly ConcurrentDictionary<(string Family, int Weight), (float Ascent, float Descent)> BoxMetrics = new();
    private static readonly Dictionary<(string Family, int Weight, double FontSize, string Text), double> MeasureCache = [];
    private static readonly Queue<(string Family, int Weight, double FontSize, string Text)> MeasureOrder = new();
    private static readonly object MeasureLock = new();

    /// <summary>
    /// 将整数权重转换为 SKFontStyle。
    /// </summary>
    public static SKFontStyle ResolveFontStyle(int weight) =>
        EffectTypefaceResolver.ResolveFontStyle(weight);

    /// <summary>
    /// 获取指定字体族名、字重及采样文本下的最佳 SKTypeface。
    /// </summary>
    public static SKTypeface GetTypeface(string family, int weight, string? sampleText = null) =>
        EffectTypefaceResolver.GetTypeface(family, weight, sampleText);

    /// <summary>
    /// 获取指定字体在 1 像素标准尺寸下的 (Ascent, Descent) 垂直包围盒度量。
    /// </summary>
    public static (float Ascent, float Descent) GetBoxMetrics(string family, int weight)
    {
        return BoxMetrics.GetOrAdd((family, weight), static key =>
        {
            using var font = new SKFont(GetTypeface(key.Family, key.Weight, null), 100);
            font.MeasureText(MetricsSampleString, out var bounds);
            var ascent = -bounds.Top / 100f;
            var descent = bounds.Bottom / 100f;
            if (ascent + descent <= 0) return (1f, 0f);
            return (ascent, descent);
        });
    }

    /// <summary>
    /// 测量指定文本在给定字体族名、字重和字号下的宽度，结果会被线程安全地缓存在内存中。
    /// </summary>
    public static double Measure(string text, string family, int weight, double fontSize)
    {
        if (string.IsNullOrEmpty(text) || fontSize <= 0)
            return 0;

        var key = (family, weight, fontSize, text);
        lock (MeasureLock)
        {
            if (MeasureCache.TryGetValue(key, out var cached))
                return cached;
        }

        double measured;
        try
        {
            using var font = new SKFont(GetTypeface(family, weight, text), (float)fontSize) { Subpixel = true };
            measured = font.MeasureText(text);
        }
        catch
        {
            measured = text.Length * fontSize * 0.6;
        }

        var width = Math.Max(fontSize * 0.08, measured);
        lock (MeasureLock)
        {
            if (MeasureCache.Count >= DefaultMeasureCacheLimit && MeasureOrder.TryDequeue(out var oldest))
                MeasureCache.Remove(oldest);
            if (MeasureCache.TryAdd(key, width))
                MeasureOrder.Enqueue(key);
        }

        return width;
    }

    /// <summary>
    /// 将文本切分为 Unicode 字形簇（Grapheme Clusters）列表。
    /// </summary>
    public static IReadOnlyList<string> SplitGraphemes(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var output = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            output.Add((string)enumerator.Current);
        return output;
    }

    /// <summary>
    /// 测量单个字形簇宽度；空白字符返回字号的 30%。
    /// </summary>
    public static double MeasureGrapheme(string character, string family, int weight, double fontSize)
    {
        return character.Trim().Length == 0
            ? fontSize * 0.3
            : Measure(character, family, weight, fontSize);
    }

    /// <summary>
    /// 获取文本的首个 Rune 标量值，空串返回 ReplacementChar。
    /// </summary>
    public static Rune FirstRune(string text) =>
        Rune.TryGetRuneAt(text, 0, out var rune) ? rune : Rune.ReplacementChar;

    /// <summary>
    /// 判断 Rune 是否为汉字（包括 CJK 统一表意文字扩充及兼容区）。
    /// </summary>
    public static bool IsHan(Rune rune) =>
        rune.Value is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF
        or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x323AF;

    /// <summary>
    /// 判断 Rune 是否为日文假名（平假名、片假名、片假名语音扩充等）。
    /// </summary>
    public static bool IsKana(Rune rune) =>
        rune.Value is >= 0x3041 and <= 0x3096 or >= 0x309D and <= 0x309F
        or >= 0x30A1 and <= 0x30FA or >= 0x30FC and <= 0x30FF or >= 0x31F0 and <= 0x31FF or >= 0xFF66 and <= 0xFF9F;

    /// <summary>
    /// 判断 Rune 是否属于 CJK 字符集（包括汉字、假名及常用叠字符号）。
    /// </summary>
    public static bool IsCjk(Rune rune) =>
        IsHan(rune) || IsKana(rune) || rune.Value is 0x3005 or 0x3006 or 0x303B;

    /// <summary>
    /// 判断 Rune 是否为拉丁/数字单词元素。
    /// </summary>
    public static bool IsLatinWordElement(Rune rune) =>
        Rune.IsLetterOrDigit(rune) && !IsCjk(rune);

    /// <summary>
    /// 判断字符串是否全部由汉字组成。
    /// </summary>
    public static bool IsHanOnly(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var rune in text.EnumerateRunes())
            if (!IsHan(rune)) return false;
        return true;
    }
}
