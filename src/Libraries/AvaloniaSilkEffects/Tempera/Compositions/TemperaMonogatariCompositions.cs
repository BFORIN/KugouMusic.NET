using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaMonogatariCompositions.ts: one flat field edge to edge, and the type is the picture.</summary>
internal static class TemperaMonogatariCompositions
{
    private static void AddField(TemperaCompositionContext ctx, TemperaRgb tone)
    {
        var (width, height, bleed) = (ctx.Width, ctx.Height, ctx.Bleed);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), tone, 1, ctx.Gradient), O(span: 0.45));
    }

    private static TemperaRgb PickField(TemperaCompositionContext ctx) =>
        Hash01(ctx.Seed, 167, 173) > 0.5 ? ctx.Palette.Tone4 : ctx.Palette.Tone1;

    private static void Card(TemperaCompositionContext ctx)
    {
        AddField(ctx, PickField(ctx));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Lines([new TemperaLine(ctx.Width * 0.12, ctx.Height * 0.9, ctx.Width * 0.88, ctx.Height * 0.9)], ctx.Palette.Paper, 1.2, 0.45),
            O(delay: 0.22, span: 0.6, enterDX: ctx.Width * 0.15));
    }

    private static void Rule(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddField(ctx, PickField(ctx));
        var y = height * (0.62 + Hash01(ctx.Seed, 179, 181) * 0.08);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, y, width + bleed * 2, height * 0.012), palette.Paper, 0.85, ctx.Gradient),
            O(delay: 0.12, span: 0.55, enterDX: -width * 0.3));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Lines([new TemperaLine(-bleed, y + height * 0.05, width + bleed, y + height * 0.05)], palette.Paper, 1, 0.35),
            O(delay: 0.2, span: 0.6, enterDX: width * 0.2));
    }

    private static void Edge(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var field = PickField(ctx);
        AddField(ctx, field);
        var band = width * (0.1 + Hash01(ctx.Seed, 191, 193) * 0.05);
        var fromLeft = Hash01(ctx.Seed, 197, 199) > 0.5;
        var spine = H.Rect(fromLeft ? -bleed : width - band, -bleed, band + bleed, height + bleed * 2);
        ctx.Add(S.PolygonFill(spine, field == palette.Tone4 ? palette.Tone1 : palette.Tone4, 0.95, ctx.Gradient),
            O(delay: 0.1, span: 0.55, enterDX: (fromLeft ? -1 : 1) * width * 0.2));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.HatchFill(spine, H.Spec(ctx.Seed, 211), palette.Paper, 0.2), O(delay: 0.18, span: 0.6, grow: true));
    }

    private static void Stack(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddField(ctx, PickField(ctx));
        var column = width * 0.5;
        ctx.Add(S.Lines([
            new TemperaLine((width - column) / 2, -bleed, (width - column) / 2, height + bleed),
            new TemperaLine((width + column) / 2, -bleed, (width + column) / 2, height + bleed),
        ], palette.Paper, 1.1, 0.4), O(delay: 0.14, span: 0.6, enterDY: height * 0.12));
    }

    private static void Flash(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        AddField(ctx, palette.Tone4);
        if (!ctx.ShowDecor) return;
        ctx.Add(S.CrossMarks(H.CrossRow(ctx.Seed, 223, width * 0.1, height * 0.18, 4, width * 0.05, 10), palette.Paper, 2.4, 0.6),
            O(delay: 0.2, span: 0.5, enterDX: -width * 0.1));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["monogatari-card"] = Card;
        registry["monogatari-rule"] = Rule;
        registry["monogatari-edge"] = Edge;
        registry["monogatari-stack"] = Stack;
        registry["monogatari-flash"] = Flash;
    }
}
