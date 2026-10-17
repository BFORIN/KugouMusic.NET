using System.Globalization;

namespace AvaloniaSilkEffects.Sonnet;

public static class SonnetTypographyRoles
{
    public static bool IsEmphasis(SonnetSegmentRole role) =>
        role is SonnetSegmentRole.Hero or SonnetSegmentRole.SemiHero;

    public static int ResolveFontWeight(int? configuredFontWeight, SonnetSegmentRole role)
    {
        if (configuredFontWeight is not null)
        {
            var clamped = Math.Clamp(configuredFontWeight.Value, 100, 900);
            return (int)Math.Round(clamped / 10d, MidpointRounding.AwayFromZero) * 10;
        }

        if (IsEmphasis(role)) return 900;
        return role == SonnetSegmentRole.Decoration ? 300 : 700;
    }

    public static int VisibleLength(SonnetSemanticSegment segment) => segment.Graphemes.Count > 0
        ? segment.Graphemes.Count(item => !string.IsNullOrWhiteSpace(item.Text))
        : StringInfo.ParseCombiningCharacters(segment.Text.Trim()).Length;

    // Retained as a coarse public score. All selection uses the lexicographic
    // comparator: no amount of duration or repetition can outrank a word class.
    public static double HeroScore(SonnetSemanticSegment segment) => Emphasis(segment).Priority;

    public static bool CanEmphasize(SonnetSemanticSegment segment) =>
        segment.IsWordLike && VisibleLength(segment) > 0 && Emphasis(segment).Priority > 0;

    public static int CompareCandidates(SonnetSemanticSegment left, SonnetSemanticSegment right)
    {
        var a = Emphasis(left);
        var b = Emphasis(right);
        var comparison = a.Priority.CompareTo(b.Priority);
        if (comparison != 0) return comparison;
        comparison = a.ReliableDurationPerGrapheme.CompareTo(b.ReliableDurationPerGrapheme);
        return comparison != 0 ? comparison : a.Occurrences.CompareTo(b.Occurrences);
    }

    public static int FindHeroIndex(IReadOnlyList<SonnetSemanticSegment> segments)
    {
        var best = -1;
        for (var index = 0; index < segments.Count; index++)
            if (CanEmphasize(segments[index]) && (best < 0 || CompareCandidates(segments[index], segments[best]) > 0))
                best = index;
        return best;
    }

    public static IReadOnlyList<int> FindSemiHeroIndices(IReadOnlyList<SonnetSemanticSegment> segments, int heroIndex)
    {
        if (heroIndex < 0 || heroIndex >= segments.Count || !CanEmphasize(segments[heroIndex])) return [];
        var candidates = Enumerable.Range(0, segments.Count)
            .Where(index => index != heroIndex && CanEmphasize(segments[index]) && HasDisplayGap(segments, index, heroIndex))
            .ToList();
        candidates.Sort((left, right) =>
        {
            var comparison = CompareCandidates(segments[right], segments[left]);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
        var selected = new List<int>(2);
        foreach (var candidate in candidates)
        {
            if (selected.Any(index => !HasDisplayGap(segments, index, candidate))) continue;
            selected.Add(candidate);
            if (selected.Count == 2) break;
        }
        selected.Sort();
        return selected;
    }

    public static int FindSemiHeroIndex(IReadOnlyList<SonnetSemanticSegment> segments, int heroIndex) =>
        FindSemiHeroIndices(segments, heroIndex).FirstOrDefault(-1);

    internal static bool HasDisplayGap(IReadOnlyList<SonnetSemanticSegment> segments, int left, int right)
    {
        for (var index = Math.Min(left, right) + 1; index < Math.Max(left, right); index++)
            if (!string.IsNullOrWhiteSpace(segments[index].Text)) return true;
        return false;
    }

    private static SonnetEmphasis Emphasis(SonnetSemanticSegment segment)
    {
        if (segment.Emphasis is { } emphasis) return emphasis;
        // Compatibility for callers constructing semantic segments directly.
        // No dictionary load or guessed word-level time is needed here.
        if (!segment.IsWordLike || string.IsNullOrWhiteSpace(segment.Text)) return new SonnetEmphasis(0, 0, 0);
        var language = segment.Text.EnumerateRunes().Any(EffectTextMetrics.IsKana) ? SonnetLanguage.Japanese
            : segment.Text.EnumerateRunes().Any(EffectTextMetrics.IsHan) ? SonnetLanguage.Chinese : SonnetLanguage.English;
        var kind = SonnetFunctionWords.Classify(segment.Text.Trim(), language, SonnetLexicalKind.Other);
        return new SonnetEmphasis(SonnetFunctionWords.Priority(kind), 0, 1);
    }
}
