using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AvaloniaSilkEffects.Sonnet;

// 统一的歌词词库分词服务：集成结巴词典 DAG、HMM 未登录词模型与 TinySegmenter 日文模型的高精度分词引擎

namespace AvaloniaSilkEffects.Lyrics.Segmentation;

public enum LyricLanguage
{
    Auto,
    Chinese,
    Japanese,
    English,
}

public readonly record struct LyricTextElement(string Text, int Start, int End);

public sealed record LyricToken(
    int Start,
    int End,
    string Text,
    LyricLanguage Language,
    LyricLexicalKind Kind,
    bool IsWord,
    bool IsContent);

/// <summary>
/// 歌词通用词库分词器与语种探测器（内置结巴词典、HMM 未登录词识别与日文统计模型）。
/// </summary>
public static class LyricTokenizer
{
    /// <summary>
    /// 将文本切分为带有字符偏移范围的字形元素数组。
    /// </summary>
    public static LyricTextElement[] Elements(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var result = new List<LyricTextElement>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var value = enumerator.GetTextElement();
            result.Add(new LyricTextElement(value, enumerator.ElementIndex, enumerator.ElementIndex + value.Length));
        }
        return [.. result];
    }

    /// <summary>
    /// 根据歌词文本中的字符集统计探测主导语言（日文假名优先判定为日文，汉字判定为中文）。
    /// </summary>
    public static LyricLanguage DetectLanguage(IEnumerable<string> lines)
    {
        var japanese = 0;
        var chinese = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            foreach (var rune in line.EnumerateRunes())
            {
                if (EffectTextMetrics.IsKana(rune)) japanese++;
                else if (EffectTextMetrics.IsHan(rune)) chinese++;
            }
        }
        return japanese > chinese ? LyricLanguage.Japanese : LyricLanguage.Chinese;
    }

    /// <summary>
    /// 使用中日文专业词库与语言模型对整行歌词文本执行分词与词性标注。
    /// </summary>
    public static IReadOnlyList<LyricToken> Tokenize(string text, LyricLanguage languageHint = LyricLanguage.Auto)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var elements = SonnetTokenizer.Elements(text);
        var sonnetLang = languageHint switch
        {
            LyricLanguage.Chinese => SonnetLanguage.Chinese,
            LyricLanguage.Japanese => SonnetLanguage.Japanese,
            LyricLanguage.English => SonnetLanguage.English,
            _ => SonnetTokenizer.SongLanguage([text]),
        };

        var tokens = SonnetTokenizer.Tokenize(text, elements, sonnetLang);
        var result = new List<LyricToken>(tokens.Count);
        foreach (var token in tokens)
        {
            var lang = token.Word.Language switch
            {
                SonnetLanguage.Japanese => LyricLanguage.Japanese,
                SonnetLanguage.English => LyricLanguage.English,
                _ => LyricLanguage.Chinese,
            };
            var kind = (LyricLexicalKind)(int)token.Word.Kind;
            result.Add(new LyricToken(
                token.Start,
                token.End,
                token.Word.Text,
                lang,
                kind,
                token.IsWord,
                token.IsContent));
        }
        return result;
    }
}
