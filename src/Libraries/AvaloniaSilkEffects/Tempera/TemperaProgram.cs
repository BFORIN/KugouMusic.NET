using System.Text.RegularExpressions;
using AvaloniaSilkEffects.Lumiere;

namespace AvaloniaSilkEffects.Tempera;

public enum TemperaParagraphKind
{
    Breath,
    Verse,
    Lift,
    Chorus,
    Break,
    Outro,
}

public enum TemperaParagraphBoundary
{
    SongStart,
    TimeGap,
    Metadata,
    DurationCap,
    LineCap,
}

/// <summary>Every transition is led by the large graphics or the camera; nothing dissolves or cuts hard.</summary>
public enum TemperaTransitionKind
{
    BlockWipe,
    CameraPan,
    ShapeCarry,
}

public enum TemperaDecorMotif
{
    Diamonds,
    HatchTwin,
    BandCross,
    PosterDiamond,
    Doodle,
}

/// <summary>One grapheme with the time it is sung.</summary>
public readonly record struct TemperaGrapheme(string Char, double StartTime, double EndTime);

public sealed class TemperaSegment
{
    public required string Text { get; set; }
    public int StartOffset { get; init; }
    public int EndOffset { get; set; }
    public double StartTime { get; init; }
    public double EndTime { get; set; }
    public required List<TemperaGrapheme> Graphemes { get; init; }
    public bool IsWordLike { get; init; }
}

public sealed record TemperaCompiledLine(int SourceIndex, LumiereLine Line, double RenderEndTime, IReadOnlyList<TemperaSegment> Segments);

public readonly record struct TemperaCameraKey(double X, double Y, double Zoom, double Rotation);

/// <summary>A stray glyph parked in the margins of a sparse composition (fractional viewport position).</summary>
public sealed record TemperaDecorFragment(string Char, double X, double Y, double Rotation, double Scale);

/// <summary>Oversized decorative word behind the composition; <see cref="Scale"/> multiplies the shot's base font size.</summary>
public sealed record TemperaDecorWatermark(string Text, double X, double Y, double Rotation, double Scale);

/// <summary>Screentone decor for one shot, resolved at compile time so every seek paints the identical frame.</summary>
public sealed record TemperaDecorSpec(TemperaDecorMotif Motif, double HatchAngle, int CrossCount, uint ScribbleSeed,
    IReadOnlyList<TemperaDecorFragment> Fragments, TemperaDecorWatermark? Watermark);

/// <summary>A half-open segment range of one compiled line.</summary>
public readonly record struct TemperaShotSlice(int LineIndex, int SegmentStart, int SegmentEnd);

public sealed class TemperaShot
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    /// <summary>Mutable: a paragraph's opening shot is pulled forward into the previous paragraph's transition.</summary>
    public double StartTime { get; set; }
    public double EndTime { get; init; }
    /// <summary>When this shot's last grapheme stops singing; the block / image stagger is paced against it.</summary>
    public double LyricEndTime { get; init; }
    public required IReadOnlyList<TemperaShotSlice> Slices { get; init; }
    /// <summary>A lyric-free shot carrying an instrumental gap between paragraphs.</summary>
    public bool IsBridge { get; init; }
    public TemperaCameraKey Camera { get; init; }
    public TemperaCameraKey CameraEnd { get; init; }
    /// <summary>Direction (radians) this shot's graphics travel in; consecutive shots only turn it a little.</summary>
    public double FlowAngle { get; init; }
    public required TemperaDecorSpec Decor { get; init; }
}

public sealed record TemperaTransition(TemperaTransitionKind Kind, double StartTime, double EndTime);

public sealed record TemperaParagraph(
    string Id,
    TemperaParagraphKind Kind,
    TemperaParagraphBoundary Boundary,
    double StartTime,
    double EndTime,
    IReadOnlyList<TemperaCompiledLine> Lines,
    IReadOnlyList<TemperaShot> Shots,
    TemperaTransition? TransitionOut);

public sealed record TemperaProgram(string Seed, double ParagraphGapThreshold, IReadOnlyList<TemperaParagraph> Paragraphs)
{
    public static TemperaProgram Empty { get; } = new("tempera", 1.25, []);

    public int ParagraphIndexAt(double time)
    {
        for (var index = Paragraphs.Count - 1; index >= 0; index--)
            if (time >= Paragraphs[index].StartTime) return index;
        return 0;
    }
}

/// <summary>Folia temperaProgram.ts: unified lyrics → a seek-safe, deterministic block-PV timeline.</summary>
public static partial class TemperaProgramCompiler
{
    private static readonly TemperaTransitionKind[] TransitionKinds =
        [TemperaTransitionKind.BlockWipe, TemperaTransitionKind.CameraPan, TemperaTransitionKind.ShapeCarry];
    private static readonly string[] TransitionNames = ["block-wipe", "camera-pan", "shape-carry"];
    private static readonly string[] MotifNames = ["diamonds", "hatch-twin", "band-cross", "poster-diamond", "doodle"];

    /// <summary>Gaps shorter than this are already covered by the paragraph transition itself.</summary>
    private const double BridgeMinGap = 1.2;
    private const double BridgeMaxLength = 5;
    /// <summary>How long the single-character glue may hold a chunk open (the usual close is 2.2 s).</summary>
    private const double SingleHanGlueMaxSeconds = 3.2;

    [GeneratedRegex(@"^[\s\p{P}\p{S}]+$")]
    private static partial Regex PunctuationOnly();
    [GeneratedRegex(@"^\s+$")]
    private static partial Regex WhitespaceOnly();
    [GeneratedRegex(@"chorus|副歌", RegexOptions.IgnoreCase)]
    private static partial Regex ChorusPart();
    [GeneratedRegex(@"bridge|break|間奏|ブリッジ", RegexOptions.IgnoreCase)]
    private static partial Regex BreakPart();
    [GeneratedRegex(@"[!?！？…]")]
    private static partial Regex Exclaim();

    public static double ResolveParagraphGapThreshold(IReadOnlyList<LumiereLine> lines) =>
        LyricParagraphScheduler.ResolveParagraphGapThreshold(lines, line => line.StartTime, line => line.RenderEndTime);

    private static bool MetadataChanged(LumiereLine previous, LumiereLine next) =>
        previous.BlockIndex is not null && next.BlockIndex is not null && previous.BlockIndex != next.BlockIndex
        || previous.SongPart is not null && next.SongPart is not null && previous.SongPart != next.SongPart;

    /// <summary>
    /// Word-level segments with parser-derived grapheme timing; sticky punctuation merges into the previous segment and a
    /// timing-less mark (zero length, pinned to the next word) is re-timed to the end of the word it now belongs to.
    /// </summary>
    public static List<TemperaSegment> BuildSegments(LumiereLine line)
    {
        if (string.IsNullOrEmpty(line.Text)) return [];
        var (graphemes, timings) = LumiereReveal.Timeline(line);
        var ranges = new (int Start, int End)[graphemes.Count];
        var cursor = 0;
        for (var index = 0; index < graphemes.Count; index++)
        {
            ranges[index] = (cursor, cursor + graphemes[index].Length);
            cursor = ranges[index].End;
        }
        var parts = TemperaSegmentation.Segment(line.Text);
        var segments = new List<TemperaSegment>(parts.Count);
        for (var index = 0; index < parts.Count; index++)
        {
            var startOffset = parts[index].Index;
            var endOffset = index + 1 < parts.Count ? parts[index + 1].Index : line.Text.Length;
            var glyphs = new List<TemperaGrapheme>();
            for (var g = 0; g < ranges.Length; g++)
                if (ranges[g].End > startOffset && ranges[g].Start < endOffset)
                    glyphs.Add(new TemperaGrapheme(graphemes[g], timings[g].Start, timings[g].End));
            segments.Add(new TemperaSegment
            {
                Text = line.Text[startOffset..endOffset],
                StartOffset = startOffset,
                EndOffset = endOffset,
                Graphemes = glyphs,
                StartTime = glyphs.Count > 0 ? glyphs[0].StartTime : line.StartTime,
                EndTime = glyphs.Count > 0 ? glyphs[^1].EndTime : line.EndTime,
                IsWordLike = parts[index].IsWordLike,
            });
        }

        var sticky = new List<TemperaSegment>(segments.Count);
        foreach (var segment in segments)
        {
            var previous = sticky.Count > 0 ? sticky[^1] : null;
            if (previous is not null && !segment.IsWordLike && !WhitespaceOnly().IsMatch(segment.Text))
            {
                previous.Text += segment.Text;
                previous.EndOffset = segment.EndOffset;
                var tail = previous.EndTime;
                var latest = previous.EndTime;
                foreach (var grapheme in segment.Graphemes)
                {
                    var merged = grapheme.EndTime > grapheme.StartTime ? grapheme : grapheme with { StartTime = tail, EndTime = tail };
                    previous.Graphemes.Add(merged);
                    latest = Math.Max(latest, merged.EndTime);
                }
                previous.EndTime = latest;
            }
            else
            {
                sticky.Add(new TemperaSegment
                {
                    Text = segment.Text,
                    StartOffset = segment.StartOffset,
                    EndOffset = segment.EndOffset,
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    Graphemes = [.. segment.Graphemes],
                    IsWordLike = segment.IsWordLike,
                });
            }
        }
        return sticky;
    }

    private static TemperaParagraphKind Classify(IReadOnlyList<TemperaCompiledLine> lines, int index, int total)
    {
        var kind = LyricParagraphScheduler.ClassifyParagraph(
            lines, index, total,
            line => line.Line.Text,
            line => line.Line.StartTime,
            line => line.RenderEndTime,
            line => line.Line.SongPart,
            line => line.Line.IsChorus,
            line => line.Segments.Count(s => s.IsWordLike));

        return (TemperaParagraphKind)(int)kind;
    }

    private sealed record ShotChunk(int LineIndex, int SegmentStart, int SegmentEnd, double StartTime, double EndTime, double LyricEndTime);

    private static bool IsRenderable(TemperaSegment segment) => segment.Text.Trim().Length > 0 && segment.Graphemes.Count > 0;

    private static bool IsSingleHanWord(TemperaSegment segment)
    {
        if (!segment.IsWordLike) return false;
        var runes = segment.Text.EnumerateRunes().ToArray();
        return runes.Length == 1 && EffectTextMetrics.IsHan(runes[0]);
    }

    private static List<ShotChunk> BuildShotChunks(List<TemperaCompiledLine> lines, string seed, int paragraphIndex, bool wholeLine)
    {
        var chunks = new List<ShotChunk>();
        foreach (var line in lines)
        {
            var usable = line.Segments.Select((segment, index) => (Segment: segment, Index: index))
                .Where(entry => IsRenderable(entry.Segment)).ToArray();
            if (usable.Length == 0) continue;
            if (wholeLine)
            {
                var first = usable[0];
                var last = usable[^1];
                var end = Math.Max(last.Segment.EndTime, first.Segment.StartTime + 0.2);
                chunks.Add(new ShotChunk(line.SourceIndex, first.Index, last.Index + 1, first.Segment.StartTime, end, end));
                continue;
            }
            var segmentStart = usable[0].Index;
            var startTime = usable[0].Segment.StartTime;
            var words = 0;
            for (var order = 0; order < usable.Length; order++)
            {
                var entry = usable[order];
                words++;
                var chunkSeed = TemperaRandom.Hash($"{seed}:{paragraphIndex}:{line.SourceIndex}:{chunks.Count}");
                var target = 2 + (int)Math.Floor(TemperaRandom.Hash01(chunkSeed, 1, 179) * 3);
                var spent = entry.Segment.EndTime - startTime;
                var isLast = order == usable.Length - 1;
                if (!isLast && words < target && spent < 2.2) continue;
                // Never cut between two touching single-character Chinese words (我/想/你), up to a hard cap.
                var hasNext = order + 1 < usable.Length;
                var next = hasNext ? usable[order + 1] : default;
                if (!isLast && spent < SingleHanGlueMaxSeconds && hasNext && IsSingleHanWord(entry.Segment)
                    && IsSingleHanWord(next.Segment) && next.Segment.StartOffset == entry.Segment.EndOffset) continue;
                var end = Math.Max(entry.Segment.EndTime, startTime + 0.2);
                chunks.Add(new ShotChunk(line.SourceIndex, segmentStart, entry.Index + 1, startTime, end, end));
                if (hasNext)
                {
                    segmentStart = next.Index;
                    startTime = next.Segment.StartTime;
                    words = 0;
                }
            }
        }
        // Tile the timeline so "the last shot whose start has passed" never lands in a hole; LyricEndTime stays untiled.
        return [.. chunks.Select((chunk, index) => chunk with
        {
            EndTime = Math.Max(chunk.StartTime + 0.2, index + 1 < chunks.Count ? chunks[index + 1].StartTime : chunk.EndTime),
        })];
    }

    private static (TemperaCameraKey Start, TemperaCameraKey End) BuildCameraKeys(string kind, uint seed, double flowAngle)
    {
        var jitterX = (TemperaRandom.Hash01(seed, 1, 11) - 0.5) * 0.02;
        var jitterY = (TemperaRandom.Hash01(seed, 2, 23) - 0.5) * 0.02;
        var jitterZoom = TemperaRandom.Hash01(seed, 3, 37) * 0.025;
        var jitterRotation = (TemperaRandom.Hash01(seed, 4, 51) - 0.5) * 0.012;
        var camera = TemperaShotProfiles.Resolve(kind).Camera;
        var travelX = Math.Cos(flowAngle) * camera.Travel;
        var travelY = Math.Sin(flowAngle) * camera.Travel;
        return (new TemperaCameraKey(-travelX / 2 + jitterX, -travelY / 2 + jitterY, camera.ZoomStart + jitterZoom, jitterRotation),
            new TemperaCameraKey(travelX / 2 + jitterX, travelY / 2 + jitterY, camera.ZoomEnd + jitterZoom, -jitterRotation));
    }

    private static List<TemperaDecorFragment> BuildDecorFragments(string pool, int count, uint seed)
    {
        var chars = EnumerateCodePoints(pool).Where(text => text.Trim().Length > 0 && !PunctuationOnly().IsMatch(text)).ToArray();
        if (chars.Length == 0 || count <= 0) return [];
        var output = new List<TemperaDecorFragment>(count);
        for (var index = 0; index < count; index++)
        {
            var character = chars[(int)Math.Floor(TemperaRandom.Hash01(seed, index, 13) * chars.Length) % chars.Length];
            var onLeft = TemperaRandom.Hash01(seed, index, 29) > 0.5;
            var edge = 0.03 + TemperaRandom.Hash01(seed, index, 41) * 0.13;
            output.Add(new TemperaDecorFragment(character, onLeft ? edge : 1 - edge,
                0.08 + TemperaRandom.Hash01(seed, index, 53) * 0.84,
                (TemperaRandom.Hash01(seed, index, 67) - 0.5) * 0.5,
                0.26 + TemperaRandom.Hash01(seed, index, 71) * 0.2));
        }
        return output;
    }

    /// <summary><c>Array.from(string)</c>: code points, not graphemes.</summary>
    private static IEnumerable<string> EnumerateCodePoints(string text)
    {
        foreach (var rune in text.EnumerateRunes()) yield return rune.ToString();
    }

    private static TemperaDecorWatermark? BuildDecorWatermark(List<string> pool, uint seed, bool allowed)
    {
        var words = pool.Select(word => word.Trim()).Where(word => word.Length > 0 && word.Length <= 12).ToArray();
        if (!allowed || words.Length == 0 || TemperaRandom.Hash01(seed, 4, 107) > 0.62) return null;
        return new TemperaDecorWatermark(
            words[(int)Math.Floor(TemperaRandom.Hash01(seed, 5, 109) * words.Length) % words.Length],
            0.28 + TemperaRandom.Hash01(seed, 6, 113) * 0.44,
            0.26 + TemperaRandom.Hash01(seed, 7, 127) * 0.48,
            (TemperaRandom.Hash01(seed, 8, 131) - 0.5) * 0.5,
            2.6 + TemperaRandom.Hash01(seed, 9, 137) * 1.9);
    }

    private static TemperaDecorSpec BuildDecorSpec(TemperaParagraphKind paragraphKind, string shotKind, string seedKey,
        string fragmentPool, List<string> watermarkPool, TemperaDecorMotif? previousMotif)
    {
        var seed = TemperaRandom.Hash(seedKey);
        var motifName = TemperaRandom.ChooseWithoutRepeat(MotifNames, seedKey,
            previousMotif is { } previous ? MotifNames[(int)previous] : null);
        var motif = (TemperaDecorMotif)Array.IndexOf(MotifNames, motifName);
        var mood = TemperaShotProfiles.Resolve(shotKind).Mood;
        var sparse = mood == TemperaMood.Quiet || paragraphKind is TemperaParagraphKind.Break or TemperaParagraphKind.Outro;
        return new TemperaDecorSpec(
            motif,
            // Shallow diagonals only; steep hatch reads as noise once the grain lands.
            (TemperaRandom.Hash01(seed, 1, 83) - 0.5) * (Math.PI / 2),
            1 + (int)Math.Floor(TemperaRandom.Hash01(seed, 2, 89) * 3),
            TemperaRandom.Mix(seed, 97),
            sparse ? BuildDecorFragments(fragmentPool, 3 + (int)Math.Floor(TemperaRandom.Hash01(seed, 3, 101) * 3), seed) : [],
            BuildDecorWatermark(watermarkPool, seed, mood != TemperaMood.Loud));
    }

    /// <summary>Vertical is Tempera's home axis; each shot turns a little and is pulled back toward it.</summary>
    private static double ResolveFlowAngle(double? previous, uint seed)
    {
        if (previous is not { } last)
        {
            var sign = TemperaRandom.Hash01(seed, 6, 71) > 0.5 ? 1 : -1;
            return sign * Math.PI / 2 + (TemperaRandom.Hash01(seed, 8, 79) - 0.5) * 0.4;
        }
        var axis = (Math.Sin(last) >= 0 ? 1 : -1) * Math.PI / 2;
        return last + (axis - last) * 0.3 + (TemperaRandom.Hash01(seed, 7, 73) - 0.5) * 0.5;
    }

    private sealed class Threads
    {
        public string? Kind;
        public TemperaDecorMotif? Motif;
        public double? Flow;
    }

    private static List<TemperaShot> BuildShots(List<TemperaCompiledLine> lines, IReadOnlyList<TemperaCompiledLine> songLines,
        TemperaParagraphKind kind, int paragraphIndex, string seed, Threads threads, bool wholeLine)
    {
        var paragraphEnd = lines.Count > 0 ? lines[^1].RenderEndTime : 0;
        var chunks = BuildShotChunks(lines, seed, paragraphIndex, wholeLine);
        var byIndex = lines.ToDictionary(line => line.SourceIndex);
        var shots = new List<TemperaShot>(chunks.Count);
        for (var shotIndex = 0; shotIndex < chunks.Count; shotIndex++)
        {
            var chunk = chunks[shotIndex];
            var line = byIndex.GetValueOrDefault(chunk.LineIndex);
            var sliceSegments = line is null ? [] : line.Segments.Skip(chunk.SegmentStart).Take(chunk.SegmentEnd - chunk.SegmentStart).ToArray();
            var sliceText = string.Concat(sliceSegments.Select(segment => segment.Text));
            var wordCount = sliceSegments.Count(segment => segment.IsWordLike);
            // Breathing paragraphs read as sparse compositions; a chorus never whispers.
            var sparse = kind == TemperaParagraphKind.Breath || kind != TemperaParagraphKind.Chorus && wordCount <= 2;
            TemperaMood[] moods = sparse ? [TemperaMood.Quiet]
                : kind == TemperaParagraphKind.Chorus ? [TemperaMood.Neutral, TemperaMood.Loud]
                : [TemperaMood.Quiet, TemperaMood.Neutral, TemperaMood.Loud];
            var shotKind = TemperaRandom.ChooseWithoutRepeat(TemperaShotProfiles.Candidates(moods),
                $"{seed}:{paragraphIndex}:{shotIndex}:{sliceText}", threads.Kind);
            threads.Kind = shotKind;

            var cameraSeed = TemperaRandom.Hash($"{seed}:{paragraphIndex}:{shotIndex}:camera");
            var flowAngle = ResolveFlowAngle(threads.Flow, cameraSeed);
            threads.Flow = flowAngle;
            var (start, end) = BuildCameraKeys(shotKind, cameraSeed, flowAngle);

            // Decor pools come from the rest of the paragraph (or, failing that, the rest of the song), never from this shot.
            var outsideSlice = lines.SelectMany(item => item.SourceIndex == chunk.LineIndex
                ? item.Segments.Where((_, index) => index < chunk.SegmentStart || index >= chunk.SegmentEnd)
                : item.Segments).ToList();
            var decorPool = outsideSlice.Count > 0
                ? outsideSlice
                : songLines.SelectMany(item => item.SourceIndex == chunk.LineIndex ? [] : item.Segments).ToList();
            var fragmentPool = string.Concat(decorPool.Select(segment => segment.Text));
            var watermarkPool = decorPool.Where(segment => segment.IsWordLike).Select(segment => segment.Text).ToList();
            var decor = BuildDecorSpec(kind, shotKind, $"{seed}:{paragraphIndex}:{shotIndex}:decor", fragmentPool, watermarkPool,
                threads.Motif);
            threads.Motif = decor.Motif;

            shots.Add(new TemperaShot
            {
                Id = $"p{paragraphIndex}-s{shotIndex}",
                Kind = shotKind,
                StartTime = chunk.StartTime,
                // The closing shot holds until the paragraph's own render tail ends.
                EndTime = shotIndex == chunks.Count - 1 ? Math.Max(chunk.EndTime, paragraphEnd) : chunk.EndTime,
                LyricEndTime = chunk.LyricEndTime,
                Slices = [new TemperaShotSlice(chunk.LineIndex, chunk.SegmentStart, chunk.SegmentEnd)],
                Camera = start,
                CameraEnd = end,
                FlowAngle = flowAngle,
                Decor = decor,
            });
        }
        return shots;
    }

    private static List<TemperaShot> BuildBridgeShots(TemperaParagraphKind paragraphKind, int paragraphIndex, string seed,
        string fragmentPool, double gapStart, double gapEnd, Threads threads)
    {
        var gap = gapEnd - gapStart;
        if (gap < BridgeMinGap) return [];
        var count = Math.Min(3, Math.Max(1, (int)Math.Ceiling(gap / BridgeMaxLength)));
        var step = gap / count;
        var shots = new List<TemperaShot>(count);
        for (var index = 0; index < count; index++)
        {
            // An instrumental beat is never the loudest thing in the song.
            var shotKind = TemperaRandom.ChooseWithoutRepeat(TemperaShotProfiles.Candidates(TemperaMood.Quiet, TemperaMood.Neutral),
                $"{seed}:{paragraphIndex}:bridge{index}", threads.Kind);
            threads.Kind = shotKind;
            var cameraSeed = TemperaRandom.Hash($"{seed}:{paragraphIndex}:bridge{index}:camera");
            var flowAngle = ResolveFlowAngle(threads.Flow, cameraSeed);
            threads.Flow = flowAngle;
            var (start, end) = BuildCameraKeys(shotKind, cameraSeed, flowAngle);
            var decor = BuildDecorSpec(paragraphKind, shotKind, $"{seed}:{paragraphIndex}:bridge{index}:decor", fragmentPool, [],
                threads.Motif);
            threads.Motif = decor.Motif;
            shots.Add(new TemperaShot
            {
                Id = $"p{paragraphIndex}-b{index}",
                Kind = shotKind,
                StartTime = gapStart + step * index,
                EndTime = gapStart + step * (index + 1),
                LyricEndTime = gapStart + step * (index + 1),
                Slices = [],
                IsBridge = true,
                Camera = start,
                CameraEnd = end,
                FlowAngle = flowAngle,
                Decor = decor,
            });
        }
        return shots;
    }

    /// <summary>Folia's virtual lines for an instrumental track: a ♪ every 8 s.</summary>
    public static IReadOnlyList<LumiereLine> InstrumentalLines()
    {
        var lines = new LumiereLine[60];
        for (var index = 0; index < lines.Length; index++)
            lines[index] = new LumiereLine("♪", index * 8, index * 8 + 6, []);
        return lines;
    }

    public static TemperaProgram Compile(IReadOnlyList<LumiereLine> lines, string seed = "tempera", bool wholeLineLyrics = false)
    {
        var compiled = new List<TemperaCompiledLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var nextStart = index + 1 < lines.Count ? lines[index + 1].StartTime : double.PositiveInfinity;
            compiled.Add(new TemperaCompiledLine(index, line,
                Math.Max(line.StartTime, Math.Min(line.RenderEndTime, nextStart)), BuildSegments(line)));
        }
        var threshold = ResolveParagraphGapThreshold(lines);
        var drafts = LyricParagraphScheduler.PartitionParagraphs(
            compiled, threshold,
            line => line.Line.StartTime,
            line => line.RenderEndTime,
            (prev, curr) => MetadataChanged(prev.Line, curr.Line),
            6, 18);

        var threads = new Threads();
        TemperaTransitionKind? previousTransition = null;
        var paragraphs = new List<TemperaParagraph>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            var kind = Classify(draft.Lines, index, drafts.Count);
            var shots = BuildShots(draft.Lines.ToList(), compiled, kind, index, seed, threads, wholeLineLyrics);
            var next = index + 1 < drafts.Count ? drafts[index + 1] : null;
            var endTime = draft.Lines[^1].RenderEndTime;
            var gap = next is null ? 0 : next.Lines[0].Line.StartTime - endTime;
            if (next is not null)
                shots.AddRange(BuildBridgeShots(kind, index, seed, string.Concat(draft.Lines.Select(item => item.Line.Text)),
                    endTime, next.Lines[0].Line.StartTime, threads));
            TemperaTransitionKind? transitionKind = null;
            if (next is not null)
            {
                var name = TemperaRandom.ChooseWithoutRepeat(TransitionNames, $"{seed}:{index}:transition",
                    previousTransition is { } last ? TransitionNames[(int)last] : null);
                transitionKind = TransitionKinds[Array.IndexOf(TransitionNames, name)];
                previousTransition = transitionKind;
            }
            // Long enough for the graphics to carry the cut, never eating more than ~0.3 s into the outgoing tail.
            var duration = next is not null ? Math.Min(1, Math.Max(0.35, Math.Max(gap, 0) + 0.3)) : 0;
            var transitionEnd = next?.Lines[0].Line.StartTime ?? endTime;
            paragraphs.Add(new TemperaParagraph($"tempera-p{index}", kind, (TemperaParagraphBoundary)(int)draft.Boundary, draft.Lines[0].Line.StartTime, endTime,
                draft.Lines, shots, transitionKind is { } transition
                    ? new TemperaTransition(transition, Math.Max(draft.Lines[0].Line.StartTime, transitionEnd - duration), transitionEnd)
                    : null));
        }

        // A paragraph's opening composition starts building while the previous one is still transitioning out.
        for (var index = 1; index < paragraphs.Count; index++)
        {
            var incoming = paragraphs[index].Shots.FirstOrDefault();
            var transition = paragraphs[index - 1].TransitionOut;
            if (incoming is null || transition is null || transition.Kind == TemperaTransitionKind.BlockWipe) continue;
            incoming.StartTime = Math.Min(incoming.StartTime, Math.Max(transition.StartTime, paragraphs[index - 1].EndTime));
        }
        return new TemperaProgram(seed, threshold, paragraphs);
    }
}
