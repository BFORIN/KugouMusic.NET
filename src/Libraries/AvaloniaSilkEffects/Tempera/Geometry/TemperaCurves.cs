namespace AvaloniaSilkEffects.Tempera.Geometry;

public readonly record struct TemperaDisc(double X, double Y, double Radius);

/// <summary>
/// Folia temperaCurves.ts: the rounded vocabulary as flat polygons. Only <see cref="Ellipse"/> and <see cref="RoundedRect"/>
/// are convex and may be hatched; everything else is fill / outline only.
/// </summary>
public static class TemperaCurves
{
    private const double Tau = Math.PI * 2;

    public static double[] Ellipse(double cx, double cy, double rx, double ry, double segments = 32)
    {
        var count = Math.Max(8, TemperaRgb.JsRound(segments));
        var points = new double[count * 2];
        for (var index = 0; index < count; index++)
        {
            var angle = index / (double)count * Tau;
            points[index * 2] = cx + Math.Cos(angle) * rx;
            points[index * 2 + 1] = cy + Math.Sin(angle) * ry;
        }
        return points;
    }

    public static double[] RoundedRect(double x, double y, double width, double height, double radius, double cornerSegments = 6)
    {
        var limit = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
        var steps = Math.Max(1, TemperaRgb.JsRound(cornerSegments));
        (double X, double Y, double Start)[] corners =
        [
            (x + width - limit, y + limit, -Math.PI / 2),
            (x + width - limit, y + height - limit, 0),
            (x + limit, y + height - limit, Math.PI / 2),
            (x + limit, y + limit, Math.PI),
        ];
        var points = new List<double>(corners.Length * (steps + 1) * 2);
        foreach (var (ccx, ccy, start) in corners)
            for (var step = 0; step <= steps; step++)
            {
                var angle = start + step / (double)steps * (Math.PI / 2);
                points.Add(ccx + Math.Cos(angle) * limit);
                points.Add(ccy + Math.Sin(angle) * limit);
            }
        return [.. points];
    }

    public static double[] Rotate(double[] polygon, double cx, double cy, double angle)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var points = new double[polygon.Length];
        for (var index = 0; index < polygon.Length; index += 2)
        {
            var dx = polygon[index] - cx;
            var dy = polygon[index + 1] - cy;
            points[index] = cx + dx * cos - dy * sin;
            points[index + 1] = cy + dx * sin + dy * cos;
        }
        return points;
    }

    public static double[] Lobed(double cx, double cy, double rx, double ry, double lobes, double amplitude, double segments = 96)
    {
        var count = Math.Max(24, TemperaRgb.JsRound(segments));
        var petals = Math.Max(1, TemperaRgb.JsRound(lobes));
        var swell = Math.Max(0, amplitude);
        var points = new double[count * 2];
        for (var index = 0; index < count; index++)
        {
            var angle = index / (double)count * Tau;
            var scale = (1 + Math.Cos(angle * petals) * swell) / (1 + swell);
            points[index * 2] = cx + Math.Cos(angle) * rx * scale;
            points[index * 2 + 1] = cy + Math.Sin(angle) * ry * scale;
        }
        return points;
    }

    public static double[] ScallopBand(double x0, double x1, double flatY, double baseY, double bumps, double segmentsPerBump = 10)
    {
        var count = Math.Max(1, TemperaRgb.JsRound(bumps));
        var steps = Math.Max(3, TemperaRgb.JsRound(segmentsPerBump));
        var span = (x1 - x0) / count;
        var radius = Math.Abs(span) / 2;
        var direction = baseY >= flatY ? 1 : -1;
        var points = new List<double> { x0, flatY, x1, flatY };
        for (var bump = count - 1; bump >= 0; bump--)
        {
            var cx = x0 + span * (bump + 0.5);
            for (var step = 0; step <= steps; step++)
            {
                var angle = step / (double)steps * Math.PI;
                points.Add(cx + Math.Cos(angle) * (span / 2));
                points.Add(baseY + direction * Math.Sin(angle) * radius);
            }
        }
        return [.. points];
    }

    public static double[] Heart(double cx, double cy, double rx, double ry, double segments = 64)
    {
        var count = Math.Max(16, TemperaRgb.JsRound(segments));
        var raw = new double[count * 2];
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        for (var index = 0; index < count; index++)
        {
            var t = index / (double)count * Tau;
            var x = Math.Pow(Math.Sin(t), 3) * 16;
            var y = -(13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t));
            raw[index * 2] = x;
            raw[index * 2 + 1] = y;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
        var halfWidth = Math.Max(1e-6, (maxX - minX) / 2);
        var halfHeight = Math.Max(1e-6, (maxY - minY) / 2);
        var midX = (minX + maxX) / 2;
        var midY = (minY + maxY) / 2;
        var points = new double[raw.Length];
        for (var index = 0; index < raw.Length; index += 2)
        {
            points[index] = cx + (raw[index] - midX) / halfWidth * rx;
            points[index + 1] = cy + (raw[index + 1] - midY) / halfHeight * ry;
        }
        return points;
    }

    public static double[] AnnularSector(double cx, double cy, double innerRadius, double outerRadius, double startAngle,
        double endAngle, double segments = 10)
    {
        var steps = Math.Max(2, TemperaRgb.JsRound(segments));
        var points = new List<double>((steps + 1) * 4);
        for (var step = 0; step <= steps; step++)
        {
            var angle = startAngle + (endAngle - startAngle) * step / steps;
            points.Add(cx + Math.Cos(angle) * outerRadius);
            points.Add(cy + Math.Sin(angle) * outerRadius);
        }
        for (var step = steps; step >= 0; step--)
        {
            var angle = startAngle + (endAngle - startAngle) * step / steps;
            points.Add(cx + Math.Cos(angle) * innerRadius);
            points.Add(cy + Math.Sin(angle) * innerRadius);
        }
        return [.. points];
    }

    public static double[] Star(double cx, double cy, double outerRadius, double innerRadius, double tipCount, double rotation = 0)
    {
        var tips = Math.Max(3, TemperaRgb.JsRound(tipCount));
        var polygon = new double[tips * 4];
        for (var index = 0; index < tips * 2; index++)
        {
            var radius = index % 2 == 0 ? outerRadius : innerRadius;
            var angle = rotation - Math.PI / 2 + index / (double)(tips * 2) * Tau;
            polygon[index * 2] = cx + Math.Cos(angle) * radius;
            polygon[index * 2 + 1] = cy + Math.Sin(angle) * radius;
        }
        return polygon;
    }

    public static double[] ArcRibbon(double x0, double x1, double y, double thickness, double sag, double steps = 28)
    {
        var count = Math.Max(4, TemperaRgb.JsRound(steps));
        var half = thickness / 2;
        var top = new List<double>();
        var bottom = new List<double>();
        for (var index = 0; index <= count; index++)
        {
            var progress = index / (double)count;
            var x = x0 + (x1 - x0) * progress;
            var centre = y + Math.Sin(progress * Math.PI) * sag;
            top.Add(x);
            top.Add(centre - half);
            bottom.Add(x);
            bottom.Add(centre + half);
        }
        var points = new List<double>(top);
        for (var index = bottom.Count - 2; index >= 0; index -= 2)
        {
            points.Add(bottom[index]);
            points.Add(bottom[index + 1]);
        }
        return [.. points];
    }

    /// <summary>A seeded disc field on a jittered lattice.</summary>
    public static List<TemperaDisc> DiscField(long seed, long salt, double width, double height, double count, double radius)
    {
        var total = Math.Max(0, TemperaRgb.JsRound(count));
        var discs = new List<TemperaDisc>(total);
        if (total == 0 || width <= 0 || height <= 0) return discs;
        var columns = Math.Max(1, TemperaRgb.JsRound(Math.Sqrt(total * (width / height))));
        var rows = Math.Max(1, (int)Math.Ceiling(total / (double)columns));
        var cellWidth = width / columns;
        var cellHeight = height / rows;
        for (var index = 0; index < total; index++)
        {
            var column = index % columns;
            var row = index / columns;
            discs.Add(new TemperaDisc(
                cellWidth * (column + 0.15 + TemperaRandom.Hash01(seed, index, salt) * 0.7),
                cellHeight * (row + 0.15 + TemperaRandom.Hash01(seed, index, salt + 3) * 0.7),
                radius * (0.55 + TemperaRandom.Hash01(seed, index, salt + 7) * 0.75)));
        }
        return discs;
    }
}
