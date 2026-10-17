using System;
using System.Collections.Concurrent;
using SkiaSharp;

// 统一的字体解析服务：跨平台中文字体 Fallback 解析与 SKTypeface 缓存

namespace AvaloniaSilkEffects;

/// <summary>
/// 跨平台中文字体解析器与缓存。
/// </summary>
public static class EffectTypefaceResolver
{
    private static readonly ConcurrentDictionary<(string Family, int Weight, string? SampleKey), SKTypeface> Typefaces = new();

    /// <summary>
    /// 将整数权重（100..1000）转换为标准 SKFontStyle。
    /// </summary>
    public static SKFontStyle ResolveFontStyle(int weight) =>
        new(Math.Clamp(weight, 100, 1000), (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

    /// <summary>
    /// 获取指定字体族名、字重及采样文本下的最佳 SKTypeface（自动进行跨平台中文匹配及 LRU 缓存）。
    /// </summary>
    public static SKTypeface GetTypeface(string family, int weight, string? sampleText = null)
    {
        string? sampleKey = null;
        if (!string.IsNullOrEmpty(sampleText))
        {
            foreach (var ch in sampleText)
            {
                if (ch > 127 && !char.IsWhiteSpace(ch))
                {
                    sampleKey = ch.ToString();
                    break;
                }
            }
        }

        return Typefaces.GetOrAdd(
            (family, weight, sampleKey),
            static k => EffectTextureCache.ResolveTypeface(k.Family, ResolveFontStyle(k.Weight), k.SampleKey));
    }
}
