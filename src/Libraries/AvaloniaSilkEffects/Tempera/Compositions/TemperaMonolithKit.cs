using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaMonolithKit.ts: one enormous matte mass cropped by the frame, hairlines across the whole
/// picture, a ruled face, survey ticks and a loose wire trace — all deliberately thin.
/// </summary>
internal static class TemperaMonolithKit
{
    public static void AddMass(TemperaCompositionContext ctx, double[] polygon, TemperaRgb color, double alpha = 0.95, double delay = 0.04,
        double span = 0.6, double enterDX = 0, double enterDY = 0, bool edge = true, double edgeWidth = 2.4)
    {
        ctx.Add(S.PolygonFill(polygon, color, alpha, ctx.Gradient), O(delay: delay, span: span, enterDX: enterDX, enterDY: enterDY));
        if (!edge) return;
        ctx.Add(S.PolygonOutline(polygon, ctx.Palette.Ink, edgeWidth, 0.8),
            O(delay: delay + 0.06, span: 0.5, enterDX: enterDX * 0.6, enterDY: enterDY * 0.6));
    }

    public static void AddGround(TemperaCompositionContext ctx, TemperaRgb color, double alpha = 0.94) =>
        ctx.Add(S.PolygonFill(H.Rect(-ctx.Bleed, -ctx.Bleed, ctx.Width + ctx.Bleed * 2, ctx.Height + ctx.Bleed * 2), color, alpha, ctx.Gradient),
            O(span: 0.5));

    /// <summary>Ruled stripes over one convex face of a mass.</summary>
    public static void AddFaceRuling(TemperaCompositionContext ctx, double[] face, long salt, TemperaRgb? color = null, double alpha = 0.5) =>
        ctx.Add(S.HatchFill(face, H.Spec(ctx.Seed, salt, 0.7), color ?? ctx.Palette.Tone4, alpha), O(delay: 0.16, span: 0.65, grow: true));

    /// <summary>Two or three hairlines ruled across the entire picture at shallow angles.</summary>
    public static void AddSurveyLines(TemperaCompositionContext ctx, int count = 3, int salt = 211)
    {
        var (width, height, bleed) = (ctx.Width, ctx.Height, ctx.Bleed);
        var lines = Enumerable.Range(0, Math.Max(1, count)).Select(index =>
        {
            // Folia reads the unwrapped seed here: shot seeds are hash + index·97 and can pass 2^32.
            var anchor = height * (0.16 + (index * 0.31 + ctx.Seed % 7 * 0.04) % 0.7);
            var lean = height * (index % 2 == 0 ? 0.22 : -0.16);
            return new TemperaLine(-bleed, anchor - lean, width + bleed, anchor + lean);
        });
        ctx.Add(S.Lines(lines, ctx.Palette.Paper, 1, 0.45), O(delay: 0.2 + salt % 3 * 0.02, span: 0.7, enterDX: width * 0.2));
    }

    /// <summary>Survey marks in two opposite corners.</summary>
    public static void AddCornerTicks(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var inset = Math.Min(width, height) * 0.06;
        var arm = Math.Min(width, height) * 0.05;
        (int Sx, int Sy)[] corners = [(1, 1), (-1, -1)];
        for (var index = 0; index < corners.Length; index++)
        {
            var (sx, sy) = corners[index];
            var x = sx > 0 ? inset : width - inset;
            var y = sy > 0 ? inset : height - inset;
            ctx.Add(S.Lines([new TemperaLine(x, y, x + sx * arm, y), new TemperaLine(x, y, x, y + sy * arm)], palette.Paper, 1.6, 0.6),
                O(delay: 0.26 + index * 0.04, span: 0.5));
        }
    }

    /// <summary>A loose wire contour parked in a corner, drawn in local coordinates of a positioned group.</summary>
    public static void AddWireTrace(TemperaCompositionContext ctx, double cx, double cy, double radius)
    {
        var group = ctx.CreateGroup(0, cx, cy);
        ctx.Add(S.Polyline(H.Scribble(ctx.Decor.ScribbleSeed, 199, 0, 0, radius, 1), ctx.Palette.Paper, 1.2, 0.4),
            O(delay: 0.3, span: 0.6, drift: true), group);
    }
}
