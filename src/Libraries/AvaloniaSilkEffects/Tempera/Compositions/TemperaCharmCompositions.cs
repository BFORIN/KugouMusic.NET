using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaCharmCompositions.ts: bubbles, frills, hearts, sparkles and sticker plates laid on a flat field.
/// Every shape keeps its ink outline so the inversion has an edge to cut against.
/// </summary>
internal static class TemperaCharmCompositions
{
    private static void AddField(TemperaCompositionContext ctx, TemperaRgb color, double alpha = 0.92)
    {
        var (width, height, bleed) = (ctx.Width, ctx.Height, ctx.Bleed);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), color, alpha, ctx.Gradient), O(span: 0.5));
    }

    private static void BubbleDrift(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        AddField(ctx, palette.Tone1);
        var big = C.DiscField(ctx.Seed, 11, width, height, 8, unit * 0.115);
        var small = C.DiscField(ctx.Seed, 17, width, height, 13, unit * 0.042);
        ctx.Add(S.Discs(big, palette.Tone3, 0.88, ctx.Gradient), O(delay: 0.05, span: 0.6, enterDY: height * 0.16, drift: true));
        ctx.Add(S.Rings(big, palette.Ink, 2, 0.55), O(delay: 0.12, span: 0.55, enterDY: height * 0.12));
        ctx.Add(S.Discs(small, palette.Tone4, 0.7), O(delay: 0.18, span: 0.6, enterDY: height * 0.22, drift: true));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Discs(big.Take(2).Select(disc => new TemperaDisc(disc.X - disc.Radius * 0.36, disc.Y - disc.Radius * 0.36, disc.Radius * 0.16))
            .ToList(), palette.Paper, 0.85), O(delay: 0.28, drift: true));
    }

    private static void CloudWindow(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width / 2;
        var cy = height * 0.52;
        var rx = width * 0.3;
        var ry = height * 0.3;
        AddField(ctx, palette.Tone3);
        var cloud = C.Lobed(cx, cy, rx, ry, 9, 0.1);
        ctx.Add(S.PolygonFill(C.Lobed(cx, cy + height * 0.025, rx * 1.05, ry * 1.05, 9, 0.1), palette.Tone4, 0.5), O(delay: 0.04, span: 0.6));
        ctx.Add(S.PolygonFill(cloud, palette.Paper, 0.96, ctx.Gradient), O(delay: 0.08, span: 0.55));
        ctx.Add(S.PolygonOutline(cloud, palette.Ink, 2.4, 0.85), O(delay: 0.12, span: 0.5));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Discs([
            new TemperaDisc(cx - rx * 1.16, cy + ry * 0.66, unit * 0.035),
            new TemperaDisc(cx - rx * 1.32, cy + ry * 0.9, unit * 0.02),
        ], palette.Paper, 0.92, ctx.Gradient), O(delay: 0.22, drift: true));
    }

    private static void HeartBurst(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var lean = Hash01(ctx.Seed, 3, 83) > 0.5 ? 1 : -1;
        var cx = width * 0.5;
        var cy = height * 0.52;
        var rx = width * 0.32;
        var ry = height * 0.4;
        AddField(ctx, palette.Tone4, 0.94);
        var main = C.Rotate(C.Heart(cx, cy, rx, ry), cx, cy, lean * 0.06);
        ctx.Add(S.PolygonFill(C.Rotate(C.Heart(cx + lean * width * 0.022, cy + height * 0.025, rx, ry), cx, cy, lean * 0.06), palette.Tone2, 0.6),
            O(delay: 0.04, span: 0.6));
        ctx.Add(S.PolygonFill(main, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.08, span: 0.55, enterDY: height * 0.1));
        ctx.Add(S.PolygonOutline(main, palette.Ink, 3, 0.9), O(delay: 0.14, span: 0.5, enterDY: height * 0.08));
        double[] scales = [0.4, 0.24];
        for (var index = 0; index < scales.Length; index++)
        {
            var scale = scales[index];
            var x = lean > 0 ? width * (0.88 + index * 0.08) : width * (0.12 - index * 0.08);
            var y = height * (0.24 + index * 0.42);
            var group = ctx.CreateGroup(0, x, y);
            ctx.Add(S.PolygonFill(C.Rotate(C.Heart(0, 0, rx * scale, ry * scale), 0, 0, -lean * 0.22), palette.Tone2, 0.9, ctx.Gradient),
                O(delay: 0.2 + index * 0.06, span: 0.5, enterDX: lean * width * 0.12, drift: true), group);
        }
    }

    private static void SparkleField(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        AddField(ctx, palette.Tone1, 0.88);
        var seeds = C.DiscField(ctx.Seed, 23, width, height, 9, unit * 0.07);
        for (var index = 0; index < Math.Min(5, seeds.Count); index++)
        {
            var disc = seeds[index];
            var star = C.Star(0, 0, disc.Radius, disc.Radius * 0.16, 4, Hash01(ctx.Seed, index, 29) * 0.9);
            var group = ctx.CreateGroup(0, disc.X, disc.Y);
            ctx.Add(S.PolygonFill(star, palette.Tone4, 0.9, ctx.Gradient), O(delay: 0.06 + index * 0.05, span: 0.5, drift: true), group);
        }
        ctx.Add(S.Discs(seeds.Skip(5).Select(disc => disc with { Radius = disc.Radius * 0.22 }).ToList(), palette.Tone4, 0.7),
            O(delay: 0.24, span: 0.55, drift: true));
        ctx.Add(S.Lines([new TemperaLine(width * 0.08, height * 0.74, width * 0.92, height * 0.72)], palette.Tone4, 1.2, 0.5),
            O(delay: 0.3, span: 0.5, enterDX: -width * 0.12));
    }

    private static void PetalArc(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var fromLeft = Hash01(ctx.Seed, 5, 89) > 0.5;
        var hubX = fromLeft ? -width * 0.1 : width * 1.1;
        var hubY = height * 0.12;
        var rx = width * 0.26;
        var ry = height * 0.085;
        AddField(ctx, palette.Tone2);
        for (var index = 0; index < 5; index++)
        {
            var petalBase = C.Ellipse(hubX + (fromLeft ? rx : -rx), hubY, rx, ry, 28);
            var petal = C.Rotate(petalBase, hubX, hubY, (fromLeft ? 1 : -1) * (0.18 + index * 0.3));
            ctx.Add(S.PolygonFill(petal, index % 2 == 0 ? palette.Tone3 : palette.Tone1, 0.9, ctx.Gradient),
                O(delay: index * 0.05, span: 0.55, enterDY: -height * 0.1));
            ctx.Add(S.PolygonOutline(petal, palette.Ink, 1.6, 0.5), O(delay: 0.06 + index * 0.05, span: 0.5));
        }
        var hub = ctx.CreateGroup(0, hubX, hubY);
        ctx.Add(S.PolygonFill(C.Lobed(0, 0, width * 0.11, width * 0.11, 6, 0.34), palette.Tone4, 0.9, ctx.Gradient),
            O(delay: 0.28, span: 0.5, drift: true), hub);
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Discs(Enumerable.Range(0, 3).Select(index => new TemperaDisc(
            fromLeft ? width * (0.78 + index * 0.07) : width * (0.22 - index * 0.07),
            height * (0.7 + index * 0.1),
            height * (0.03 - index * 0.006))).ToList(), palette.Tone4, 0.75, ctx.Gradient), O(delay: 0.32, span: 0.5, drift: true));
    }

    private static void ScallopBand(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddField(ctx, palette.Tone1, 0.9);
        var top = C.ScallopBand(-bleed, width + bleed, -bleed, height * 0.28, 9);
        var bottom = C.ScallopBand(-bleed, width + bleed, height + bleed, height * 0.72, 9);
        ctx.Add(S.PolygonFill(top, palette.Tone3, 0.92, ctx.Gradient), O(delay: 0.04, span: 0.55, enterDY: -height * 0.18));
        ctx.Add(S.PolygonFill(bottom, palette.Tone4, 0.92, ctx.Gradient), O(delay: 0.1, span: 0.55, enterDY: height * 0.18));
        ctx.Add(S.PolygonOutline(top, palette.Ink, 2, 0.75), O(delay: 0.14, span: 0.5, enterDY: -height * 0.12));
        ctx.Add(S.PolygonOutline(bottom, palette.Ink, 2, 0.75), O(delay: 0.18, span: 0.5, enterDY: height * 0.12));
        if (!ctx.ShowDecor) return;
        var pitch = (width + bleed * 2) / 9;
        var beads = Enumerable.Range(0, 5)
            .Select(index => new TemperaDisc(-bleed + pitch * (index + 2), height * 0.28, Math.Min(width, height) * 0.018)).ToList();
        ctx.Add(S.Discs(beads, palette.Ink, 0.7), O(delay: 0.26, drift: true));
    }

    private static void RibbonLoop(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var sag = height * (0.06 + Hash01(ctx.Seed, 7, 97) * 0.05) * (Hash01(ctx.Seed, 9, 97) > 0.5 ? 1 : -1);
        AddField(ctx, palette.Tone2);
        var band = C.ArcRibbon(-bleed, width + bleed, height * 0.5, height * 0.3, sag);
        var echo = C.ArcRibbon(-bleed, width + bleed, height * 0.68, height * 0.06, -sag);
        ctx.Add(S.PolygonFill(echo, palette.Tone4, 0.7, ctx.Gradient), O(delay: 0.04, span: 0.6, enterDX: -width * 0.16));
        ctx.Add(S.PolygonFill(band, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.08, span: 0.55, enterDX: width * 0.18));
        ctx.Add(S.PolygonOutline(band, palette.Ink, 2.6, 0.85), O(delay: 0.14, span: 0.5, enterDX: width * 0.14));
        var knotX = width * (Hash01(ctx.Seed, 11, 97) > 0.5 ? 0.24 : 0.76);
        var knotY = height * 0.5 + Math.Sin((knotX + bleed) / (width + bleed * 2) * Math.PI) * sag;
        var bow = ctx.CreateGroup(0, knotX, knotY);
        double[] sides = [-1, 1];
        for (var index = 0; index < sides.Length; index++)
        {
            var side = sides[index];
            var loop = C.Rotate(C.Ellipse(side * width * 0.055, 0, width * 0.05, height * 0.07, 26), 0, 0, side * 0.3);
            ctx.Add(S.PolygonFill(loop, palette.Tone4, 0.92, ctx.Gradient), O(delay: 0.2 + index * 0.04, span: 0.5, enterDX: side * width * 0.06), bow);
            ctx.Add(S.PolygonOutline(loop, palette.Ink, 2, 0.8), O(delay: 0.24 + index * 0.04, span: 0.5), bow);
        }
        var knot = C.RoundedRect(-width * 0.018, -height * 0.035, width * 0.036, height * 0.07, height * 0.02);
        ctx.Add(S.PolygonFill(knot, palette.Tone4, 0.95, ctx.Gradient), O(delay: 0.28, span: 0.5), bow);
        ctx.Add(S.PolygonOutline(knot, palette.Ink, 2, 0.85), O(delay: 0.3, span: 0.5), bow);
    }

    private static void RoundPlate(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        AddField(ctx, palette.Tone3);
        var plate = C.RoundedRect(width * 0.16, height * 0.24, width * 0.68, height * 0.52, unit * 0.11);
        var inner = C.RoundedRect(width * 0.19, height * 0.29, width * 0.62, height * 0.42, unit * 0.08);
        ctx.Add(S.PolygonFill(plate, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDY: height * 0.06));
        ctx.Add(S.HatchFill(inner, H.Spec(ctx.Seed, 101, 1.3), palette.Tone2, 0.35), O(delay: 0.1, span: 0.6, grow: true));
        ctx.Add(S.PolygonOutline(plate, palette.Ink, 3, 0.9), O(delay: 0.12, span: 0.5));
        ctx.Add(S.PolygonOutline(inner, palette.Tone4, 1.2, 0.55), O(delay: 0.16, span: 0.5));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Discs([
            new TemperaDisc(width * 0.22, height * 0.3, unit * 0.016),
            new TemperaDisc(width * 0.78, height * 0.3, unit * 0.016),
            new TemperaDisc(width * 0.22, height * 0.7, unit * 0.016),
            new TemperaDisc(width * 0.78, height * 0.7, unit * 0.016),
        ], palette.Ink, 0.75), O(delay: 0.22, span: 0.5));
    }

    private static void HaloBurst(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width * (0.44 + Hash01(ctx.Seed, 13, 103) * 0.12);
        var cy = height * 0.5;
        var reach = Math.Sqrt(width * width + height * height) * 0.7;
        const int wedges = 14;
        AddField(ctx, palette.Tone1);
        for (var index = 0; index < wedges; index += 2)
        {
            var angle = index / (double)wedges * Math.PI * 2 + Hash01(ctx.Seed, index, 107) * 0.09;
            const double spread = 0.11;
            ctx.Add(S.PolygonFill([
                cx, cy,
                cx + Math.Cos(angle - spread) * reach, cy + Math.Sin(angle - spread) * reach,
                cx + Math.Cos(angle + spread) * reach, cy + Math.Sin(angle + spread) * reach,
            ], palette.Tone3, 0.55, ctx.Gradient), O(delay: 0.04 + index * 0.012, span: 0.6));
        }
        ctx.Add(S.PolygonFill(C.Ellipse(cx, cy, unit * 0.34, unit * 0.34, 48), palette.Paper, 0.94, ctx.Gradient), O(delay: 0.14, span: 0.55));
        (double Scale, double Stroke)[] rings = [(0.34, 3), (0.44, 1.4)];
        for (var index = 0; index < rings.Length; index++)
            ctx.Add(S.Rings([new TemperaDisc(cx, cy, unit * rings[index].Scale)], palette.Ink, rings[index].Stroke, 0.75),
                O(delay: 0.2 + index * 0.06, span: 0.5, drift: true));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["bubble-drift"] = BubbleDrift;
        registry["cloud-window"] = CloudWindow;
        registry["heart-burst"] = HeartBurst;
        registry["sparkle-field"] = SparkleField;
        registry["petal-arc"] = PetalArc;
        registry["scallop-band"] = ScallopBand;
        registry["ribbon-loop"] = RibbonLoop;
        registry["round-plate"] = RoundPlate;
        registry["halo-burst"] = HaloBurst;
    }
}
