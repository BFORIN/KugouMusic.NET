namespace AvaloniaSilkEffects.Tempera.Geometry;

public readonly record struct TemperaHatchSpec(double Angle, double Spacing, double Width);

public readonly record struct TemperaLine(double X1, double Y1, double X2, double Y2);

public readonly record struct TemperaMark(double X, double Y, double Size, double Rotation);

/// <summary>
/// Folia temperaHatch.ts: pure, seed-driven generators for the screentone language — diagonal hatch fills clipped to convex
/// polygons, scribbles, wavy edges, mark rows, the paper dot lattice and the full-bleed crossing lines. Polygons are flat
/// [x0, y0, x1, y1, …] arrays exactly like the TypeScript.
/// </summary>
public static class TemperaHatch
{
    private const double Tau = Math.PI * 2;
    private const int MaxHatchLines = 320;
    private static readonly double[] HatchAngles = [-Math.PI / 4, Math.PI / 4, -Math.PI / 3, Math.PI / 6];
    private static readonly double[] HatchSpacings = [9, 13, 18];

    public static TemperaHatchSpec Spec(long seed, long salt, double scale = 1)
    {
        var angleIndex = (int)Math.Floor(TemperaRandom.Hash01(seed, 1, salt) * 4);
        var angle = angleIndex < HatchAngles.Length ? HatchAngles[angleIndex] : Math.PI / 4;
        var density = (int)Math.Floor(TemperaRandom.Hash01(seed, 2, salt) * 3);
        var spacing = (density < HatchSpacings.Length ? HatchSpacings[density] : 13) * scale;
        var width = Math.Max(0.8, spacing * (0.16 + TemperaRandom.Hash01(seed, 3, salt) * 0.14));
        return new TemperaHatchSpec(angle, spacing, width);
    }

    public static double[] Rect(double x, double y, double width, double height) =>
        [x, y, x + width, y, x + width, y + height, x, y + height];

    /// <summary>Convex circle approximation, so the hatch clipper can fill round windows too.</summary>
    public static double[] Circle(double cx, double cy, double radius, double segments = 28)
    {
        var count = Math.Max(6, (int)TemperaRgb.JsRound(segments));
        var points = new double[count * 2];
        for (var index = 0; index < count; index++)
        {
            var angle = index / (double)count * Tau;
            points[index * 2] = cx + Math.Cos(angle) * radius;
            points[index * 2 + 1] = cy + Math.Sin(angle) * radius;
        }
        return points;
    }

    public static double[] Diamond(double cx, double cy, double rx, double ry) =>
        [cx, cy - ry, cx + rx, cy, cx, cy + ry, cx - rx, cy];

    private static TemperaLine? ClipToConvex(double[] polygon, double ax, double ay, double dx, double dy, double span)
    {
        var count = polygon.Length / 2;
        double cx = 0, cy = 0;
        for (var index = 0; index < count; index++)
        {
            cx += polygon[index * 2];
            cy += polygon[index * 2 + 1];
        }
        cx /= count;
        cy /= count;
        var tMin = -span;
        var tMax = span;
        for (var index = 0; index < count; index++)
        {
            var x0 = polygon[index * 2];
            var y0 = polygon[index * 2 + 1];
            var x1 = polygon[(index + 1) % count * 2];
            var y1 = polygon[(index + 1) % count * 2 + 1];
            var nx = y1 - y0;
            var ny = -(x1 - x0);
            if (nx * (cx - x0) + ny * (cy - y0) > 0)
            {
                nx = -nx;
                ny = -ny;
            }
            var denominator = nx * dx + ny * dy;
            var numerator = nx * (ax - x0) + ny * (ay - y0);
            if (Math.Abs(denominator) < 1e-9)
            {
                if (numerator > 0) return null;
                continue;
            }
            var t = -numerator / denominator;
            if (denominator > 0) tMax = Math.Min(tMax, t);
            else tMin = Math.Max(tMin, t);
            if (tMin > tMax) return null;
        }
        if (tMax - tMin < 0.5) return null;
        return new TemperaLine(ax + dx * tMin, ay + dy * tMin, ax + dx * tMax, ay + dy * tMax);
    }

    /// <summary>Parallel strokes clipped to a convex polygon; <paramref name="coverage"/> trims the ends toward the middle.</summary>
    public static List<TemperaLine> Lines(double[] polygon, TemperaHatchSpec spec, double coverage = 1)
    {
        var lines = new List<TemperaLine>();
        if (polygon.Length < 6 || spec.Spacing <= 0) return lines;
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        for (var index = 0; index < polygon.Length; index += 2)
        {
            minX = Math.Min(minX, polygon[index]);
            maxX = Math.Max(maxX, polygon[index]);
            minY = Math.Min(minY, polygon[index + 1]);
            maxY = Math.Max(maxY, polygon[index + 1]);
        }
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        var diagonal = Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        if (diagonal <= 0) return lines;
        var dx = Math.Cos(spec.Angle);
        var dy = Math.Sin(spec.Angle);
        var px = -dy;
        var py = dx;
        var steps = Math.Min(MaxHatchLines / 2, (int)Math.Ceiling(diagonal / (spec.Spacing * 2)) + 2);
        var clamped = Math.Clamp(coverage, 0, 1);
        for (var step = -steps; step <= steps; step++)
        {
            var offset = step * spec.Spacing;
            if (ClipToConvex(polygon, centerX + px * offset, centerY + py * offset, dx, dy, diagonal) is not { } clipped) continue;
            if (clamped >= 1)
            {
                lines.Add(clipped);
                continue;
            }
            var midX = (clipped.X1 + clipped.X2) / 2;
            var midY = (clipped.Y1 + clipped.Y2) / 2;
            lines.Add(new TemperaLine(midX + (clipped.X1 - midX) * clamped, midY + (clipped.Y1 - midY) * clamped,
                midX + (clipped.X2 - midX) * clamped, midY + (clipped.Y2 - midY) * clamped));
        }
        return lines;
    }

    /// <summary>A jittered spiral polyline that reads as a scribbled loop.</summary>
    public static double[] Scribble(long seed, long salt, double cx, double cy, double radius, double turns = 2)
    {
        const int perTurn = 13;
        var total = Math.Max(perTurn, TemperaRgb.JsRound(perTurn * Math.Max(1, turns)));
        var points = new double[(total + 1) * 2];
        for (var index = 0; index <= total; index++)
        {
            var progress = index / (double)total;
            var angle = progress * Tau * Math.Max(1, turns);
            var jitterR = (TemperaRandom.Hash01(seed, index, salt) - 0.5) * radius * 0.26;
            var jitterA = (TemperaRandom.Hash01(seed, index, salt + 7) - 0.5) * 0.22;
            var currentRadius = radius * (0.52 + 0.48 * progress) + jitterR;
            points[index * 2] = cx + Math.Cos(angle + jitterA) * currentRadius;
            points[index * 2 + 1] = cy + Math.Sin(angle + jitterA) * currentRadius * 0.82;
        }
        return points;
    }

    /// <summary>A wobbling horizontal edge.</summary>
    public static double[] Wavy(long seed, long salt, double x0, double x1, double y, double amplitude, double steps = 24)
    {
        var safeSteps = Math.Max(2, TemperaRgb.JsRound(steps));
        var points = new double[(safeSteps + 1) * 2];
        for (var index = 0; index <= safeSteps; index++)
        {
            var progress = index / (double)safeSteps;
            var wave = Math.Sin(progress * Tau * 1.5 + TemperaRandom.Hash01(seed, 0, salt) * Tau);
            var jitter = (TemperaRandom.Hash01(seed, index, salt) - 0.5) * amplitude * 0.5;
            points[index * 2] = x0 + (x1 - x0) * progress;
            points[index * 2 + 1] = y + wave * amplitude + jitter;
        }
        return points;
    }

    private static List<TemperaMark> MarkRow(long seed, long salt, double x, double y, double count, double spacing, double size,
        double angle)
    {
        var marks = new List<TemperaMark>();
        var total = Math.Max(0, TemperaRgb.JsRound(count));
        var dx = Math.Cos(angle);
        var dy = Math.Sin(angle);
        for (var index = 0; index < total; index++)
        {
            var jitter = (TemperaRandom.Hash01(seed, index, salt) - 0.5) * spacing * 0.16;
            var distance = index * spacing + jitter;
            marks.Add(new TemperaMark(x + dx * distance, y + dy * distance,
                size * (0.82 + TemperaRandom.Hash01(seed, index, salt + 3) * 0.36),
                (TemperaRandom.Hash01(seed, index, salt + 11) - 0.5) * 0.3));
        }
        return marks;
    }

    public static List<TemperaMark> CrossRow(long seed, long salt, double x, double y, double count, double spacing, double size = 9,
        double angle = 0) => MarkRow(seed, salt, x, y, count, spacing, size, angle);

    public static List<TemperaMark> DotRow(long seed, long salt, double x, double y, double count, double spacing, double size = 4,
        double angle = Math.PI / 2) => MarkRow(seed, salt, x, y, count, spacing, size, angle);

    /// <summary>An even dot lattice, every other row offset by half a step so it reads as halftone.</summary>
    public static List<TemperaMark> DotGrid(double width, double height, double spacing, double size = 1.6)
    {
        var marks = new List<TemperaMark>();
        if (spacing <= 0 || width <= 0 || height <= 0) return marks;
        var columns = Math.Min(200, (int)Math.Ceiling(width / spacing) + 1);
        var rows = Math.Min(200, (int)Math.Ceiling(height / spacing) + 1);
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                marks.Add(new TemperaMark(column * spacing + (row % 2 == 0 ? 0 : spacing / 2), row * spacing, size, 0));
        return marks;
    }

    /// <summary>One to three shallow lines that run past the viewport edges and carry the composition.</summary>
    public static List<TemperaLine> CrossingLines(long seed, long salt, double width, double height, double count)
    {
        var total = Math.Min(3, Math.Max(0, TemperaRgb.JsRound(count)));
        var lines = new List<TemperaLine>(total);
        for (var index = 0; index < total; index++)
        {
            var anchorY = height * (0.18 + TemperaRandom.Hash01(seed, index, salt) * 0.64);
            var sign = TemperaRandom.Hash01(seed, index, salt + 5) > 0.5 ? 1 : -1;
            var angle = sign * (0.07 + TemperaRandom.Hash01(seed, index, salt + 9) * 0.11);
            var reach = width * 0.9;
            lines.Add(new TemperaLine(-width * 0.3, anchorY - Math.Tan(angle) * reach, width * 1.3, anchorY + Math.Tan(angle) * reach));
        }
        return lines;
    }
}
