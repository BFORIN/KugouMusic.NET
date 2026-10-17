using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaBandCompositions.ts: horizontal strata that read as depth under the vertical flow.</summary>
internal static class TemperaBandCompositions
{
    private static void BandStrip(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var bandY = height * 0.37;
        var bandHeight = height * 0.3;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, bandY, width + bleed * 2, bandHeight), palette.Tone3, 0.96, ctx.Gradient),
            O(span: 0.55, enterDX: -width * 0.5));
        ctx.Add(S.Lines([
            new TemperaLine(-bleed, bandY - 10, width + bleed, bandY - 22),
            new TemperaLine(-bleed, bandY + bandHeight + 22, width + bleed, bandY + bandHeight + 10),
        ], palette.Tone4, 1.4, 0.75), O(delay: 0.12, enterDX: width * 0.25));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.CrossMarks(H.CrossRow(ctx.Seed, 17, width * 0.06, bandY - height * 0.16, 4, width * 0.055, 9), palette.Ink, 2, 0.8),
            O(delay: 0.2, span: 0.5));
        ctx.Add(S.SquareMarks(H.DotRow(ctx.Seed, 19, width * 0.94, bandY + bandHeight + height * 0.06, 3, height * 0.05, 6), palette.Ink, 0.75),
            O(delay: 0.26, drift: true));
    }

    private static void HorizonBand(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var waterline = height * (0.5 + Hash01(ctx.Seed, 1, 23) * 0.12);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, waterline + bleed), palette.Tone1, 0.94, ctx.Gradient),
            O(span: 0.55, enterDY: -height * 0.3));
        var water = H.Rect(-bleed, waterline, width + bleed * 2, height - waterline + bleed);
        ctx.Add(S.PolygonFill(water, palette.Tone3, 0.95, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDY: height * 0.3));
        ctx.Add(S.HatchFill(water, H.Spec(ctx.Seed, 29) with { Angle = 0 }, palette.Tone4, 0.55), O(delay: 0.1, span: 0.55, grow: true));
        ctx.Add(S.Polyline(H.Wavy(ctx.Seed, 31, -bleed, width + bleed, waterline, height * 0.012, 30), palette.Ink, 2.2, 0.85),
            O(delay: 0.16, span: 0.5));
    }

    private static void DeepDive(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        TemperaRgb[] tones = [palette.Tone1, palette.Tone2, palette.Tone3, palette.Tone4];
        var bandHeight = (height + bleed * 2) / tones.Length;
        for (var index = 0; index < tones.Length; index++)
        {
            var top = -bleed + bandHeight * index;
            ctx.Add(S.PolygonFill(H.Rect(-bleed, top, width + bleed * 2, bandHeight + 1), tones[index], 0.95, ctx.Gradient),
                O(delay: index * 0.06, span: 0.55, enterDY: height * 0.3));
            if (index == 0) continue;
            ctx.Add(S.Polyline(H.Wavy(ctx.Seed, 37 + index, -bleed, width + bleed, top, height * 0.008, 24), palette.Paper, 1.6, 0.5),
                O(delay: index * 0.06 + 0.05, span: 0.5));
        }
    }

    private static void ToneRamp(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var spec = H.Spec(ctx.Seed, 41);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.92, ctx.Gradient), O(span: 0.5));
        const int steps = 4;
        for (var index = 0; index < steps; index++)
        {
            var columnWidth = (width + bleed * 2) / steps;
            var column = H.Rect(-bleed + columnWidth * index, -bleed, columnWidth, height + bleed * 2);
            ctx.Add(S.HatchFill(column, spec with { Spacing = spec.Spacing * (1.6 - index * 0.32) }, palette.Tone4, 0.6),
                O(delay: index * 0.06, span: 0.55, grow: true));
        }
    }

    private static void DoubleBand(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var slot = height * (0.2 + Hash01(ctx.Seed, 51, 43) * 0.08);
        var rail = height * 0.3;
        var top = (height - slot) / 2 - rail;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, top, width + bleed * 2, rail), palette.Tone3, 0.95, ctx.Gradient),
            O(span: 0.55, enterDX: -width * 0.4));
        ctx.Add(S.PolygonFill(H.Rect(-bleed, top + rail + slot, width + bleed * 2, rail), palette.Tone4, 0.95, ctx.Gradient),
            O(delay: 0.07, span: 0.55, enterDX: width * 0.4));
    }

    private static void TiltBand(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var lean = height * (0.14 + Hash01(ctx.Seed, 53, 47) * 0.14);
        var half = height * 0.17;
        double[] band =
        [
            -bleed, height / 2 + lean - half,
            width + bleed, height / 2 - lean - half,
            width + bleed, height / 2 - lean + half,
            -bleed, height / 2 + lean + half,
        ];
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(band, palette.Tone4, 0.95, ctx.Gradient), O(delay: 0.06, span: 0.55, enterDX: -width * 0.35));
        ctx.Add(S.HatchFill(band, H.Spec(ctx.Seed, 59) with { Angle = Math.PI / 2 }, palette.Paper, 0.3),
            O(delay: 0.12, span: 0.55, grow: true));
    }

    private static void EdgeRails(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var rail = height * (0.16 + Hash01(ctx.Seed, 61, 53) * 0.06);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, rail + bleed), palette.Tone3, 0.94, ctx.Gradient),
            O(span: 0.55, enterDY: -height * 0.2));
        ctx.Add(S.PolygonFill(H.Rect(-bleed, height - rail, width + bleed * 2, rail + bleed), palette.Tone3, 0.94, ctx.Gradient),
            O(delay: 0.06, span: 0.55, enterDY: height * 0.2));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Lines([
            new TemperaLine(-bleed, rail + 8, width + bleed, rail + 8),
            new TemperaLine(-bleed, height - rail - 8, width + bleed, height - rail - 8),
        ], palette.Tone4, 1.2, 0.6), O(delay: 0.16, span: 0.5, enterDX: width * 0.2));
    }

    private static void GradientWall(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var spec = H.Spec(ctx.Seed, 67);
        var downward = Hash01(ctx.Seed, 71, 59) > 0.5;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone2, 0.94, ctx.Gradient), O(span: 0.5));
        const int steps = 5;
        var bandHeight = height / steps;
        for (var index = 0; index < steps; index++)
        {
            var rank = downward ? index : steps - 1 - index;
            var top = bandHeight * index - (index == 0 ? bleed : 0);
            var bottom = bandHeight * (index + 1) + (index == steps - 1 ? bleed : 0);
            ctx.Add(S.HatchFill(H.Rect(-bleed, top, width + bleed * 2, bottom - top), spec with { Spacing = spec.Spacing * (1.9 - rank * 0.34) },
                palette.Tone4, 0.55), O(delay: index * 0.05, span: 0.55, grow: true));
        }
    }

    private static void Terrace(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        TemperaRgb[] tones = [palette.Tone1, palette.Tone2, palette.Tone3, palette.Tone4];
        var bandHeight = height / tones.Length;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        for (var index = 0; index < tones.Length; index++)
        {
            var left = width * index * 0.14 - bleed;
            var top = bandHeight * index - (index == 0 ? bleed : 0);
            var bottom = bandHeight * (index + 1) + (index == tones.Length - 1 ? bleed : 1);
            ctx.Add(S.PolygonFill(H.Rect(left, top, width + bleed * 2 - left - bleed, bottom - top), tones[index], 0.94, ctx.Gradient),
                O(delay: index * 0.06, span: 0.55, enterDX: -width * 0.25));
        }
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["band-strip"] = BandStrip;
        registry["horizon-band"] = HorizonBand;
        registry["deep-dive"] = DeepDive;
        registry["tone-ramp"] = ToneRamp;
        registry["double-band"] = DoubleBand;
        registry["tilt-band"] = TiltBand;
        registry["edge-rails"] = EdgeRails;
        registry["gradient-wall"] = GradientWall;
        registry["terrace"] = Terrace;
    }
}
