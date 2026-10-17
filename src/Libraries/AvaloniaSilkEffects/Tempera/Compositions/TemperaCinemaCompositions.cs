using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaCinemaCompositions.ts: a solid matte with a window of a real pixel aspect, so a square window is
/// square on any display. The matte is four convex bars, which the hatch clipper needs.
/// </summary>
internal static class TemperaCinemaCompositions
{
    private readonly record struct Window(double X, double Y, double Width, double Height);

    private static Window Fit(double width, double height, double aspect, double fill)
    {
        var windowWidth = Math.Min(width * fill, height * fill * aspect);
        var windowHeight = windowWidth / aspect;
        return new Window((width - windowWidth) / 2, (height - windowHeight) / 2, windowWidth, windowHeight);
    }

    private static void AddMatte(TemperaCompositionContext ctx, Window hole, TemperaRgb tone)
    {
        var (width, height, bleed) = (ctx.Width, ctx.Height, ctx.Bleed);
        (double[] Polygon, double EnterDX, double EnterDY)[] bars =
        [
            (H.Rect(-bleed, -bleed, width + bleed * 2, hole.Y + bleed), 0, -height * 0.2),
            (H.Rect(-bleed, hole.Y + hole.Height, width + bleed * 2, height - hole.Y - hole.Height + bleed), 0, height * 0.2),
            (H.Rect(-bleed, hole.Y, hole.X + bleed, hole.Height), -width * 0.2, 0),
            (H.Rect(hole.X + hole.Width, hole.Y, width - hole.X - hole.Width + bleed, hole.Height), width * 0.2, 0),
        ];
        for (var index = 0; index < bars.Length; index++)
            ctx.Add(S.PolygonFill(bars[index].Polygon, tone, 0.96, ctx.Gradient),
                O(delay: index * 0.04, span: 0.5, enterDX: bars[index].EnterDX, enterDY: bars[index].EnterDY));
        ctx.Add(S.PolygonOutline(H.Rect(hole.X, hole.Y, hole.Width, hole.Height), ctx.Palette.Ink, 1.6, 0.5), O(delay: 0.2, span: 0.5));
    }

    private static void AddWindowWash(TemperaCompositionContext ctx, Window hole, double alpha)
    {
        var polygon = H.Rect(hole.X, hole.Y, hole.Width, hole.Height);
        ctx.Add(S.PolygonFill(polygon, ctx.Palette.Tone1, alpha, ctx.Gradient), O(span: 0.55));
        ctx.Add(S.HatchFill(polygon, H.Spec(ctx.Seed, 149, 1.5), ctx.Palette.Tone4, 0.2), O(delay: 0.1, span: 0.6, grow: true));
    }

    private static Action<TemperaCompositionContext> Matte(double aspect, double baseFill, double washAlpha = 0.35) => ctx =>
    {
        // A slightly off-size window drifts by seed, so the same aspect never lands identically.
        var fill = baseFill + (Hash01(ctx.Seed, 157, 163) - 0.5) * 0.06;
        var hole = Fit(ctx.Width, ctx.Height, aspect, fill);
        AddWindowWash(ctx, hole, washAlpha);
        AddMatte(ctx, hole, ctx.Palette.Tone4);
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Lines([
            new TemperaLine(hole.X, hole.Y - ctx.Height * 0.05, hole.X, hole.Y - ctx.Height * 0.02),
            new TemperaLine(hole.X + hole.Width, hole.Y + hole.Height + ctx.Height * 0.02, hole.X + hole.Width,
                hole.Y + hole.Height + ctx.Height * 0.05),
        ], ctx.Palette.Paper, 2, 0.6), O(delay: 0.26, span: 0.5));
    };

    private static void CinemaTwin(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var inset = height * 0.16;
        var gutter = width * 0.04;
        var left = new Window(width * 0.08, inset, width * 0.5, height - inset * 2);
        var right = new Window(left.X + left.Width + gutter, inset + height * 0.1, width * 0.28, height - inset * 2 - height * 0.2);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone4, 0.96, ctx.Gradient), O(span: 0.5));
        Window[] holes = [left, right];
        for (var index = 0; index < holes.Length; index++)
        {
            var hole = holes[index];
            var polygon = H.Rect(hole.X, hole.Y, hole.Width, hole.Height);
            ctx.Add(S.PolygonFill(polygon, palette.Tone1, index == 0 ? 0.4 : 0.85, ctx.Gradient),
                O(delay: index * 0.08, span: 0.55, enterDX: (index == 0 ? -1 : 1) * width * 0.14));
            ctx.Add(S.PolygonOutline(polygon, palette.Ink, 1.6, 0.5), O(delay: 0.16 + index * 0.05, span: 0.5));
        }
        if (!ctx.ShowDecor) return;
        ctx.Add(S.HatchFill(H.Rect(right.X, right.Y, right.Width, right.Height), H.Spec(ctx.Seed, 151), palette.Tone4, 0.35),
            O(delay: 0.24, span: 0.6, grow: true));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["cinema-scope"] = Matte(2.39, 0.88);
        registry["cinema-wide"] = Matte(1.85, 0.84);
        registry["cinema-academy"] = Matte(1.33, 0.78);
        registry["cinema-square"] = Matte(1, 0.72);
        registry["cinema-portrait"] = Matte(0.75, 0.72);
        registry["cinema-tall"] = Matte(0.5625, 0.72);
        registry["cinema-twin"] = CinemaTwin;
    }
}
