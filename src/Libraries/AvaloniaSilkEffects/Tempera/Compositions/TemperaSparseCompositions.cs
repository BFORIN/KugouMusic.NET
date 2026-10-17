using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaSparseCompositions.ts: hairlines, dot fields and hand-drawn strokes for breathing paragraphs.</summary>
internal static class TemperaSparseCompositions
{
    /// <summary>JavaScript's <c>%</c> on doubles (sign of the dividend).</summary>
    private static double JsMod(double value, double divisor) => value % divisor;

    private static void QuietLine(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var gridWidth = width * 0.7;
        var gridX = (width - gridWidth) / 2;
        double[] ratios = [0.38, 0.5, 0.62];
        for (var index = 0; index < ratios.Length; index++)
            ctx.Add(S.PolygonFill(H.Rect(gridX, height * ratios[index], gridWidth, 1), palette.Line, 1, ctx.Gradient),
                O(delay: index * 0.08, span: 0.6, enterDX: (index % 2 == 0 ? -1 : 1) * width * 0.2));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Polyline(H.Scribble(ctx.Decor.ScribbleSeed, 47, width * 0.2, height * 0.26, Math.Min(width, height) * 0.09, 2),
            palette.Tone4, 1.6, 0.7), O(delay: 0.24, span: 0.6));
        var tuftX = width * 0.82;
        var tuftY = height * 0.74;
        ctx.Add(S.Lines(Enumerable.Range(0, 6).Select(index =>
        {
            var lean = (Hash01(ctx.Decor.ScribbleSeed, index, 59) - 0.5) * 34;
            return new TemperaLine(tuftX + index * 7, tuftY, tuftX + index * 7 + lean, tuftY - 24 - index * 3);
        }), palette.Tone4, 1.4, 0.7), O(delay: 0.3, span: 0.55));
    }

    private static void StarfieldDots(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var spacing = Math.Max(24, Math.Sqrt(width * height / 900));
        var marks = H.DotGrid(width + bleed, height + bleed, spacing, 2.6);
        var dense = marks.Where(mark => mark.Y > height * 0.45).ToList();
        var sparse = marks.Where(mark => mark.Y <= height * 0.45 && JsMod(mark.X + mark.Y, 3) < 1).ToList();
        ctx.Add(S.SquareMarks(dense, palette.Tone4, 0.45), O(span: 0.6, enterDY: height * 0.2));
        ctx.Add(S.SquareMarks(sparse, palette.Tone4, 0.25), O(delay: 0.08, span: 0.6, enterDY: -height * 0.15));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Lines([new TemperaLine(-bleed, height * 0.45, width + bleed, height * 0.45)], palette.Tone4, 1.2, 0.5),
            O(delay: 0.2, span: 0.5, enterDX: width * 0.2));
    }

    private static void RippleLines(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        const int count = 7;
        for (var index = 0; index < count; index++)
        {
            var y = height * (0.16 + index * 0.11);
            var amplitude = height * (0.006 + index * 0.004);
            ctx.Add(S.Polyline(H.Wavy(ctx.Seed, 89 + index, -bleed, width + bleed, y, amplitude, 26), palette.Tone4,
                index % 3 == 0 ? 2 : 1.1, 0.55), O(delay: index * 0.045, span: 0.6, enterDX: (index % 2 == 0 ? -1 : 1) * width * 0.15));
        }
    }

    private static void HairGrid(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        const int columns = 6;
        const int rows = 4;
        var vertical = Enumerable.Range(0, columns + 1)
            .Select(index => new TemperaLine(width / columns * index, -bleed, width / columns * index, height + bleed));
        var horizontal = Enumerable.Range(0, rows + 1)
            .Select(index => new TemperaLine(-bleed, height / rows * index, width + bleed, height / rows * index));
        ctx.Add(S.Lines(vertical, palette.Line, 1, 0.8), O(span: 0.6, enterDY: -height * 0.1));
        ctx.Add(S.Lines(horizontal, palette.Line, 1, 0.8), O(delay: 0.08, span: 0.6, enterDX: width * 0.1));
    }

    private static void MarginRule(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var fromLeft = Hash01(ctx.Seed, 121, 127) > 0.5;
        var x = fromLeft ? width * 0.12 : width * 0.88;
        ctx.Add(S.PolygonFill(H.Rect(x - 3, -bleed, 6, height + bleed * 2), palette.Tone4, 0.9, ctx.Gradient), O(span: 0.6, enterDY: height * 0.15));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.SquareMarks(Enumerable.Range(0, 3).Select(index =>
            new TemperaMark(x + (fromLeft ? 22 : -22), height * (0.32 + index * 0.18), 7, 0)), palette.Tone4, 0.8), O(delay: 0.22, drift: true));
    }

    private static void DotDrift(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var spacing = Math.Max(22, Math.Sqrt(width * height / 1200));
        var marks = H.DotGrid(width + bleed, height + bleed, spacing, 2.4);
        var span = width + height;
        var near = marks.Where(mark => mark.X + mark.Y > span * 0.5).ToList();
        var far = marks.Where(mark => mark.X + mark.Y <= span * 0.5 && JsMod(mark.X + mark.Y, 2) < 1).ToList();
        ctx.Add(S.SquareMarks(near, palette.Tone4, 0.5), O(span: 0.6, enterDX: width * 0.12));
        ctx.Add(S.SquareMarks(far, palette.Tone4, 0.24), O(delay: 0.08, span: 0.6, enterDX: -width * 0.12));
    }

    private static void ArcSweep(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var cx = width * (Hash01(ctx.Seed, 131, 137) > 0.5 ? 1.15 : -0.15);
        var cy = height * 1.05;
        var radiusBase = Math.Sqrt(width * width + height * height) * 0.42;
        for (var index = 0; index < 5; index++)
            ctx.Add(S.PolygonOutline(H.Circle(cx, cy, radiusBase + index * radiusBase * 0.22, 72), palette.Tone4, index % 2 == 0 ? 1.8 : 1, 0.55),
                O(delay: index * 0.05, span: 0.6, enterDY: height * 0.08));
    }

    private static void BlankPage(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        ctx.Add(S.Lines([new TemperaLine(width * 0.18, height * 0.7, width * 0.36, height * 0.7)], palette.Line, 1.4, 0.8),
            O(span: 0.6, enterDX: -width * 0.12));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.Polyline(H.Scribble(ctx.Decor.ScribbleSeed, 139, width * 0.82, height * 0.26, Math.Min(width, height) * 0.06, 2),
            palette.Tone4, 1.4, 0.6), O(delay: 0.28, span: 0.6));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["quiet-line"] = QuietLine;
        registry["starfield-dots"] = StarfieldDots;
        registry["ripple-lines"] = RippleLines;
        registry["hair-grid"] = HairGrid;
        registry["margin-rule"] = MarginRule;
        registry["dot-drift"] = DotDrift;
        registry["arc-sweep"] = ArcSweep;
        registry["blank-page"] = BlankPage;
    }
}
