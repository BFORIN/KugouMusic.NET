using AvaloniaSilkEffects.Lumiere;
using AvaloniaSilkEffects.Lumiere.Text;
using AvaloniaSilkEffects.Tempera.Geometry;
using AvaloniaSilkEffects.Tempera.Rendering;
using AvaloniaSilkEffects.Tempera.Text;

namespace AvaloniaSilkEffects.Tempera;

/// <summary>One glyph's nodes and the constants its per-frame solver reads.</summary>
internal sealed class TemperaGlyphView
{
    public required TemperaText Display;
    public TemperaText? Shadow;
    public required TemperaText[] Echoes;
    public TemperaGlyphMotionInput Motion;
    public double BaseX;
    public double BaseY;
    public double ShadowDX;
    public double ShadowDY;
}

internal sealed class TemperaShotView
{
    public required TemperaShot Shot;
    public required TemperaGroup Container;
    public required List<TemperaGlyphView> Glyphs;
    public required TemperaBlocks Blocks;
    public double BaseX;
    public double BaseY;
    public double RevealDoneTime;
}

/// <summary>
/// Folia temperaSceneBuilder.ts buildTemperaScene: one paragraph scene — a translucent paper wash, the faint screentone dot
/// lattice, then per shot: the block MG layer, the watermark, the (inverted) text layer with ghosts and margin fragments, the
/// unfiltered echo layer and the keyword layer. Built once; playback only writes transforms and alpha.
/// </summary>
internal sealed class TemperaSceneView : IDisposable
{
    private const double ShadowOffsetX = 0.06;
    private const double ShadowOffsetY = 0.08;
    private const double ShadowAlpha = 0.34;

    public TemperaParagraph Paragraph { get; }
    public TemperaGroup Container { get; } = new() { Visible = false };
    public List<TemperaShotView> Shots { get; } = [];
    public TemperaPalette Palette { get; }
    public uint SceneSeed { get; }
    public int ActiveShotIndex { get; set; } = -1;
    private readonly List<TemperaGraphic> _graphics = [];

    public TemperaSceneView(TemperaProgram program, int index, TemperaTheme theme, TemperaTuning tuning, double width, double height,
        double lyricsFontScale, bool staticMode, IReadOnlyList<TemperaRgb> coverColors)
    {
        var paragraph = program.Paragraphs[index];
        Paragraph = paragraph;
        Palette = TemperaPalette.Resolve(theme, tuning.ColorMode, coverColors);
        SceneSeed = TemperaRandom.Hash($"{program.Seed}:{paragraph.Id}");
        var family = theme.FontFamily;
        var weight = theme.FontWeight;

        var paperWash = TemperaShapes.RectFill(0, 0, width, height, Palette.Paper, 0.35);
        paperWash.Visible = tuning.ShowBlocks;
        Container.Add(paperWash);
        _graphics.Add(paperWash);
        if (tuning.ShowBlocks)
        {
            // Spacing grows with the viewport so the lattice stays around 3k dots on any display.
            var toneSpacing = Math.Max(26, Math.Sqrt(width * height / 6000));
            var screentone = TemperaShapes.SquareMarks(TemperaHatch.DotGrid(width, height, toneSpacing, 1.6), Palette.Tone4, 0.05);
            Container.Add(screentone);
            _graphics.Add(screentone);
        }

        var matchers = LumiereKeywords.Prepare(theme.WordColors, true);
        for (var shotIndex = 0; shotIndex < paragraph.Shots.Count; shotIndex++)
        {
            var shot = paragraph.Shots[shotIndex];
            var shotContainer = new TemperaGroup();
            var sliceLines = new List<IReadOnlyList<TemperaSegment>>();
            var segmentColors = new List<IReadOnlyList<TemperaRgb?>>();
            foreach (var slice in shot.Slices)
            {
                var line = paragraph.Lines.FirstOrDefault(item => item.SourceIndex == slice.LineIndex);
                if (line is null) continue;
                var segments = line.Segments.Skip(slice.SegmentStart).Take(slice.SegmentEnd - slice.SegmentStart)
                    .Where(TemperaLayout.IsLayoutSegment).ToList();
                if (segments.Count == 0) continue;
                sliceLines.Add(segments);
                segmentColors.Add(matchers.Count == 0 ? [.. segments.Select(_ => (TemperaRgb?)null)] : KeywordColors(line, segments, matchers));
            }

            var maxGraphemes = Math.Max(3, sliceLines.Count == 0 ? 0 : sliceLines.Max(segments => segments.Sum(segment => segment.Graphemes.Count)));
            var baseFontSize = Math.Max(34, Math.Min(150, width / Math.Max(5, maxGraphemes * 1.05) * 1.5)) * lyricsFontScale;
            var shotSeed = SceneSeed + (long)shotIndex * 97;
            var blocks = new TemperaBlocks(shot.Kind, shot.Decor, Palette, width, height, shotSeed, tuning.ShowDecor, shot.FlowAngle);
            blocks.Container.Visible = tuning.ShowBlocks;

            var watermarkLayer = new TemperaGroup();
            var inverted = TextFilterLayer(Palette, tuning.TextInversion);
            TemperaGroup textLayer = inverted ?? new TemperaGroup();
            var echoLayer = new TemperaGroup();
            var keywordLayer = new TemperaGroup();
            // Everything the inversion should read renders before the text layer — the watermark included, which is the point
            // of it; nothing glyph-shaped may go there. Echoes and keyword glyphs render after it, unfiltered.
            shotContainer.Add(blocks.Container);
            shotContainer.Add(watermarkLayer);
            shotContainer.Add(textLayer);
            shotContainer.Add(echoLayer);
            shotContainer.Add(keywordLayer);

            var placements = TemperaLayout.Resolve(sliceLines, shot.Kind, width, height, baseFontSize, family, weight, shotSeed, segmentColors,
                tuning.GlyphSettleStretch);
            var glyphs = BuildTextViews(placements, Palette, family, weight, tuning.ShowDecor, tuning.ShowDecor && !staticMode ? 2 : 0,
                textLayer, echoLayer, keywordLayer);
            if (shot.Decor.Watermark is { } watermark && tuning.ShowDecor && watermark.Text.Trim().Length > 0)
            {
                watermarkLayer.Add(new TemperaText
                {
                    Text = watermark.Text,
                    FontFamily = family,
                    FontWeight = weight,
                    FontSize = Math.Max(48, baseFontSize * watermark.Scale),
                    Color = Palette.Tone4,
                    X = watermark.X * width,
                    Y = watermark.Y * height,
                    Rotation = watermark.Rotation,
                    Alpha = 0.16,
                });
            }
            if (shot.Decor.Fragments.Count > 0 && tuning.ShowDecor)
            {
                // Stray glyphs in the margins: no timeline of their own, the shot's alpha covers them.
                foreach (var fragment in shot.Decor.Fragments)
                    textLayer.Add(new TemperaText
                    {
                        Text = fragment.Char,
                        FontFamily = family,
                        FontWeight = weight,
                        FontSize = Math.Max(12, baseFontSize * fragment.Scale),
                        Color = Palette.Ink,
                        X = fragment.X * width,
                        Y = fragment.Y * height,
                        Rotation = fragment.Rotation,
                        Alpha = 0.42,
                    });
            }
            // A bridge shot has no type to reveal, so the camera breath may start immediately.
            var revealDoneTime = glyphs.Count > 0 ? glyphs.Max(glyph => glyph.Motion.SettleTime) : shot.StartTime;
            shotContainer.SetPivot(width / 2, height / 2);
            shotContainer.SetPosition(width / 2, height / 2);
            shotContainer.Visible = false;
            Container.Add(shotContainer);
            Shots.Add(new TemperaShotView
            {
                Shot = shot,
                Container = shotContainer,
                Glyphs = glyphs,
                Blocks = blocks,
                BaseX = shotContainer.X,
                BaseY = shotContainer.Y,
                RevealDoneTime = revealDoneTime,
            });
        }
    }

    /// <summary>
    /// The lyric's colour filter. In gradient mode the ramp only exists as this filter's tint, so <c>textInversion: false</c>
    /// keeps a tint-only filter; with neither ramp nor inversion the layer stays unfiltered.
    /// </summary>
    public static TemperaInvertLayer? TextFilterLayer(TemperaPalette palette, bool inversion) =>
        !inversion && palette.TextGradient is null
            ? null
            : new TemperaInvertLayer { Ink = palette.Ink, Paper = palette.Paper, Tint = palette.TextGradient, Inversion = inversion };

    /// <summary>A segment takes a keyword's colour when any of its graphemes is inside a matched range.</summary>
    private static IReadOnlyList<TemperaRgb?> KeywordColors(TemperaCompiledLine line, List<TemperaSegment> segments,
        IReadOnlyList<LumiereKeywordMatcher> matchers)
    {
        var colors = LumiereKeywords.GlyphColors(line.Line.Text, matchers);
        var graphemeStarts = new List<int>();
        var cursor = 0;
        foreach (var grapheme in LumiereReveal.Graphemes(line.Line.Text))
        {
            graphemeStarts.Add(cursor);
            cursor += grapheme.Length;
        }
        return [.. segments.Select(segment =>
        {
            for (var index = 0; index < graphemeStarts.Count && index < colors.Length; index++)
            {
                var start = graphemeStarts[index];
                if (start < segment.StartOffset || start >= segment.EndOffset || colors[index] is not { } color) continue;
                return (TemperaRgb?)new TemperaRgb(TemperaRgb.JsRound(color.X * 255), TemperaRgb.JsRound(color.Y * 255),
                    TemperaRgb.JsRound(color.Z * 255));
            }
            return null;
        })];
    }

    /// <summary>
    /// Folia temperaTextView.ts: one node per grapheme, an off-register ghost inside the inverted layer (inserted at the bottom,
    /// so the filter colours ghost and glyph together), and motion echoes in the unfiltered echo layer.
    /// </summary>
    private static List<TemperaGlyphView> BuildTextViews(List<TemperaGlyphPlacement> placements, TemperaPalette palette, string family,
        int weight, bool shadowEnabled, int echoCount, TemperaGroup textLayer, TemperaGroup echoLayer, TemperaGroup keywordLayer)
    {
        var views = new List<TemperaGlyphView>(placements.Count);
        foreach (var placement in placements)
        {
            if (placement.Char.Trim().Length == 0) continue;
            TemperaText Make(TemperaRgb color) => new()
            {
                Text = placement.Char,
                FontFamily = family,
                FontWeight = weight,
                FontSize = placement.FontSize,
                Color = color,
                Rotation = placement.Rotation,
            };
            var display = Make(placement.Color ?? palette.Ink);
            display.SetPosition(placement.X, placement.Y);
            TemperaText? shadow = null;
            if (shadowEnabled)
            {
                shadow = Make(palette.Ink);
                textLayer.AddAt(shadow, 0);
            }
            var echoes = new TemperaText[echoCount];
            for (var index = 0; index < echoCount; index++)
            {
                echoes[index] = Make(placement.Color ?? palette.Tone4);
                echoes[index].Visible = false;
                echoLayer.Add(echoes[index]);
            }
            (placement.Color is not null ? keywordLayer : textLayer).Add(display);
            views.Add(new TemperaGlyphView
            {
                Display = display,
                Shadow = shadow,
                Echoes = echoes,
                Motion = new TemperaGlyphMotionInput(placement.StartTime, placement.SettleTime, placement.EndTime, placement.EnterX,
                    placement.EnterY, placement.EnterRotation, placement.EnterScale, placement.Rotation, placement.EnterStyle,
                    placement.ReleaseTime, placement.TrackingX, placement.TrackingY),
                BaseX = placement.X,
                BaseY = placement.Y,
                ShadowDX = placement.FontSize * ShadowOffsetX,
                ShadowDY = placement.FontSize * ShadowOffsetY,
            });
        }
        return views;
    }

    /// <summary>
    /// Folia temperaShotFrame.ts updateTemperaShot: camera keys plus the hand-held breath, the push-in / push-out hand-off along
    /// the flow, the block layer's clock and every glyph's motion.
    /// </summary>
    public static void UpdateShot(TemperaShotView view, double time, double width, double height, TemperaTuning tuning, TemperaTheme theme)
    {
        var shot = view.Shot;
        var duration = Math.Max(shot.EndTime - shot.StartTime, 0.001);
        var rawProgress = (time - shot.StartTime) / duration;
        var animationScale = theme.AnimationScale;
        var camera = tuning.CameraIntensity * animationScale;
        var motion = tuning.GlyphMotion * animationScale;
        var frame = TemperaCamera.Frame(shot, rawProgress);
        var breathWeight = TemperaCamera.BreathWeight(time, view.RevealDoneTime);
        if (breathWeight > 0)
        {
            var phase = TemperaRandom.Hash(shot.Id) % 1024 / 1024d * Math.PI * 2;
            var breath = TemperaCamera.Breath(time, phase);
            frame = new TemperaCameraFrame(frame.X + breath.X * breathWeight, frame.Y + breath.Y * breathWeight,
                frame.Scale + breath.Scale * breathWeight, frame.Rotation + breath.Rotation * breathWeight);
        }
        var handoff = Handoff(shot);
        var span = Math.Max(width, height);
        // The arrival is front-loaded: glyphs reveal on the shot's own clock, so a slow entrance would expose type off frame.
        var enter = TemperaEasing.Enter(TemperaEasing.Clamp01((time - shot.StartTime) / (handoff * 0.8)));
        var exit = TemperaEasing.InOut(Exit(shot, time));
        var travel = exit * span * 0.55 - (1 - enter) * span * 0.32;
        view.Container.SetPosition(view.BaseX + frame.X * width * camera + Math.Cos(shot.FlowAngle) * travel,
            view.BaseY + frame.Y * height * camera + Math.Sin(shot.FlowAngle) * travel);
        // Opaque on the way in: this is a push, not a dissolve. Only the exit fades.
        view.Container.Alpha = 1 - exit;
        view.Container.SetScale((1 + (frame.Scale - 1) * camera) * (1 - exit * 0.08));
        view.Container.Rotation = frame.Rotation * camera;

        view.Blocks.Update(time, shot.StartTime, shot.EndTime, shot.LyricEndTime);

        foreach (var glyph in view.Glyphs)
        {
            var glyphFrame = TemperaMotion.Resolve(glyph.Motion, time, motion);
            var x = glyph.BaseX + glyphFrame.X;
            var y = glyph.BaseY + glyphFrame.Y;
            var display = glyph.Display;
            display.Alpha = glyphFrame.Alpha;
            display.Visible = glyphFrame.Visible;
            display.SetPosition(x, y);
            display.ScaleX = glyphFrame.ScaleX;
            display.ScaleY = glyphFrame.ScaleY;
            display.Rotation = glyphFrame.Rotation;
            if (glyph.Shadow is { } shadow)
            {
                shadow.Alpha = glyphFrame.Alpha * ShadowAlpha;
                shadow.Visible = glyphFrame.Visible;
                shadow.SetPosition(x + glyph.ShadowDX, y + glyph.ShadowDY);
                shadow.ScaleX = glyphFrame.ScaleX;
                shadow.ScaleY = glyphFrame.ScaleY;
                shadow.Rotation = glyphFrame.Rotation;
            }
            var echoVisible = glyphFrame.Visible && glyphFrame.EchoAlpha > 0.004;
            for (var index = 0; index < glyph.Echoes.Length; index++)
            {
                var echo = glyph.Echoes[index];
                echo.Visible = echoVisible;
                if (!echoVisible) continue;
                var depth = 1 + index * 0.85;
                echo.Alpha = glyphFrame.EchoAlpha / (index + 1.4);
                echo.SetPosition(glyph.BaseX + glyphFrame.EchoX * depth, glyph.BaseY + glyphFrame.EchoY * depth);
                echo.ScaleX = glyphFrame.ScaleX;
                echo.ScaleY = glyphFrame.ScaleY;
                echo.Rotation = glyphFrame.Rotation;
            }
        }
    }

    /// <summary>How long a finished shot keeps sliding out while the next one already slides in.</summary>
    public static double Handoff(TemperaShot shot) => TemperaEasing.ShotPaced(shot.EndTime - shot.StartTime, 0.3, 0.4, 1.1);

    public static double Exit(TemperaShot shot, double time) => TemperaEasing.Clamp01((time - shot.EndTime) / Handoff(shot));

    public int FindActiveShotIndex(double time)
    {
        for (var index = Shots.Count - 1; index >= 0; index--)
            if (time >= Shots[index].Shot.StartTime) return index;
        return 0;
    }

    public void Dispose()
    {
        foreach (var graphic in _graphics) graphic.Dispose();
        foreach (var shot in Shots) shot.Blocks.Dispose();
    }
}
