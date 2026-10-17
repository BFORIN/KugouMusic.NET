using System;
using System.Collections.Generic;

// 统一的歌词语义分段抽象与分词聚合工具

namespace AvaloniaSilkEffects.Lyrics.Segmentation;

public enum LyricLexicalKind
{
    Other,
    Noun,
    Verb,
    Adjective,
    Adverb,
    Pronoun,
    Function,
    Negation,
    Whitespace,
    Punctuation,
    Symbol,
}

public sealed record LyricLexicalWord(
    string Text,
    int StartOffset,
    int EndOffset,
    LyricLanguage Language,
    LyricLexicalKind Kind);

/// <summary>
/// 通用歌词分词与语义分段辅助工具。
/// </summary>
public static class LyricSegmentation
{
    /// <summary>
    /// 判断词性是否属于具有实际语义的名词、动词或形容词等实词。
    /// </summary>
    public static bool IsContentWord(LyricLexicalKind kind) =>
        kind is not (LyricLexicalKind.Function or LyricLexicalKind.Whitespace or LyricLexicalKind.Punctuation or LyricLexicalKind.Symbol);
}
