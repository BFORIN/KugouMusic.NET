using System.Globalization;
using System.Text;

namespace AvaloniaSilkEffects.Sonnet;

internal readonly record struct SonnetTextElement(string Text, int Start, int End);
internal sealed record SonnetToken(int Start, int End, SonnetLexicalWord Word)
{
    public bool IsWord => Word.Kind is not (SonnetLexicalKind.Whitespace or SonnetLexicalKind.Punctuation or SonnetLexicalKind.Symbol);
    public bool IsContent => IsWord && Word.Kind != SonnetLexicalKind.Function;
}

internal static class SonnetTokenizer
{
    public static SonnetTextElement[] Elements(string text)
    {
        var result = new List<SonnetTextElement>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var value = enumerator.GetTextElement();
            result.Add(new SonnetTextElement(value, enumerator.ElementIndex, enumerator.ElementIndex + value.Length));
        }
        return result.ToArray();
    }

    public static SonnetLanguage SongLanguage(IReadOnlyList<SonnetLine> lines)
    {
        var japanese = 0;
        var chinese = 0;
        foreach (var line in lines)
        {
            if (line.LanguageHint == SonnetLanguage.Japanese) japanese++;
            else if (line.LanguageHint == SonnetLanguage.Chinese) chinese++;
            else if (line.LanguageHint == SonnetLanguage.Auto)
            {
                if (line.FullText.EnumerateRunes().Any(IsKana)) japanese++;
                else if (line.FullText.EnumerateRunes().Any(IsHan)) chinese++;
            }
        }
        return japanese > chinese ? SonnetLanguage.Japanese : SonnetLanguage.Chinese;
    }

    public static SonnetLanguage SongLanguage(IEnumerable<string> lines)
    {
        var japanese = 0;
        var chinese = 0;
        foreach (var line in lines)
        {
            if (line.EnumerateRunes().Any(IsKana)) japanese++;
            else if (line.EnumerateRunes().Any(IsHan)) chinese++;
        }
        return japanese > chinese ? SonnetLanguage.Japanese : SonnetLanguage.Chinese;
    }

    public static List<SonnetToken> Tokenize(SonnetLine line, SonnetTextElement[] elements, SonnetLanguage songLanguage) =>
        Tokenize(line.FullText, elements, songLanguage, line.LanguageHint);

    public static List<SonnetToken> Tokenize(string fullText, SonnetTextElement[] elements, SonnetLanguage songLanguage, SonnetLanguage languageHint = SonnetLanguage.Auto)
    {
        var language = languageHint != SonnetLanguage.Auto ? languageHint
            : elements.Any(item => IsKana(FirstRune(item.Text))) ? SonnetLanguage.Japanese : songLanguage;
        var japanese = language == SonnetLanguage.Japanese
            ? SonnetJapaneseTokenizer.Instance.Boundaries(elements) : null;
        var output = new List<SonnetToken>();
        for (var index = 0; index < elements.Length;)
        {
            var rune = FirstRune(elements[index].Text);
            var start = index++;
            if (IsKeycap(elements[start].Text))
            {
                output.Add(Token(fullText, elements, start, index, language, SonnetLexicalKind.Symbol, SonnetLexicalSource.Scanner));
                continue;
            }
            if (IsCjk(rune))
            {
                while (index < elements.Length && IsCjk(FirstRune(elements[index].Text))) index++;
                if (japanese is not null)
                {
                    var tokenStart = start;
                    for (var finish = start + 1; finish <= index; finish++)
                        if (finish == index || japanese[finish])
                        {
                            output.Add(Token(fullText, elements, tokenStart, finish, SonnetLanguage.Japanese,
                                SonnetLexicalKind.Other, SonnetLexicalSource.JapaneseModel));
                            tokenStart = finish;
                        }
                }
                else if (language == SonnetLanguage.English)
                    output.Add(Token(fullText, elements, start, index, language,
                        SonnetLexicalKind.Other, SonnetLexicalSource.Scanner));
                else SonnetChineseTokenizer.Append(fullText, elements, start, index, output);
                continue;
            }
            if (Rune.IsWhiteSpace(rune))
            {
                while (index < elements.Length && Rune.IsWhiteSpace(FirstRune(elements[index].Text))) index++;
                output.Add(Token(fullText, elements, start, index, language, SonnetLexicalKind.Whitespace, SonnetLexicalSource.Scanner));
                continue;
            }
            if (Rune.IsLetterOrDigit(rune))
            {
                while (index < elements.Length)
                {
                    var current = FirstRune(elements[index].Text);
                    if (IsLatinWordElement(current) && !IsKeycap(elements[index].Text)) { index++; continue; }
                    if (index + 1 < elements.Length && !IsKeycap(elements[index + 1].Text) && IsJoiner(elements[index].Text,
                        FirstRune(elements[index - 1].Text), FirstRune(elements[index + 1].Text)))
                    { index += 2; continue; }
                    break;
                }
                output.Add(Token(fullText, elements, start, index, SonnetLanguage.English, SonnetLexicalKind.Other, SonnetLexicalSource.Scanner));
                continue;
            }
            var kind = Rune.GetUnicodeCategory(rune) is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation
                ? SonnetLexicalKind.Punctuation : SonnetLexicalKind.Symbol;
            output.Add(Token(fullText, elements, start, index, language, kind, SonnetLexicalSource.Scanner));
        }
        return output;
    }

    public static SonnetToken Token(string text, SonnetTextElement[] elements, int start, int end,
        SonnetLanguage language, SonnetLexicalKind kind, SonnetLexicalSource source)
    {
        var from = elements[start].Start;
        var to = elements[end - 1].End;
        var word = text[from..to];
        if (kind is <= SonnetLexicalKind.Negation)
            kind = SonnetFunctionWords.Classify(word, language, kind);
        return new SonnetToken(start, end, new SonnetLexicalWord(word, from, to, language, kind, source));
    }

    public static Rune FirstRune(string text) => EffectTextMetrics.FirstRune(text);
    public static bool IsHan(Rune rune) => EffectTextMetrics.IsHan(rune);
    public static bool IsKana(Rune rune) => EffectTextMetrics.IsKana(rune);
    public static bool IsCjk(Rune rune) => EffectTextMetrics.IsCjk(rune);
    public static bool IsLatinWordElement(Rune rune) => EffectTextMetrics.IsLatinWordElement(rune);
    private static bool IsKeycap(string text) => text.Contains('\u20E3');
    private static bool IsJoiner(string text, Rune before, Rune after) =>
        IsLatinWordElement(before) && IsLatinWordElement(after) &&
        (text is "'" or "’" or "-" or "‐" or "‑" || (text is "." or "．") && Rune.IsDigit(before) && Rune.IsDigit(after));
}

internal enum SonnetAttachment { None, Previous, Next }

internal static class SonnetFunctionWords
{
    private static readonly HashSet<string> ZhPrevious = Set("的 地 得 了 着 著 过 過 吗 嗎 呢 吧 啊 呀 哦 嘛 啦 呗 唄 罢 罷");
    private static readonly HashSet<string> ZhNext = Set("把 被 在 从 從 向 对 對 于 於 与 與 和 跟 为 為 以 及 而 且 但 若 因 因为 因為 所以");
    private static readonly HashSet<string> JaPrevious = Set("は が を に へ で と も の や ね よ か ぞ さ な から まで より だけ ほど など って です ます でした ました だ た て ば");
    private static readonly HashSet<string> EnNext = Set("a an the of to in on at for from with by and or but as if than that this these those into onto upon through over under about because while");
    private static readonly HashSet<string> ZhNegation = Set("不 没 沒 无 無 别 別 未 非 莫 勿 没有 沒有 不是 不再 不曾 不必 不用 不要 不能 不会 不會 不肯 不愿 不願 不敢 无法 無法 从未 從未 从不 從不");
    private static readonly HashSet<string> JaNegation = Set("ない なく ぬ ず ません いいえ いや");
    private static readonly HashSet<string> EnNegation = Set("no not never neither nor don't don’t doesn't doesn’t didn't didn’t won't won’t can't can’t cannot isn't isn’t aren't aren’t");
    private static HashSet<string> Set(string words) => new(words.Split(' '), StringComparer.OrdinalIgnoreCase);

    public static SonnetLexicalKind Classify(string text, SonnetLanguage language, SonnetLexicalKind fallback)
    {
        var negative = language switch { SonnetLanguage.Japanese => JaNegation, SonnetLanguage.English => EnNegation, _ => ZhNegation };
        if (negative.Contains(text)) return SonnetLexicalKind.Negation;
        return Attachment(text, language) != SonnetAttachment.None ? SonnetLexicalKind.Function : fallback;
    }

    public static SonnetAttachment Attachment(string text, SonnetLanguage language) => language switch
    {
        SonnetLanguage.Japanese => JaPrevious.Contains(text) ? SonnetAttachment.Previous : SonnetAttachment.None,
        SonnetLanguage.English => EnNext.Contains(text) ? SonnetAttachment.Next : SonnetAttachment.None,
        _ => ZhPrevious.Contains(text) ? SonnetAttachment.Previous : ZhNext.Contains(text) ? SonnetAttachment.Next : SonnetAttachment.None,
    };

    public static int Priority(SonnetLexicalKind kind) => kind switch
    {
        SonnetLexicalKind.Noun or SonnetLexicalKind.Verb or SonnetLexicalKind.Adjective or SonnetLexicalKind.Negation => 3,
        SonnetLexicalKind.Other => 2,
        SonnetLexicalKind.Adverb or SonnetLexicalKind.Pronoun => 1,
        _ => 0,
    };
}
