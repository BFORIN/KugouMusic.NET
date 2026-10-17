using AvaloniaSilkEffects.Lyrics.Segmentation;

// Lumiere 歌词单词切分：复用统一的歌词词库分词引擎

namespace AvaloniaSilkEffects.Lumiere.Text;

/// <summary>A word of a line: grapheme range [Start, End); whitespace, punctuation and symbols are blank.</summary>
public readonly record struct LumiereWordSpan(string Text, int Start, int End, bool Blank);

/// <summary>
/// Folia text/wordStyle.ts segmentWords: 统一通过 LyricTokenizer 调用中日文专业词库分词。
/// </summary>
public static class LumiereWords
{
    public static IReadOnlyList<LumiereWordSpan> Segment(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var tokens = LyricTokenizer.Tokenize(text);
        return [.. tokens.Select(token => new LumiereWordSpan(token.Text, token.Start, token.End, !token.IsWord))];
    }
}
