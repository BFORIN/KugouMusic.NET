using AvaloniaSilkEffects.Tempera.Geometry;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaBlock;
using static AvaloniaSilkEffects.Tempera.Compositions.TemperaCutout;
using static AvaloniaSilkEffects.Tempera.TemperaRandom;
using C = AvaloniaSilkEffects.Tempera.Geometry.TemperaCurves;
using H = AvaloniaSilkEffects.Tempera.Geometry.TemperaHatch;
using S = AvaloniaSilkEffects.Tempera.Rendering.TemperaShapes;

namespace AvaloniaSilkEffects.Tempera.Compositions;

/// <summary>Folia compositions/temperaSignalCompositions.ts: measured geometric decoration around one heavy focal mass.</summary>
internal static class TemperaSignalCompositions
{
    private static void AddField(TemperaCompositionContext ctx, TemperaRgb color, double alpha = 0.92)
    {
        var (width, height, bleed) = (ctx.Width, ctx.Height, ctx.Bleed);
        ctx.Add(S.PolygonFill(H.Rect(-bleed, -bleed, width + bleed * 2, height + bleed * 2), color, alpha, ctx.Gradient), O(span: 0.5));
    }

    private static void SightMark(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        var cx = width * 0.5;
        var cy = height * 0.5;
        AddField(ctx, palette.Tone1);
        ctx.Add(S.Lines([new TemperaLine(-bleed, cy, width + bleed, cy), new TemperaLine(cx, -bleed, cx, height + bleed)], palette.Tone4, 1.4, 0.6),
            O(delay: 0.04, span: 0.55, enterDX: width * 0.1));
        ctx.Add(S.PolygonFill(H.Rect(width * 0.24, height * 0.36, width * 0.52, height * 0.28), palette.Tone3, 0.94, ctx.Gradient),
            O(delay: 0.1, span: 0.55, enterDY: height * 0.06));
        ctx.Add(S.PolygonOutline(H.Rect(width * 0.2, height * 0.32, width * 0.6, height * 0.36), palette.Ink, 2.4, 0.85), O(delay: 0.16, span: 0.5));
        if (!ctx.ShowDecor) return;
        (double Fx, double Fy, double Sx, double Sy)[] ticks = [(0.2, 0.32, 1, 1), (0.8, 0.32, -1, 1), (0.2, 0.68, 1, -1), (0.8, 0.68, -1, -1)];
        for (var index = 0; index < ticks.Length; index++)
        {
            var (fx, fy, sx, sy) = ticks[index];
            var x = width * fx;
            var y = height * fy;
            ctx.Add(S.Lines([new TemperaLine(x, y, x + sx * width * 0.04, y), new TemperaLine(x, y, x, y + sy * height * 0.05)], palette.Ink, 3, 0.8),
                O(delay: 0.2 + index * 0.03, span: 0.5));
        }
    }

    private static void DialScale(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width * 0.5;
        var cy = height * 0.5;
        AddField(ctx, palette.Tone2);
        ctx.Add(S.PolygonFill(C.AnnularSector(cx, cy, unit * 0.34, unit * 0.44, 0, Math.PI * 2 - 0.006, 60), palette.Tone4, 0.92, ctx.Gradient),
            O(delay: 0.05, span: 0.6));
        var ticks = Enumerable.Range(0, 24).Select(index =>
        {
            var angle = index / 24d * Math.PI * 2;
            var inner = unit * (index % 6 == 0 ? 0.46 : 0.475);
            return new TemperaLine(cx + Math.Cos(angle) * inner, cy + Math.Sin(angle) * inner,
                cx + Math.Cos(angle) * unit * 0.5, cy + Math.Sin(angle) * unit * 0.5);
        });
        ctx.Add(S.Lines(ticks, palette.Ink, 1.6, 0.7), O(delay: 0.14, span: 0.55));
        var pointer = 0.4 + Hash01(ctx.Seed, 5, 149) * 0.5;
        ctx.Add(S.PolygonFill(AxisRect(cx + Math.Cos(pointer * Math.PI * 2) * unit * 0.2, cy + Math.Sin(pointer * Math.PI * 2) * unit * 0.2,
            unit * 0.4, unit * 0.035, pointer * Math.PI * 2), palette.Ink, 0.9), O(delay: 0.2, span: 0.5));
    }

    private static void ChevronRun(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        const int focus = 2;
        AddField(ctx, palette.Tone1);
        for (var index = 0; index < 5; index++)
        {
            var y = height * (0.02 + index * 0.24);
            var rise = height * 0.12;
            var thickness = height * 0.07;
            double[] chevron =
            [
                -bleed, y, width * 0.5, y - rise, width + bleed, y,
                width + bleed, y + thickness, width * 0.5, y - rise + thickness, -bleed, y + thickness,
            ];
            ctx.Add(S.PolygonFill(chevron, index == focus ? palette.Tone4 : palette.Tone3, index == focus ? 0.95 : 0.7, ctx.Gradient),
                O(delay: index * 0.04, span: 0.55, enterDY: -height * 0.12));
        }
    }

    private static void TallyColumn(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var x = width * 0.82;
        AddField(ctx, palette.Tone1);
        var marks = Enumerable.Range(0, 11).Select(index =>
        {
            var isLong = index % 4 == 0;
            return new TemperaLine(x, height * (0.1 + index * 0.075), x + width * (isLong ? 0.12 : 0.06), height * (0.1 + index * 0.075));
        });
        ctx.Add(S.Lines(marks, palette.Tone4, 2.4, 0.75), O(delay: 0.06, span: 0.6, enterDX: width * 0.1));
        ctx.Add(S.PolygonFill(H.Rect(x - width * 0.02, height * 0.4, width * 0.16, height * 0.12), palette.Tone4, 0.95, ctx.Gradient),
            O(delay: 0.16, span: 0.55, enterDX: width * 0.14));
        ctx.Add(S.Lines([new TemperaLine(x, height * 0.06, x, height * 0.92)], palette.Ink, 1.6, 0.6), O(delay: 0.2, span: 0.5));
    }

    private static void GridFocus(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var left = width * 0.08;
        var top = height * 0.12;
        var cellWidth = width * 0.84 / 4;
        var cellHeight = height * 0.76 / 3;
        double[] Cell(int column, int row) => H.Rect(left + cellWidth * column, top + cellHeight * row, cellWidth, cellHeight);
        AddCutField(ctx, palette.Tone2, [Cell(0, 0), Cell(3, 2)]);
        AddHoleLip(ctx, Cell(0, 0), 2, 0.7, 0.1);
        AddHoleLip(ctx, Cell(3, 2), 2, 0.7, 0.14);
        ctx.Add(S.PolygonFill(Cell(3, 0), palette.Tone4, 0.95, ctx.Gradient), O(delay: 0.08, span: 0.55, enterDX: width * 0.08));
        var lines = new List<TemperaLine>();
        for (var column = 0; column <= 4; column++)
            lines.Add(new TemperaLine(left + cellWidth * column, top, left + cellWidth * column, top + cellHeight * 3));
        for (var row = 0; row <= 3; row++)
            lines.Add(new TemperaLine(left, top + cellHeight * row, left + cellWidth * 4, top + cellHeight * row));
        ctx.Add(S.Lines(lines, palette.Tone4, 1.2, 0.6), O(delay: 0.16, span: 0.55));
    }

    private static void AxisCaps(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var cx = width * 0.5;
        var cy = height * 0.42;
        var bar = AxisRect(cx, cy, FlowSpan(ctx), unit * 0.22, ctx.FlowAngle);
        var port = H.Circle(cx, cy, unit * 0.08, 32);
        AddCutField(ctx, palette.Tone1, [port], alpha: 0.92);
        ctx.Add(S.PolygonFillWithHoles(bar, [port], palette.Tone4, 0.94, ctx.Gradient), O(delay: 0.05, span: 0.6));
        ctx.Add(S.Rings([new TemperaDisc(cx, cy, unit * 0.08)], palette.Ink, 2.4, 0.85), O(delay: 0.12, span: 0.5));
        double[] sides = [1, -1];
        for (var index = 0; index < sides.Length; index++)
        {
            var distance = unit * 0.46;
            var capX = cx + Math.Cos(ctx.FlowAngle) * distance * sides[index];
            var capY = cy + Math.Sin(ctx.FlowAngle) * distance * sides[index];
            ctx.Add(S.PolygonFill(AxisRect(capX, capY, unit * 0.1, unit * 0.34, ctx.FlowAngle), palette.Ink, 0.85),
                O(delay: 0.16 + index * 0.04, span: 0.5));
        }
    }

    private static void StrobeSlats(TemperaCompositionContext ctx)
    {
        var (width, height, palette, bleed) = (ctx.Width, ctx.Height, ctx.Palette, ctx.Bleed);
        AddField(ctx, palette.Tone1);
        double[] widths = [0.06, 0.03, 0.09, 0.04, 0.34, 0.04, 0.08, 0.03, 0.06];
        var cursor = width * 0.03;
        for (var index = 0; index < widths.Length; index++)
        {
            var fraction = widths[index];
            var slat = H.Rect(cursor, -bleed, width * fraction, height + bleed * 2);
            var wide = fraction > 0.2;
            ctx.Add(S.PolygonFill(slat, wide ? palette.Tone2 : palette.Tone4, wide ? 0.9 : 0.85, ctx.Gradient),
                O(delay: index * 0.03, span: 0.55, enterDY: (index % 2 == 0 ? 1 : -1) * height * 0.14));
            cursor += width * (fraction + 0.02);
        }
    }

    private static void OffsetPlate(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var unit = Math.Min(width, height);
        var step = unit * 0.06;
        var holeX = width * 0.72;
        var holeY = height * 0.36;
        var hole = H.Circle(holeX, holeY, unit * 0.05, 28);
        AddCutField(ctx, palette.Tone1, [hole], alpha: 0.9);
        TemperaRgb[] tones = [palette.Tone2, palette.Tone3, palette.Tone4];
        for (var index = 0; index < tones.Length; index++)
        {
            var plate = H.Rect(width * 0.2 + step * index, height * 0.2 + step * index, width * 0.5, height * 0.5);
            ctx.Add(S.PolygonFillWithHoles(plate, [hole], tones[index], index == 2 ? 0.95 : 0.8, ctx.Gradient),
                O(delay: index * 0.06, span: 0.55, enterDX: -step * 2, enterDY: -step));
        }
        ctx.Add(S.Rings([new TemperaDisc(holeX, holeY, unit * 0.05)], palette.Ink, 2, 0.8), O(delay: 0.22, span: 0.5));
    }

    private static void RadialComb(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var hubX = width * 1.06;
        var hubY = height * 1.1;
        var reach = Math.Sqrt(width * width + height * height) * 1.3;
        AddField(ctx, palette.Tone2);
        var rays = Enumerable.Range(0, 11).Select(index =>
        {
            var angle = Math.PI + 0.16 + index * 0.062;
            return new TemperaLine(hubX, hubY, hubX + Math.Cos(angle) * reach, hubY + Math.Sin(angle) * reach);
        });
        ctx.Add(S.Lines(rays, palette.Tone4, 2.6, 0.8), O(delay: 0.06, span: 0.6));
        var start = Math.PI + 0.44;
        var end = Math.PI + 0.56;
        ctx.Add(S.PolygonFill([
            hubX, hubY,
            hubX + Math.Cos(start) * reach, hubY + Math.Sin(start) * reach,
            hubX + Math.Cos(end) * reach, hubY + Math.Sin(end) * reach,
        ], palette.Tone4, 0.95, ctx.Gradient), O(delay: 0.14, span: 0.6));
        if (!ctx.ShowDecor) return;
        ctx.Add(S.HatchFill(H.Rect(width * 0.06, height * 0.08, width * 0.2, height * 0.14), H.Spec(ctx.Seed, 151), palette.Tone4, 0.4),
            O(delay: 0.24, span: 0.6, grow: true));
    }

    private static void BracketTarget(TemperaCompositionContext ctx)
    {
        var (width, height, palette) = (ctx.Width, ctx.Height, ctx.Palette);
        var box = H.Rect(width * 0.28, height * 0.54, width * 0.44, height * 0.34);
        AddCutField(ctx, palette.Tone3, [box]);
        AddHoleLip(ctx, box, 2.4, 0.85, 0.08);
        var arm = Math.Min(width, height) * 0.08;
        (double Fx, double Fy, double Sx, double Sy)[] corners = [(0.28, 0.54, 1, 1), (0.72, 0.54, -1, 1), (0.28, 0.88, 1, -1), (0.72, 0.88, -1, -1)];
        for (var index = 0; index < corners.Length; index++)
        {
            var (fx, fy, sx, sy) = corners[index];
            var x = width * fx + sx * width * 0.03;
            var y = height * fy + sy * height * 0.04;
            ctx.Add(S.Lines([new TemperaLine(x, y, x + sx * arm, y), new TemperaLine(x, y, x, y + sy * arm)], palette.Ink, 3.5, 0.9),
                O(delay: 0.12 + index * 0.04, span: 0.5, enterDX: sx * width * 0.05));
        }
        ctx.Add(S.Lines([new TemperaLine(width * 0.1, height * 0.44, width * 0.9, height * 0.44)], palette.Tone4, 1.6, 0.6),
            O(delay: 0.24, span: 0.5, enterDX: -width * 0.1));
    }

    public static void Register(Dictionary<string, Action<TemperaCompositionContext>> registry)
    {
        registry["sight-mark"] = SightMark;
        registry["dial-scale"] = DialScale;
        registry["chevron-run"] = ChevronRun;
        registry["tally-column"] = TallyColumn;
        registry["grid-focus"] = GridFocus;
        registry["axis-caps"] = AxisCaps;
        registry["strobe-slats"] = StrobeSlats;
        registry["offset-plate"] = OffsetPlate;
        registry["radial-comb"] = RadialComb;
        registry["bracket-target"] = BracketTarget;
    }
}
