using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaFrameCompositions.ts: outlined windows that hold the type in a quiet pocket.</summary>
internal static class TemperaFrameCompositions
{
    private static void FrameWindow(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var cx = width / 2;
        var cy = height / 2;
        var rx = width * 0.42;
        var ry = height * 0.44;
        ctx.Add(S.HatchFill(H.Diamond(cx, cy, rx * 0.78, ry * 0.78), H.Spec(ctx.Seed, 23, 1.4), palette.Tone2, 0.45), O(grow: true, span: 0.6));
        ctx.Add(S.ConcentricDiamonds(cx, cy, rx, ry, 3, palette.Ink, 0.9), O(delay: 0.08, enterDY: height * 0.06, span: 0.55));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.CrossMarks(H.CrossRow(ctx.Seed, 29, width * 0.08, height * 0.14, 3, width * 0.045, 8), palette.Tone4, 1.8, 0.85), O(delay: 0.22));
        ctx.Add(S.SquareMarks(H.DotRow(ctx.Seed, 37, width * 0.92, height * 0.62, 4, height * 0.06, 7), palette.Tone4, 0.8), O(delay: 0.28));
    }

    private static void DoubleFrame(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var offset = width * 0.05;
        var box = H.Rect(width * 0.16, height * 0.2, width * 0.68, height * 0.6);
        var shifted = H.Rect(width * 0.16 + offset, height * 0.2 + offset * 0.6, width * 0.68, height * 0.6);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(shifted, palette.Tone3, 0.8, ctx.Gradient), O(delay: 0.06, span: 0.55, enterDX: offset * 3));
        ctx.Add(S.HatchFill(shifted, H.Spec(ctx.Seed, 43), palette.Tone4, 0.4), O(delay: 0.1, span: 0.55, grow: true));
        ctx.Add(S.PolygonOutline(box, palette.Ink, 3.5, 0.9), O(delay: 0.14, span: 0.5, enterDY: -height * 0.08));
    }

    private static void CircleWindow(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var radius = Math.Min(width, height) * 0.34;
        var circle = H.Circle(width / 2, height / 2, radius);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone3, 0.92, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.HatchFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), H.Spec(ctx.Seed, 47), palette.Tone4, 0.4),
            O(delay: 0.04, span: 0.6, grow: true));
        ctx.Add(S.PolygonFill(circle, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.08, span: 0.55));
        ctx.Add(S.PolygonOutline(circle, palette.Ink, 3, 0.9), O(delay: 0.14, span: 0.5));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.PolygonOutline(H.Circle(width / 2, height / 2, radius * 1.12), palette.Ink, 1.2, 0.55), O(delay: 0.2, drift: true));
    }

    private static void LadderFrame(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var left = width * 0.14;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        const int steps = 5;
        for (var index = 0; index < steps; index++)
        {
            var y = height * (0.18 + index * 0.14);
            var run = width * (0.06 + index * 0.03);
            ctx.Add(S.Lines([new TemperaLine(left, y, left + run, y), new TemperaLine(left, y, left, y + height * 0.14)],
                palette.Tone4, index % 2 == 0 ? 3 : 1.4, 0.85), O(delay: index * 0.05, span: 0.5, enterDX: -width * 0.1));
        }
        ctx.Add(S.Lines([new TemperaLine(width * 0.9, -bleed, width * 0.9, height + bleed)], palette.Tone4, 1.4, 0.6),
            O(delay: 0.28, span: 0.5, enterDY: height * 0.2));
    }

    private static void CornerBrackets(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var inset = Math.Min(width, height) * 0.12;
        var arm = Math.Min(width, height) * 0.16;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone2, 0.9, ctx.Gradient), O(span: 0.5));
        (double X, double Y, double Sx, double Sy)[] corners =
        [
            (inset, inset, 1, 1),
            (width - inset, inset, -1, 1),
            (inset, height - inset, 1, -1),
            (width - inset, height - inset, -1, -1),
        ];
        for (var index = 0; index < corners.Length; index++)
        {
            var (x, y, sx, sy) = corners[index];
            ctx.Add(S.Lines([new TemperaLine(x, y, x + sx * arm, y), new TemperaLine(x, y, x, y + sy * arm)], palette.Ink, 3.5, 0.9),
                O(delay: index * 0.05, span: 0.5, enterDX: sx * width * 0.06, enterDY: sy * height * 0.06));
        }
        if (!ctx.ShowDecor) return;
        var dotted = Hash01(ctx.Seed, 2, 53) > 0.5;
        ctx.Add(dotted
            ? S.SquareMarks(H.DotRow(ctx.Seed, 59, width * 0.5, height * 0.16, 3, width * 0.03, 6, 0), palette.Tone4, 0.8)
            : S.CrossMarks(H.CrossRow(ctx.Seed, 61, width * 0.44, height * 0.84, 3, width * 0.04, 7), palette.Tone4, 1.8, 0.8),
            O(delay: 0.24, drift: true));
    }

    private static void InsetBox(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var box = H.Rect(width * 0.14, height * 0.2, width * 0.72, height * 0.6);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        ctx.Add(S.PolygonFill(box, palette.Tone3, 0.55, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDY: height * 0.06));
        ctx.Add(S.PolygonOutline(box, palette.Ink, 2.4, 0.85), O(delay: 0.1, span: 0.5));
    }

    private static void BracketPair(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var inset = width * 0.16;
        var arm = width * 0.09;
        var top = height * 0.26;
        var bottom = height * 0.74;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone2, 0.9, ctx.Gradient), O(span: 0.5));
        double[] sides = [1, -1];
        for (var index = 0; index < sides.Length; index++)
        {
            var side = sides[index];
            var x = side == 1 ? inset : width - inset;
            ctx.Add(S.Lines([
                new TemperaLine(x, top, x + side * arm, top),
                new TemperaLine(x, top, x, bottom),
                new TemperaLine(x, bottom, x + side * arm, bottom),
            ], palette.Ink, 4, 0.9), O(delay: index * 0.07, span: 0.5, enterDX: side * width * 0.08));
        }
    }

    private static void ArchWindow(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var radius = width * 0.24;
        var cx = width / 2;
        var shoulder = height * 0.42;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone3, 0.92, ctx.Gradient), O(span: 0.5));
        var arch = H.Circle(cx, shoulder, radius, 48);
        var body = H.Rect(cx - radius, shoulder, radius * 2, height * 0.4);
        ctx.Add(S.PolygonFill(arch, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDY: -height * 0.08));
        ctx.Add(S.PolygonFill(body, palette.Paper, 0.95, ctx.Gradient), O(delay: 0.05, span: 0.55, enterDY: height * 0.08));
        ctx.Add(S.PolygonOutline(arch, palette.Ink, 2.4, 0.8), O(delay: 0.12, span: 0.5));
        ctx.Add(S.PolygonOutline(body, palette.Ink, 2.4, 0.8), O(delay: 0.12, span: 0.5));
    }

    private static void GridCells(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone1, 0.9, ctx.Gradient), O(span: 0.5));
        var left = width * 0.12;
        var top = height * 0.18;
        var cellWidth = width * 0.76 / 3;
        var cellHeight = height * 0.64 / 3;
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                var cell = H.Rect(left + cellWidth * column, top + cellHeight * row, cellWidth, cellHeight);
                var index = row * 3 + column;
                if (row == 1 && column == 1)
                    ctx.Add(S.PolygonFill(cell, palette.Tone4, 0.75, ctx.Gradient), O(delay: index * 0.03, span: 0.5));
                ctx.Add(S.PolygonOutline(cell, palette.Tone4, 1.2, 0.6), O(delay: index * 0.03, span: 0.5));
            }
    }

    private static void Keyhole(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var radius = Math.Min(width, height) * 0.19;
        var cx = width / 2;
        var cy = height * 0.36;
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), palette.Tone4, 0.94, ctx.Gradient), O(span: 0.5));
        var head = H.Circle(cx, cy, radius, 48);
        var shaft = H.Rect(cx - radius * 0.55, cy, radius * 1.1, height * 0.46);
        ctx.Add(S.PolygonFill(head, palette.Paper, 0.96, ctx.Gradient), O(delay: 0.05, span: 0.55));
        ctx.Add(S.PolygonFill(shaft, palette.Paper, 0.96, ctx.Gradient), O(delay: 0.08, span: 0.55, enterDY: height * 0.1));
        ctx.Add(S.PolygonOutline(head, palette.Ink, 2, 0.7), O(delay: 0.14, span: 0.5));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["frame-window"] = FrameWindow;
        registry["double-frame"] = DoubleFrame;
        registry["circle-window"] = CircleWindow;
        registry["ladder-frame"] = LadderFrame;
        registry["corner-brackets"] = CornerBrackets;
        registry["inset-box"] = InsetBox;
        registry["bracket-pair"] = BracketPair;
        registry["arch-window"] = ArchWindow;
        registry["grid-cells"] = GridCells;
        registry["keyhole"] = Keyhole;
    }
}
