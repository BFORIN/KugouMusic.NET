namespace AvaloniaSilkEffects.Tempera.Text;

/// <summary>One placed grapheme: where it lands, how it arrives, when it settles and releases.</summary>
internal sealed class TemperaGlyphPlacement
{
    public required string Char;
    public int LineIndex;
    public int SegmentIndex;
    public double X;
    public double Y;
    public double Rotation;
    public double StartTime;
    public double EndTime;
    public double SettleTime;
    public double FontSize;
    /// <summary>Keyword colour from the theme; such glyphs keep their hue instead of being inverted.</summary>
    public TemperaRgb? Color;
    public double EnterX;
    public double EnterY;
    public double EnterRotation;
    public double EnterScale;
    public TemperaEnterStyle EnterStyle;
    public double ReleaseTime;
    public double TrackingX;
    public double TrackingY;
}

/// <summary>
/// Folia temperaLayout.ts + temperaMeasure.ts: collage typesetting. Word segmentation only picks font sizes (a hero word per
/// line at 1.34–1.6×, the rest at 0.7–0.86×); words pack into ragged rows inside the shot's region with seeded indents and
/// tilts; a fit loop shrinks until the block fits; every glyph gets its own fanned entrance vector and one entrance style per
/// word; the entrance windows sweep toward the shot's lyric end and the sung block later widens its tracking.
/// </summary>
internal static class TemperaLayout
{
    private const double MinSettleSeconds = 0.34;

    private sealed class WordGlyph
    {
        public required string Char;
        public double StartTime;
        public double EndTime;
        public double Offset;
        public double Width;
    }

    private sealed class WordUnit
    {
        public int LineIndex;
        public int SegmentIndex;
        public int StartOffset;
        public int EndOffset;
        public double LeadingGap;
        public double Scale;
        public double Width;
        public required List<WordGlyph> Glyphs;
    }

    private sealed class Row
    {
        public required List<WordUnit> Words;
        public double Width;
        public double Height;
    }

    public static bool IsLayoutSegment(TemperaSegment segment) => segment.Text.Trim().Length > 0;

    private static (double X, double Y) RotateAbout(double x, double y, double cx, double cy, double angle)
    {
        if (angle == 0) return (x, y);
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var dx = x - cx;
        var dy = y - cy;
        return (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
    }

    private static double[] WordScales(IReadOnlyList<TemperaSegment> segments, long seed, int lineIndex)
    {
        var lengths = segments.Select(segment => segment.IsWordLike ? segment.Graphemes.Count : 0).ToArray();
        var maxLength = Math.Max(0, lengths.Length == 0 ? 0 : lengths.Max());
        var heroCandidates = lengths.Select((length, index) => (length, index))
            .Where(entry => entry.length > 0 && entry.length >= maxLength - 1).Select(entry => entry.index).ToArray();
        var heroIndex = heroCandidates.Length > 0
            ? heroCandidates[(int)Math.Floor(TemperaRandom.Hash01(seed, lineIndex, 101) * heroCandidates.Length) % heroCandidates.Length]
            : -1;
        return [.. segments.Select((_, index) => index == heroIndex
            ? 1.34 + TemperaRandom.Hash01(seed, lineIndex * 31 + index, 103) * 0.26
            : 0.7 + TemperaRandom.Hash01(seed, lineIndex * 31 + index, 107) * 0.16)];
    }

    private static WordUnit? BuildWordUnit(TemperaSegment segment, int lineIndex, int segmentIndex, double fontSize, double scale,
        string family, int weight)
    {
        var glyphs = segment.Graphemes.Where(grapheme => grapheme.Char.Length > 0).ToArray();
        if (glyphs.Length == 0) return null;
        var scaledSize = fontSize * scale;
        var raw = glyphs.Select(grapheme => TemperaFonts.MeasureGrapheme(grapheme.Char, family, weight, scaledSize)).ToArray();
        var rawTotal = raw.Sum();
        var shaped = TemperaFonts.Measure(segment.Text.TrimEnd(), family, weight, scaledSize);
        // Distribute the shaping difference proportionally instead of nudging a single glyph.
        var correction = rawTotal > 0 ? shaped / rawTotal : 1;
        var offset = 0d;
        var placed = new List<WordGlyph>(glyphs.Length);
        for (var index = 0; index < glyphs.Length; index++)
        {
            var width = raw[index] * correction;
            placed.Add(new WordGlyph
            {
                Char = glyphs[index].Char,
                StartTime = glyphs[index].StartTime,
                EndTime = glyphs[index].EndTime,
                Offset = offset,
                Width = width,
            });
            offset += width;
        }
        return new WordUnit
        {
            LineIndex = lineIndex,
            SegmentIndex = segmentIndex,
            StartOffset = segment.StartOffset,
            EndOffset = segment.EndOffset,
            Scale = scale,
            Width = offset,
            Glyphs = placed,
        };
    }

    /// <summary>A real space only where the source had whitespace; a bare segmentation boundary gets an optical hair.</summary>
    private static List<WordUnit> BuildWordUnits(IReadOnlyList<IReadOnlyList<TemperaSegment>> lines, double fontSize, long seed,
        string family, int weight)
    {
        var units = new List<WordUnit>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var segments = lines[lineIndex];
            var scales = WordScales(segments, seed, lineIndex);
            int? previousEnd = null;
            for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                var segment = segments[segmentIndex];
                if (BuildWordUnit(segment, lineIndex, segmentIndex, fontSize, scales[segmentIndex], family, weight) is not { } unit) continue;
                var spaced = previousEnd is not null && segment.StartOffset > previousEnd;
                unit.LeadingGap = previousEnd is null ? 0 : fontSize * (spaced ? 0.26 : 0.035);
                previousEnd = segment.EndOffset;
                units.Add(unit);
            }
        }
        return units;
    }

    private static List<Row> PackRows(List<WordUnit> words, double maxWidth, long seed)
    {
        var rows = new List<Row>();
        var current = new List<WordUnit>();
        var currentWidth = 0d;
        double BudgetFor(int rowIndex) => maxWidth * (0.72 + TemperaRandom.Hash01(seed, rowIndex, 109) * 0.28);
        void Flush()
        {
            if (current.Count == 0) return;
            rows.Add(new Row { Words = current, Width = currentWidth });
            current = [];
            currentWidth = 0;
        }
        foreach (var word in words)
        {
            var newLine = current.Count > 0 && word.LineIndex != current[0].LineIndex;
            var advance = current.Count == 0 ? word.Width : word.Width + word.LeadingGap;
            if (newLine || current.Count > 0 && currentWidth + advance > BudgetFor(rows.Count)) Flush();
            currentWidth += current.Count == 0 ? word.Width : advance;
            current.Add(word);
        }
        Flush();
        return rows;
    }

    private static void ApplyGlyphTiming(List<TemperaGlyphPlacement> placements, double settleStretch)
    {
        if (placements.Count == 0) return;
        var stretch = double.IsFinite(settleStretch) ? Math.Clamp(settleStretch, 0, 1) : 0.5;
        var lineEnds = new Dictionary<int, double>();
        var lineStarts = new Dictionary<int, double>();
        foreach (var placement in placements)
        {
            lineEnds[placement.LineIndex] = Math.Max(lineEnds.GetValueOrDefault(placement.LineIndex, 0), placement.EndTime);
            lineStarts[placement.LineIndex] = Math.Min(lineStarts.GetValueOrDefault(placement.LineIndex, double.PositiveInfinity),
                placement.StartTime);
        }
        var shotLyricEnd = placements.Aggregate(0d, (latest, placement) => Math.Max(latest, placement.EndTime));
        foreach (var placement in placements)
        {
            var reach = Math.Max(0, shotLyricEnd - placement.StartTime - MinSettleSeconds);
            placement.SettleTime = placement.StartTime + MinSettleSeconds + reach * stretch;
        }
        var centerX = placements.Sum(placement => placement.X) / placements.Count;
        var centerY = placements.Sum(placement => placement.Y) / placements.Count;
        foreach (var placement in placements)
        {
            var span = Math.Max(0.5, lineEnds[placement.LineIndex] - lineStarts[placement.LineIndex]);
            placement.ReleaseTime = Math.Max(placement.EndTime, placement.SettleTime) + span;
            placement.TrackingX = placement.X - centerX;
            placement.TrackingY = placement.Y - centerY;
        }
    }

    public static List<TemperaGlyphPlacement> Resolve(IReadOnlyList<IReadOnlyList<TemperaSegment>> lines, string shotKind, double width,
        double height, double baseFontSize, string family, int weight, long seed, IReadOnlyList<IReadOnlyList<TemperaRgb?>>? segmentColors,
        double settleStretch)
    {
        var profile = TemperaShotProfiles.Resolve(shotKind);
        var region = profile.Region;
        var centerX = region.Cx * width;
        var centerY = region.Cy * height;
        var regionWidth = region.W * width;
        var regionHeight = region.H * height;

        var fontSize = Math.Max(14, baseFontSize * region.FontScale);
        var rows = new List<Row>();
        var blockHeight = 0d;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            rows = PackRows(BuildWordUnits(lines, fontSize, seed, family, weight), regionWidth, seed);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var tallest = rows[rowIndex].Words.Max(word => word.Scale);
                rows[rowIndex].Height = fontSize * tallest * (1.02 + TemperaRandom.Hash01(seed, rowIndex, 113) * 0.1);
            }
            blockHeight = rows.Sum(row => row.Height);
            var maxRowWidth = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(row => row.Width));
            var fit = Math.Min(1, Math.Min(
                blockHeight > regionHeight ? regionHeight / blockHeight : 1,
                maxRowWidth > regionWidth ? regionWidth / maxRowWidth : 1));
            if (fit >= 0.999) break;
            fontSize *= fit;
        }

        var baseX = profile.Enter.X * fontSize;
        var baseY = profile.Enter.Y * fontSize;
        var baseAngle = Math.Atan2(baseY, baseX);
        var baseMagnitude = Math.Sqrt(baseX * baseX + baseY * baseY);
        var placements = new List<TemperaGlyphPlacement>();
        var cursorY = centerY - blockHeight / 2;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowCenterY = cursorY + row.Height / 2;
            cursorY += row.Height;
            var drift = (TemperaRandom.Hash01(seed, rowIndex, 127) - 0.5) * regionWidth * 0.06;
            var rowRotation = (TemperaRandom.Hash01(seed, rowIndex, 131) - 0.5) * 0.06;
            var rowLeft = region.Align switch
            {
                TemperaAlign.Center => centerX - row.Width / 2 + drift,
                TemperaAlign.Left => centerX - regionWidth / 2 + Math.Abs(drift) * 0.6,
                _ => centerX + regionWidth / 2 - row.Width - Math.Abs(drift) * 0.6,
            };
            var rowCenterX = rowLeft + row.Width / 2;
            var cursorX = rowLeft;
            for (var wordIndex = 0; wordIndex < row.Words.Count; wordIndex++)
            {
                var word = row.Words[wordIndex];
                var salt = rowIndex * 37 + wordIndex;
                var enterStyle = TemperaMotion.EnterStyles[
                    (int)Math.Floor(TemperaRandom.Hash01(seed, salt, 193) * TemperaMotion.EnterStyles.Length) % TemperaMotion.EnterStyles.Length];
                var wordRotation = (TemperaRandom.Hash01(seed, salt, 137) - 0.5) * 0.07;
                var wordShiftY = (TemperaRandom.Hash01(seed, salt, 139) - 0.5) * fontSize * 0.09;
                var wordLeft = cursorX + (wordIndex == 0 ? 0 : word.LeadingGap);
                cursorX = wordLeft + word.Width;
                var wordCenterX = wordLeft + word.Width / 2;
                var wordCenterY = rowCenterY + wordShiftY;
                var glyphSize = fontSize * word.Scale;
                for (var glyphIndex = 0; glyphIndex < word.Glyphs.Count; glyphIndex++)
                {
                    var glyph = word.Glyphs[glyphIndex];
                    if (glyph.Char.Trim().Length == 0) continue;
                    var localX = wordLeft + glyph.Offset + glyph.Width / 2;
                    var rotatedWord = RotateAbout(localX, wordCenterY, wordCenterX, wordCenterY, wordRotation);
                    var rotatedRow = RotateAbout(rotatedWord.X, rotatedWord.Y, rowCenterX, rowCenterY, rowRotation);
                    var final = RotateAbout(rotatedRow.X, rotatedRow.Y, centerX, centerY, region.Rotation);
                    var glyphSalt = salt * 53 + glyphIndex;
                    var spread = (TemperaRandom.Hash01(seed, glyphSalt, 149) - 0.5) * 1.7;
                    var magnitude = baseMagnitude * (0.55 + TemperaRandom.Hash01(seed, glyphSalt, 151) * 1.05);
                    var angle = baseAngle + spread;
                    placements.Add(new TemperaGlyphPlacement
                    {
                        Char = glyph.Char,
                        LineIndex = word.LineIndex,
                        SegmentIndex = word.SegmentIndex,
                        X = final.X,
                        Y = final.Y,
                        Rotation = region.Rotation + rowRotation + wordRotation,
                        StartTime = glyph.StartTime,
                        EndTime = glyph.EndTime,
                        SettleTime = glyph.EndTime,
                        FontSize = glyphSize,
                        Color = segmentColors is not null && word.LineIndex < segmentColors.Count
                            && word.SegmentIndex < segmentColors[word.LineIndex].Count
                            ? segmentColors[word.LineIndex][word.SegmentIndex]
                            : null,
                        EnterX = Math.Cos(angle) * magnitude,
                        EnterY = Math.Sin(angle) * magnitude,
                        EnterRotation = (TemperaRandom.Hash01(seed, glyphSalt, 157) - 0.5) * 0.7,
                        EnterScale = 0.6 + TemperaRandom.Hash01(seed, glyphSalt, 163) * 0.3,
                        EnterStyle = enterStyle,
                    });
                }
            }
        }
        ApplyGlyphTiming(placements, settleStretch);
        return placements;
    }
}
