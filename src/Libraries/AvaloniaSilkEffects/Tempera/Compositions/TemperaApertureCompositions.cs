using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaCutout;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaApertureCompositions.ts: one plate, one hole, and the hole's shape is the composition.</summary>
internal static class TemperaApertureCompositions
{
    private static void IrisHole(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width * 0.68;
        var cy = height * 0.42;
        var radius = unit * 0.3;
        var hole = H.Circle(cx, cy, radius, 56);
        AddCutField(ctx, palette.Tone3, [hole]);
        AddHoleLip(ctx, hole, 3, 0.9, 0.08);
        ctx.Add(S.PolygonFill(H.Rect(-ctx.Bleed, cy - unit * 0.035, cx - radius * 0.7 + ctx.Bleed, unit * 0.07), palette.Tone4, 0.9, ctx.Gradient),
            O(delay: 0.14, span: 0.55, enterDX: -width * 0.12));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Rings([new TemperaDisc(cx, cy, radius * 1.14)], palette.Tone4, 1.4, 0.6), O(delay: 0.2, span: 0.5, drift: true));
    }

    private static void SlotRail(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var top = height * 0.22;
        var slot = H.Rect(-bleed, top, width + bleed * 2, height * 0.16);
        AddCutField(ctx, palette.Tone4, [slot]);
        AddHoleLip(ctx, slot, 2.4, 0.8, 0.1);
        ctx.Add(S.PolygonFill(H.Rect(width * 0.66, top - height * 0.06, width * 0.12, height * 0.28), palette.Tone2, 0.95, ctx.Gradient),
            O(delay: 0.16, span: 0.55, enterDY: -height * 0.1));
        ctx.Add(S.Lines([new TemperaLine(-bleed, top + height * 0.22, width + bleed, top + height * 0.22)], palette.Tone2, 1.4, 0.55),
            O(delay: 0.22, span: 0.5, enterDX: width * 0.14));
    }

    private static void PunchRow(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var radius = unit * 0.028;
        var holes = new List<double[]>();
        for (var index = 0; index < 9; index++)
        {
            var x = width * (0.08 + index * 0.105);
            holes.Add(H.Circle(x, height * 0.14, radius, 20));
            holes.Add(H.Circle(x, height * 0.86, radius, 20));
        }
        AddCutField(ctx, palette.Tone3, holes);
        double[] rows = [0.22, 0.78];
        for (var index = 0; index < rows.Length; index++)
            ctx.Add(S.Lines([new TemperaLine(0, height * rows[index], width, height * rows[index])], palette.Tone4, 1.6, 0.6),
                O(delay: 0.12 + index * 0.04, span: 0.5, enterDX: (index == 0 ? 1 : -1) * width * 0.12));
    }

    private static void FilmGate(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var gate = H.Rect(width * 0.2, height * 0.1, width * 0.6, height * 0.52);
        var holes = new List<double[]> { gate };
        for (var index = 0; index < 4; index++)
        {
            var y = height * (0.14 + index * 0.13);
            holes.Add(H.Rect(width * 0.08, y, width * 0.05, height * 0.06));
            holes.Add(H.Rect(width * 0.87, y, width * 0.05, height * 0.06));
        }
        AddCutField(ctx, palette.Tone4, holes);
        AddHoleLip(ctx, gate, 3, 0.9, 0.08);
        ctx.Add(S.HatchFill(H.Rect(width * 0.2, height * 0.68, width * 0.6, height * 0.1), H.Spec(ctx.Seed, 131, 1.2), palette.Tone2, 0.5),
            O(delay: 0.18, span: 0.6, grow: true));
    }

    private static void CrossVent(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var cx = width * 0.5;
        var cy = height * 0.38;
        var arm = Math.Min(width, height) * 0.3;
        var bar = Math.Min(width, height) * 0.1;
        double[] cross =
        [
            cx - bar, cy - arm, cx + bar, cy - arm, cx + bar, cy - bar,
            cx + arm, cy - bar, cx + arm, cy + bar, cx + bar, cy + bar,
            cx + bar, cy + arm, cx - bar, cy + arm, cx - bar, cy + bar,
            cx - arm, cy + bar, cx - arm, cy - bar, cx - bar, cy - bar,
        ];
        AddCutField(ctx, palette.Tone3, [cross]);
        AddHoleLip(ctx, cross, 2.6, 0.85, 0.1);
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Rings([new TemperaDisc(cx, cy, arm * 1.24)], palette.Tone4, 1.2, 0.5), O(delay: 0.2, span: 0.5));
    }

    private static void Louvre(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var holes = new List<double[]>();
        for (var index = 0; index < 6; index++)
        {
            var y = height * (0.08 + index * 0.1);
            var lean = height * 0.03;
            var thickness = height * (0.05 - index * 0.004);
            holes.Add([-bleed, y, width + bleed, y - lean, width + bleed, y - lean + thickness, -bleed, y + thickness]);
        }
        AddCutField(ctx, palette.Tone4, holes);
        ctx.Add(S.PolygonFill(H.Rect(width * 0.1, height * 0.72, width * 0.08, height * 0.2), palette.Tone2, 0.9, ctx.Gradient),
            O(delay: 0.2, span: 0.55, enterDY: height * 0.1));
    }

    private static void RingEye(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width * 0.5;
        var cy = height * 0.5;
        var inner = unit * 0.31;
        var outer = unit * 0.44;
        const int segments = 12;
        const double gap = 0.03;
        var holes = Enumerable.Range(0, segments).Select(index =>
        {
            var start = index / (double)segments * Math.PI * 2 + gap;
            var end = (index + 1) / (double)segments * Math.PI * 2 - gap;
            return C.AnnularSector(cx, cy, inner, outer, start, end, 8);
        }).ToList();
        AddCutField(ctx, palette.Tone3, holes);
        ctx.Add(S.Rings([new TemperaDisc(cx, cy, inner), new TemperaDisc(cx, cy, outer)], palette.Ink, 2.4, 0.85), O(delay: 0.12, span: 0.5));
    }

    private static void NotchStack(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var holes = new List<double[]>();
        for (var index = 0; index < 4; index++)
        {
            var size = unit * (0.26 - index * 0.05);
            holes.Add(H.Rect(width * (0.6 + index * 0.06), height * (0.1 + index * 0.19), size, size));
        }
        AddCutField(ctx, palette.Tone4, holes);
        for (var index = 0; index < holes.Count; index++) AddHoleLip(ctx, holes[index], 2, 0.75, 0.1 + index * 0.04);
    }

    private static void WedgeGap(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var apexX = width * (0.4 + Hash01(ctx.Seed, 3, 137) * 0.2);
        double[] wedge = [width * 0.1, -bleed, width * 0.92, -bleed, apexX, height * 0.56];
        AddCutField(ctx, palette.Tone3, [wedge]);
        AddHoleLip(ctx, wedge, 3, 0.9, 0.08);
        ctx.Add(S.Lines([new TemperaLine(apexX, height * 0.58, apexX, height + bleed)], palette.Tone4, 1.6, 0.6),
            O(delay: 0.18, span: 0.5, enterDY: height * 0.12));
    }

    private static void DotSieve(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var holes = new List<double[]>();
        for (var row = 0; row < 6; row++)
            for (var column = 0; column < 5; column++)
            {
                var density = 1 - column / 4.5;
                if (Hash01(ctx.Seed, row * 9 + column, 139) > density) continue;
                var x = width * (0.06 + column * 0.105) + (row % 2 == 0 ? 0 : width * 0.05);
                holes.Add(H.Circle(x, height * (0.1 + row * 0.16), unit * 0.045 * density + unit * 0.012, 18));
            }
        AddCutField(ctx, palette.Tone2, holes, alpha: 0.92);
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["iris-hole"] = IrisHole;
        registry["slot-rail"] = SlotRail;
        registry["punch-row"] = PunchRow;
        registry["film-gate"] = FilmGate;
        registry["cross-vent"] = CrossVent;
        registry["louvre-slats"] = Louvre;
        registry["ring-eye"] = RingEye;
        registry["notch-stack"] = NotchStack;
        registry["wedge-gap"] = WedgeGap;
        registry["dot-sieve"] = DotSieve;
    }
}
