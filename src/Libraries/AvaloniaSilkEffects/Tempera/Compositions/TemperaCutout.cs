using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>
/// Folia compositions/temperaCutout.ts: the plumbing for the families that punch holes through the tone field. A hole is
/// genuinely transparent and shows the shell background; the lyric never sits over one.
/// </summary>
internal static class TemperaCutout
{
    public static double[] FieldPolygon(TemperaCompositionContext ctx) =>
        H.Rect(-ctx.Bleed, -ctx.Bleed, ctx.Width + ctx.Bleed * 2, ctx.Height + ctx.Bleed * 2);

    /// <summary>Full-bleed tone with <paramref name="holes"/> punched through it; hole outlines are separate nodes.</summary>
    public static void AddCutField(TemperaCompositionContext ctx, TemperaRgb color, IReadOnlyList<double[]> holes, double alpha = 0.94,
        double delay = 0, double span = 0.5) =>
        ctx.Add(S.PolygonFillWithHoles(FieldPolygon(ctx), holes, color, alpha, ctx.Gradient), O(delay: delay, span: span));

    /// <summary>The ink lip around a hole, which keeps a cutout reading as a punched plate.</summary>
    public static void AddHoleLip(TemperaCompositionContext ctx, double[] hole, double width = 2.4, double alpha = 0.85, double delay = 0.1) =>
        ctx.Add(S.PolygonOutline(hole, ctx.Palette.Ink, width, alpha), O(delay: delay, span: 0.5));

    /// <summary>A rectangle centred on (cx, cy), <paramref name="length"/> along <paramref name="angle"/> and <paramref name="width"/> across.</summary>
    public static double[] AxisRect(double cx, double cy, double length, double width, double angle) =>
        C.Rotate(H.Rect(cx - length / 2, cy - width / 2, length, width), cx, cy, angle);

    /// <summary>The flow angle pulled halfway back toward its own vertical axis.</summary>
    public static double ChannelAxis(TemperaCompositionContext ctx)
    {
        var axis = (Math.Sin(ctx.FlowAngle) >= 0 ? 1 : -1) * Math.PI / 2;
        return axis + (ctx.FlowAngle - axis) * 0.5;
    }

    /// <summary>Unit vector across the channel axis, always pointing into the +x half of the frame.</summary>
    public static (double X, double Y) AcrossFlow(double angle)
    {
        var across = angle + Math.PI / 2;
        var sign = Math.Cos(across) >= 0 ? 1 : -1;
        return (Math.Cos(across) * sign, Math.Sin(across) * sign);
    }

    public static (double X, double Y) AcrossPoint(TemperaCompositionContext ctx, double offset)
    {
        var across = AcrossFlow(ChannelAxis(ctx));
        return (ctx.Width / 2 + across.X * offset, ctx.Height / 2 + across.Y * offset);
    }

    public static (double X, double Y) FlowPoint(TemperaCompositionContext ctx, double distance, double offset)
    {
        var point = AcrossPoint(ctx, offset);
        var angle = ChannelAxis(ctx);
        return (point.X + Math.Cos(angle) * distance, point.Y + Math.Sin(angle) * distance);
    }

    /// <summary>How far a shape must run to leave the frame on both ends whatever the flow angle.</summary>
    public static double FlowSpan(TemperaCompositionContext ctx) => Math.Sqrt(ctx.Width * ctx.Width + ctx.Height * ctx.Height) * 1.6;
}
