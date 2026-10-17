using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaSplitCompositions.ts: flat tone panels. A glyph straddling a panel edge flips colour
/// mid-stroke, which is where the inversion filter reacts most strongly.
/// </summary>
internal static class TemperaSplitCompositions
{
    private readonly record struct Panel(double[] Polygon, TemperaRgb Tone, double EnterDX, double EnterDY);

    private static void AddPanels(TemperaCompositionContext ctx, Panel[] panels, int hatchIndex)
    {
        var hatch = H.Spec(ctx.Seed, 5);
        for (var index = 0; index < panels.Length; index++)
        {
            var panel = panels[index];
            ctx.Add(S.PolygonFill(panel.Polygon, panel.Tone, 0.96, ctx.Gradient),
                O(delay: index * 0.05, span: 0.5, enterDX: panel.EnterDX, enterDY: panel.EnterDY));
            if (index != hatchIndex) continue;
            ctx.Add(S.HatchFill(panel.Polygon, hatch, ctx.Palette.Tone4, 0.5), O(delay: index * 0.05 + 0.06, span: 0.5, grow: true));
        }
    }

    private static void AddSeam(TemperaCompositionContext ctx, double[] polygon, double delay) =>
        ctx.Add(S.PolygonFill(polygon, ctx.Palette.Ink, 0.85, ctx.Gradient), O(delay: delay, span: 0.5));

    private static void DuoSplit(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var horizontal = Hash01(ctx.Seed, 1, 3) > 0.5;
        AddPanels(ctx, horizontal
            ?
            [
                new(H.Rect(-bleed, -bleed, width + bleed * 2, height * 0.52 + bleed), palette.Tone1, 0, -height * 0.55),
                new(H.Rect(-bleed, height * 0.52, width + bleed * 2, height * 0.48 + bleed), palette.Tone3, 0, height * 0.55),
            ]
            :
            [
                new(H.Rect(-bleed, -bleed, width * 0.5 + bleed, height + bleed * 2), palette.Tone1, -width * 0.5, 0),
                new(H.Rect(width * 0.5, -bleed, width * 0.5 + bleed, height + bleed * 2), palette.Tone3, width * 0.5, 0),
            ], 0);
        AddSeam(ctx, horizontal
            ? H.Rect(-bleed, height * 0.52 - 1.5, width + bleed * 2, 3)
            : H.Rect(width * 0.5 - 1.5, -bleed, 3, height + bleed * 2), 0.16);
    }

    private static void QuadSplit(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var splitX = width * (0.42 + Hash01(ctx.Seed, 2, 7) * 0.16);
        var splitY = height * (0.42 + Hash01(ctx.Seed, 3, 11) * 0.16);
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, splitX + bleed, splitY + bleed), palette.Tone1, -width * 0.3, -height * 0.3),
            new(H.Rect(splitX, -bleed, width - splitX + bleed, splitY + bleed), palette.Tone4, width * 0.3, -height * 0.3),
            new(H.Rect(-bleed, splitY, splitX + bleed, height - splitY + bleed), palette.Tone3, -width * 0.3, height * 0.3),
            new(H.Rect(splitX, splitY, width - splitX + bleed, height - splitY + bleed), palette.Tone2, width * 0.3, height * 0.3),
        ], 3);
        AddSeam(ctx, H.Rect(splitX - 1.5, -bleed, 3, height + bleed * 2), 0.2);
        AddSeam(ctx, H.Rect(-bleed, splitY - 1.5, width + bleed * 2, 3), 0.24);
    }

    private static void TriColumn(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var edge = width * (0.26 + Hash01(ctx.Seed, 4, 13) * 0.06);
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, edge + bleed, height + bleed * 2), palette.Tone1, -width * 0.25, 0),
            new(H.Rect(edge, -bleed, width - edge * 2, height + bleed * 2), palette.Tone4, 0, -height * 0.3),
            new(H.Rect(width - edge, -bleed, edge + bleed, height + bleed * 2), palette.Tone1, width * 0.25, 0),
        ], 0);
    }

    private static void ThirdsStack(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var band = height * 0.34;
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, width + bleed * 2, band + bleed), palette.Tone2, -width * 0.2, 0),
            new(H.Rect(-bleed, band, width + bleed * 2, band), palette.Tone4, width * 0.2, 0),
            new(H.Rect(-bleed, band * 2, width + bleed * 2, height - band * 2 + bleed), palette.Tone1, -width * 0.2, 0),
        ], 0);
    }

    private static void CheckerQuad(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var splitX = width * 0.5;
        var splitY = height * 0.5;
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, splitX + bleed, splitY + bleed), palette.Tone4, 0, -height * 0.35),
            new(H.Rect(splitX, -bleed, width - splitX + bleed, splitY + bleed), palette.Tone1, 0, -height * 0.35),
            new(H.Rect(-bleed, splitY, splitX + bleed, height - splitY + bleed), palette.Tone1, 0, height * 0.35),
            new(H.Rect(splitX, splitY, width - splitX + bleed, height - splitY + bleed), palette.Tone4, 0, height * 0.35),
        ], 1);
    }

    private static void CornerWedge(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var fromLeft = Hash01(ctx.Seed, 5, 17) > 0.5;
        var apexX = fromLeft ? width * 0.78 : width * 0.22;
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0, 0),
            new(fromLeft
                    ? [-bleed, -bleed, apexX, -bleed, -bleed, height + bleed]
                    : [width + bleed, -bleed, apexX, -bleed, width + bleed, height + bleed],
                palette.Tone4, fromLeft ? -width * 0.4 : width * 0.4, 0),
        ], 1);
    }

    private static void DiagonalHalves(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var lean = height * (0.2 + Hash01(ctx.Seed, 6, 19) * 0.3);
        AddPanels(ctx,
        [
            new([-bleed, -bleed, width + bleed, -bleed, width + bleed, lean, -bleed, height - lean], palette.Tone2, 0, -height * 0.4),
            new([-bleed, height - lean, width + bleed, lean, width + bleed, height + bleed, -bleed, height + bleed], palette.Tone4, 0,
                height * 0.4),
        ], 1);
    }

    private static void CrossAxis(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var barX = width * 0.11;
        var barY = height * 0.13;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(H.Rect((width - barX) / 2, -bleed, barX, height + bleed * 2), palette.Tone4, 0.95),
            O(delay: 0.06, span: 0.5, enterDY: -height * 0.5));
        ctx.Add(S.PolygonFill(H.Rect(-bleed, (height - barY) / 2, width + bleed * 2, barY), palette.Tone3, 0.95),
            O(delay: 0.12, span: 0.5, enterDX: width * 0.5));
    }

    private static void OffsetHalves(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var step = height * (0.06 + Hash01(ctx.Seed, 7, 23) * 0.06);
        var seam = width * 0.48;
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, seam + bleed, height * 0.5 + step + bleed), palette.Tone1, -width * 0.3, 0),
            new(H.Rect(seam, -bleed, width - seam + bleed, height * 0.5 - step + bleed), palette.Tone3, width * 0.3, 0),
            new(H.Rect(-bleed, height * 0.5 + step, seam + bleed, height * 0.5 - step + bleed), palette.Tone4, -width * 0.2, 0),
            new(H.Rect(seam, height * 0.5 - step, width - seam + bleed, height * 0.5 + step + bleed), palette.Tone2, width * 0.2, 0),
        ], 2);
    }

    private static void StairBlocks(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        TemperaRgb[] tones = [palette.Tone1, palette.Tone2, palette.Tone3, palette.Tone4];
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        for (var index = 0; index < tones.Length; index++)
        {
            var left = width * (0.06 + index * 0.2);
            var top = height * (0.1 + index * 0.16);
            ctx.Add(S.PolygonFill(H.Rect(left, top, width * 0.3, height * 0.34), tones[index], 0.94, ctx.Gradient),
                O(delay: index * 0.06, span: 0.5, enterDX: -width * 0.2, enterDY: height * 0.12));
        }
    }

    private static void PillarGap(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var gap = width * (0.26 + Hash01(ctx.Seed, 8, 29) * 0.08);
        var side = (width - gap) / 2;
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, side + bleed, height + bleed * 2), palette.Tone4, -width * 0.3, 0),
            new(H.Rect(width - side, -bleed, side + bleed, height + bleed * 2), palette.Tone4, width * 0.3, 0),
        ], 1);
    }

    private static void CornerQuad(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var splitX = width * (0.6 + Hash01(ctx.Seed, 9, 31) * 0.12);
        var splitY = height * (0.62 + Hash01(ctx.Seed, 10, 37) * 0.1);
        AddPanels(ctx,
        [
            new(H.Rect(-bleed, -bleed, splitX + bleed, splitY + bleed), palette.Tone2, 0, -height * 0.25),
            new(H.Rect(splitX, -bleed, width - splitX + bleed, splitY + bleed), palette.Tone4, width * 0.25, 0),
            new(H.Rect(-bleed, splitY, splitX + bleed, height - splitY + bleed), palette.Tone1, -width * 0.25, 0),
            new(H.Rect(splitX, splitY, width - splitX + bleed, height - splitY + bleed), palette.Tone3, 0, height * 0.25),
        ], 0);
    }

    private static void SliverStack(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var fromLeft = Hash01(ctx.Seed, 11, 41) > 0.5;
        var slabWidth = width * 0.46;
        var left = fromLeft ? -bleed : width - slabWidth;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        const int count = 9;
        var sliver = (height + bleed * 2) / count;
        for (var index = 0; index < count; index++)
        {
            if (index % 2 == 1) continue;
            ctx.Add(S.PolygonFill(H.Rect(left, -bleed + sliver * index, slabWidth + bleed, sliver), palette.Tone4, 0.92, ctx.Gradient),
                O(delay: index * 0.02, span: 0.5, enterDX: (fromLeft ? -1 : 1) * width * 0.2));
        }
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["duo-split"] = DuoSplit;
        registry["quad-split"] = QuadSplit;
        registry["tri-column"] = TriColumn;
        registry["thirds-stack"] = ThirdsStack;
        registry["checker-quad"] = CheckerQuad;
        registry["corner-wedge"] = CornerWedge;
        registry["diagonal-halves"] = DiagonalHalves;
        registry["cross-axis"] = CrossAxis;
        registry["offset-halves"] = OffsetHalves;
        registry["stair-blocks"] = StairBlocks;
        registry["pillar-gap"] = PillarGap;
        registry["corner-quad"] = CornerQuad;
        registry["sliver-stack"] = SliverStack;
    }
}
