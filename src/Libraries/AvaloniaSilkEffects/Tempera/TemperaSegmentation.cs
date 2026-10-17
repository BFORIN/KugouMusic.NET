using AvaloniaSilkEffects.Sonnet;

namespace AvaloniaSilkEffects.Tempera;

/// <summary>One word-granularity piece of a line: code-unit offset, length, and whether it is a word (vs space / punctuation).</summary>
public readonly record struct TemperaWordPiece(int Index, int Length, bool IsWordLike);

/// <summary>
/// Folia utils/lyrics/wordSegmentation.ts segmentLyricWords via icu-folia: whitespace runs and punctuation marks are pieces of
/// their own, Chinese runs are cut by jieba and then take their trailing particles back (故事/的 → 故事的, but 天/和/地 stays),
/// Japanese runs are cut into bunsetsu. The Sonnet tokenizer already carries the jieba / bunsetsu models.
/// </summary>
public static class TemperaSegmentation
{
    private static readonly HashSet<string> TrailingParticles =
    [
        "的", "地", "得", "了", "着", "著", "过", "過", "们", "們",
        "吗", "嗎", "呢", "吧", "啊", "呀", "啦", "嘛", "哦", "喔", "么", "麼", "哇", "呐", "咯",
        "嘅", "咗", "喇", "囉", "咩", "㗎", "噃", "喎", "啫", "吖", "嘞",
    ];

    private static readonly HashSet<string> NonHosts =
    [
        "和", "与", "與", "跟", "同", "及", "或", "而", "在", "从", "從", "向", "对", "對", "把", "被", "给", "給",
        "让", "讓", "为", "為", "也", "都", "就", "又", "再", "还", "還", "才", "很", "太", "最", "更", "不", "没", "沒",
        "别", "別", "未", "是", "係", "唔", "冇", "啲",
    ];

    public static IReadOnlyList<TemperaWordPiece> Segment(string text, SonnetLanguage songLanguage = SonnetLanguage.Auto)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var line = new SonnetLine(text, 0, 0, []);
        var elements = SonnetTokenizer.Elements(text);
        var language = songLanguage == SonnetLanguage.Auto ? SonnetTokenizer.SongLanguage([line]) : songLanguage;
        var tokens = SonnetTokenizer.Tokenize(line, elements, language);
        var pieces = new List<(int Start, int End, string Text, bool Word, bool Chinese)>(tokens.Count);
        foreach (var token in tokens)
        {
            var word = token.Word;
            var chinese = word.Language == SonnetLanguage.Chinese
                && word.Source is SonnetLexicalSource.Dictionary or SonnetLexicalSource.UnknownWord && IsHanOnly(word.Text);
            var previous = pieces.Count > 0 ? pieces[^1] : default;
            if (chinese && pieces.Count > 0 && previous.Chinese && previous.End == word.StartOffset
                && TrailingParticles.Contains(word.Text) && !NonHosts.Contains(previous.Text))
            {
                pieces[^1] = previous with { End = word.EndOffset, Text = previous.Text + word.Text };
                continue;
            }
            pieces.Add((word.StartOffset, word.EndOffset, word.Text, token.IsWord, chinese));
        }
        return [.. pieces.Select(piece => new TemperaWordPiece(piece.Start, piece.End - piece.Start, piece.Word))];
    }

    private static bool IsHanOnly(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            if (!SonnetTokenizer.IsHan(rune)) return false;
        return text.Length > 0;
    }
}
