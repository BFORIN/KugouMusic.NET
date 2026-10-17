using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaPosterCompositions.ts: big tilted masses the type has to fight.</summary>
internal static class TemperaPosterCompositions
{
    private static double[] Field(TemperaCompositionContext ctx) =>
        H.Rect(-ctx.Bleed, -ctx.Bleed, ctx.Width + ctx.Bleed * 2, ctx.Height + ctx.Bleed * 2);

    private static void PosterPanel(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var poster = ctx.CreateGroup(-0.06, width / 2, height / 2);
        var solid = H.Diamond(-width * 0.12, 0, width * 0.42, height * 0.66);
        var hatched = H.Diamond(width * 0.2, -height * 0.06, width * 0.3, height * 0.48);
        ctx.Add(S.PolygonFill(solid, palette.Ink, 0.92, ctx.Gradient), O(enterDX: -width * 0.6, span: 0.55), poster);
        ctx.Add(S.HatchFill(hatched, H.Spec(ctx.Seed, 41), palette.Tone4, 0.7), O(delay: 0.08, grow: true, span: 0.55), poster);
        ctx.Add(S.PolygonOutline(hatched, palette.Ink, 2, 0.8), O(delay: 0.12, span: 0.55), poster);
        ctx.Add(S.Polyline(H.Wavy(ctx.Seed, 43, -width * 0.6, width * 0.6, height * 0.42, height * 0.02), palette.Tone4, 2, 0.7),
            O(delay: 0.2, enterDY: height * 0.1), poster);
    }

    private static void DiamondStack(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        TemperaRgb[] tones = [palette.Ink, palette.Tone4, palette.Tone3];
        for (var index = 0; index < tones.Length; index++)
        {
            var scale = 1 - index * 0.28;
            var cx = width * (0.34 + index * 0.24);
            var cy = height * (0.52 - index * 0.14);
            ctx.Add(S.PolygonFill(H.Diamond(cx, cy, width * 0.3 * scale, height * 0.46 * scale), tones[index], 0.93, ctx.Gradient),
                O(delay: index * 0.07, span: 0.55, enterDX: width * 0.25, enterDY: -height * 0.12));
        }
    }

    private static void SlashPoster(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var lean = height * 0.34;
        double[] band =
        [
            -bleed, height * 0.24 + lean,
            width + bleed, height * 0.24 - lean,
            width + bleed, height * 0.72 - lean,
            -bleed, height * 0.72 + lean,
        ];
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(band, palette.Tone4, 0.94, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDX: -width * 0.4));
        ctx.Add(S.HatchFill(band, H.Spec(ctx.Seed, 67) with { Angle = Math.PI / 3 }, palette.Paper, 0.3), O(delay: 0.1, span: 0.55, grow: true));
        ctx.Add(S.PolygonOutline(band, palette.Ink, 2.2, 0.8), O(delay: 0.14, span: 0.5));
    }

    private static void ArrowWedge(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var apex = height * (0.24 + Hash01(ctx.Seed, 3, 71) * 0.14);
        var thickness = height * 0.2;
        double[] chevron =
        [
            -bleed, height + bleed,
            width * 0.5, apex,
            width + bleed, height + bleed,
            width + bleed, height + bleed,
            width * 0.5, apex + thickness,
            -bleed, height + bleed,
        ];
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill([-bleed, height + bleed, width * 0.5, apex, width + bleed, height + bleed], palette.Tone4, 0.94, ctx.Gradient),
            O(delay: 0.05, span: 0.55, enterDY: height * 0.3));
        ctx.Add(S.HatchFill(chevron, H.Spec(ctx.Seed, 73) with { Angle = -Math.PI / 4 }, palette.Paper, 0.35),
            O(delay: 0.1, span: 0.55, grow: true));
        ctx.Add(S.Polyline([-bleed, height * 0.9, width * 0.5, apex - thickness * 0.4, width + bleed, height * 0.9], palette.Ink, 2, 0.7),
            O(delay: 0.16, span: 0.5, enterDY: -height * 0.1));
    }

    private static void EdgeBleed(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var fromLeft = Hash01(ctx.Seed, 4, 79) > 0.5;
        var cover = width * 0.42;
        var mass = fromLeft
            ? H.Rect(-bleed, -bleed, cover + bleed, height + bleed * 2)
            : H.Rect(width - cover, -bleed, cover + bleed, height + bleed * 2);
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(mass, palette.Ink, 0.93, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDX: (fromLeft ? -1 : 1) * width * 0.4));
        ctx.Add(S.HatchFill(mass, H.Spec(ctx.Seed, 83), palette.Paper, 0.22), O(delay: 0.12, span: 0.55, grow: true));
    }

    private static void TriangleMass(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var fromLeft = Hash01(ctx.Seed, 91, 89) > 0.5;
        var apexX = fromLeft ? width * 1.02 : -width * 0.02;
        double[] triangle =
        [
            fromLeft ? -bleed : width + bleed, -bleed,
            fromLeft ? -bleed : width + bleed, height + bleed,
            apexX, height * (0.3 + Hash01(ctx.Seed, 93, 97) * 0.3),
        ];
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(triangle, palette.Tone4, 0.94, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDX: (fromLeft ? -1 : 1) * width * 0.35));
        ctx.Add(S.HatchFill(triangle, H.Spec(ctx.Seed, 101), palette.Paper, 0.24), O(delay: 0.12, span: 0.55, grow: true));
    }

    private static void RibbonCross(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var half = height * 0.14;
        var lean = height * 0.3;
        double[] Ribbon(double sign) =>
        [
            -bleed, height / 2 + sign * lean - half,
            width + bleed, height / 2 - sign * lean - half,
            width + bleed, height / 2 - sign * lean + half,
            -bleed, height / 2 + sign * lean + half,
        ];
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(Ribbon(1), palette.Tone3, 0.8, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDX: -width * 0.3));
        ctx.Add(S.PolygonFill(Ribbon(-1), palette.Tone4, 0.8, ctx.Gradient), O(delay: 0.11, span: 0.55, enterDX: width * 0.3));
    }

    private static void HalfDisc(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var fromRight = Hash01(ctx.Seed, 103, 107) > 0.5;
        var radius = Math.Sqrt(width * width + height * height) * 0.62;
        var disc = H.Circle(fromRight ? width + radius * 0.55 : -radius * 0.55, height * 0.5, radius, 64);
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(disc, palette.Tone4, 0.94, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDX: (fromRight ? 1 : -1) * width * 0.3));
        ctx.Add(S.PolygonOutline(disc, palette.Ink, 2, 0.6), O(delay: 0.12, span: 0.5));
    }

    private static void StackedSlabs(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        TemperaRgb[] tones = [palette.Tone2, palette.Tone3, palette.Tone4];
        for (var index = 0; index < tones.Length; index++)
        {
            var group = ctx.CreateGroup(-0.09 + index * 0.08, width / 2, height / 2);
            var slab = H.Rect(-width * (0.42 - index * 0.05), -height * (0.3 - index * 0.03), width * (0.84 - index * 0.1),
                height * (0.6 - index * 0.06));
            ctx.Add(S.PolygonFill(slab, tones[index], 0.93, ctx.Gradient),
                O(delay: index * 0.07, span: 0.55, enterDX: width * 0.2, enterDY: height * 0.12), group);
        }
    }

    private static void WedgePair(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var waist = width * (0.2 + Hash01(ctx.Seed, 109, 113) * 0.1);
        ctx.Add(S.PolygonFill(Field(ctx), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill([-bleed, -bleed, (width - waist) / 2, height / 2, -bleed, height + bleed], palette.Tone4, 0.94, ctx.Gradient),
            O(delay: 0.05, span: 0.55, enterDX: -width * 0.25));
        ctx.Add(S.PolygonFill([width + bleed, -bleed, (width + waist) / 2, height / 2, width + bleed, height + bleed], palette.Tone4, 0.94,
            ctx.Gradient), O(delay: 0.11, span: 0.55, enterDX: width * 0.25));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["poster-panel"] = PosterPanel;
        registry["diamond-stack"] = DiamondStack;
        registry["slash-poster"] = SlashPoster;
        registry["arrow-wedge"] = ArrowWedge;
        registry["edge-bleed"] = EdgeBleed;
        registry["triangle-mass"] = TriangleMass;
        registry["ribbon-cross"] = RibbonCross;
        registry["half-disc"] = HalfDisc;
        registry["stacked-slabs"] = StackedSlabs;
        registry["wedge-pair"] = WedgePair;
    }
}
