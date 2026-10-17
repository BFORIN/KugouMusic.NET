using System.Numerics;
using AvaloniaSilkEffects.Tempera.Geometry;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>Four-colour ramp plus the axis it runs along, supplied by the gradient colour mode.</summary>
internal sealed record TemperaGradientFill(TemperaRgb[] Colors, double Angle);

/// <summary>
/// Folia temperaShapes.ts: Graphics factories for the screentone language. Every factory returns a finished static node;
/// playback only writes transforms and alpha. Strokes use Pixi's default style (width as given, alignment 0.5, miter joins,
/// butt caps); fills are earcut polygons, centroid-fanned circles or rectangles, as Pixi builds them.
/// </summary>
internal static class TemperaShapes
{
    private sealed class PartBuilder
    {
        public readonly List<double> Vertices = [];
        public readonly List<int> Indices = [];
        public readonly List<double> Ramp = [];
    }

    private static TemperaMeshPart Finish(PartBuilder builder, TemperaRgb color, double alpha, Vector3[]? stops)
    {
        var positions = new float[builder.Vertices.Count];
        for (var index = 0; index < positions.Length; index++) positions[index] = (float)builder.Vertices[index];
        return new TemperaMeshPart
        {
            Positions = positions,
            Indices = [.. builder.Indices],
            Ramp = stops is null ? null : [.. builder.Ramp.Select(value => (float)value)],
            Color = color,
            Alpha = (float)alpha,
            Stops = stops,
        };
    }

    /// <summary>
    /// The stops a shape of <paramref name="tone"/> fills with: every ramp colour pulled halfway toward the tone, so the shape
    /// keeps the brightness its place in the composition needs.
    /// </summary>
    private static Vector3[]? Stops(TemperaGradientFill? gradient, TemperaRgb tone) =>
        gradient is { Colors.Length: > 1 }
            ? [.. Enumerable.Range(0, 4).Select(index =>
                TemperaRgb.Mix(gradient.Colors[Math.Min(index, gradient.Colors.Length - 1)], tone, 0.5).ToVector3())]
            : null;

    /// <summary>
    /// Pixi FillGradient (linear, textureSpace local) per shape: the ramp coordinate of each new vertex against the shape's own
    /// bounds. Pixi swaps start/end per axis whenever that axis runs negative and draws the texture reversed, which mirrors
    /// the ramp axis when exactly one of dx, dy is negative; that is reproduced here.
    /// </summary>
    private static void AppendRamp(PartBuilder builder, int fromVertex, double minX, double minY, double width, double height,
        double angle)
    {
        var hx = Math.Cos(angle) * 0.5;
        var hy = Math.Sin(angle) * 0.5;
        double x0 = 0.5 - hx, y0 = 0.5 - hy, x1 = 0.5 + hx, y1 = 0.5 + hy;
        var dx = x1 - x0;
        var dy = y1 - y0;
        var flip = dx < 0 || dy < 0;
        if (dx < 0)
        {
            (x0, x1) = (x1, x0);
            dx = -dx;
        }
        if (dy < 0)
        {
            (y0, y1) = (y1, y0);
            dy = -dy;
        }
        var lengthSquared = dx * dx + dy * dy;
        for (var vertex = fromVertex; vertex < builder.Vertices.Count / 2; vertex++)
        {
            var nx = width > 0 ? (builder.Vertices[vertex * 2] - minX) / width : 0;
            var ny = height > 0 ? (builder.Vertices[vertex * 2 + 1] - minY) / height : 0;
            var u = ((nx - x0) * dx + (ny - y0) * dy) / lengthSquared;
            builder.Ramp.Add(flip ? 1 - u : u);
        }
    }

    private static (double MinX, double MinY, double Width, double Height) Bounds(IReadOnlyList<double> polygon)
    {
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        for (var index = 0; index < polygon.Count; index += 2)
        {
            minX = Math.Min(minX, polygon[index]);
            maxX = Math.Max(maxX, polygon[index]);
            minY = Math.Min(minY, polygon[index + 1]);
            maxY = Math.Max(maxY, polygon[index + 1]);
        }
        return (minX, minY, maxX - minX, maxY - minY);
    }

    private static (double X, double Y) BoundsCenter(IReadOnlyList<double> polygon)
    {
        var (minX, minY, width, height) = Bounds(polygon);
        return (minX + width / 2, minY + height / 2);
    }

    private static TemperaGraphic Single(TemperaMeshPart part)
    {
        var node = new TemperaGraphic();
        if (part.Indices.Length > 0) node.Parts.Add(part);
        return node;
    }

    public static TemperaGraphic PolygonFill(double[] polygon, TemperaRgb color, double alpha = 1, TemperaGradientFill? gradient = null) =>
        PolygonFillWithHoles(polygon, [], color, alpha, gradient);

    /// <summary>
    /// A fill with holes punched clean through: what shows through is the shell's own background. Pixi attaches every
    /// <c>cut()</c> to the one fill instruction, so all holes go to earcut together.
    /// </summary>
    public static TemperaGraphic PolygonFillWithHoles(double[] polygon, IReadOnlyList<double[]> holes, TemperaRgb color, double alpha = 1,
        TemperaGradientFill? gradient = null)
    {
        var builder = new PartBuilder();
        PixiGeometry.FillPolygon(polygon, holes.Where(hole => hole.Length >= 6).ToArray(), builder.Vertices, builder.Indices);
        var stops = Stops(gradient, color);
        if (stops is not null)
        {
            var (minX, minY, width, height) = Bounds(polygon);
            AppendRamp(builder, 0, minX, minY, width, height, gradient!.Angle);
        }
        return Single(Finish(builder, color, alpha, stops));
    }

    public static TemperaGraphic PolygonOutline(double[] polygon, TemperaRgb color, double width, double alpha = 1)
    {
        var builder = new PartBuilder();
        PixiGeometry.Stroke(polygon, width, true, builder.Vertices, builder.Indices);
        return Single(Finish(builder, color, alpha, null));
    }

    /// <summary>Parallel strokes clipped to a convex polygon, pivoted on the shape centre so it can open with a scale.</summary>
    public static TemperaGraphic HatchFill(double[] polygon, TemperaHatchSpec spec, TemperaRgb color, double alpha = 1)
    {
        var builder = new PartBuilder();
        foreach (var line in TemperaHatch.Lines(polygon, spec))
            PixiGeometry.Stroke([line.X1, line.Y1, line.X2, line.Y2], spec.Width, false, builder.Vertices, builder.Indices);
        var node = Single(Finish(builder, color, alpha, null));
        var (x, y) = BoundsCenter(polygon);
        node.SetPivot(x, y);
        node.SetPosition(x, y);
        return node;
    }

    private static (double X, double Y) DiscsCenter(IReadOnlyList<TemperaDisc> discs)
    {
        if (discs.Count == 0) return (0, 0);
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity, minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (var disc in discs)
        {
            minX = Math.Min(minX, disc.X - disc.Radius);
            maxX = Math.Max(maxX, disc.X + disc.Radius);
            minY = Math.Min(minY, disc.Y - disc.Radius);
            maxY = Math.Max(maxY, disc.Y + disc.Radius);
        }
        return ((minX + maxX) / 2, (minY + maxY) / 2);
    }

    private static TemperaGraphic PivotOnSelf(TemperaGraphic node, (double X, double Y) center)
    {
        node.SetPivot(center.X, center.Y);
        node.SetPosition(center.X, center.Y);
        return node;
    }

    /// <summary>A whole disc field as one node, pivoted on the field centre so <c>drift</c> breathes in place.</summary>
    public static TemperaGraphic Discs(IReadOnlyList<TemperaDisc> discs, TemperaRgb color, double alpha = 1, TemperaGradientFill? gradient = null)
    {
        var builder = new PartBuilder();
        var stops = Stops(gradient, color);
        foreach (var disc in discs)
        {
            var radius = Math.Max(0.5, disc.Radius);
            var from = builder.Vertices.Count / 2;
            PixiGeometry.FanTriangulate(PixiGeometry.CirclePoints(disc.X, disc.Y, radius), builder.Vertices, builder.Indices);
            if (stops is not null) AppendRamp(builder, from, disc.X - radius, disc.Y - radius, radius * 2, radius * 2, gradient!.Angle);
        }
        return PivotOnSelf(Single(Finish(builder, color, alpha, stops)), DiscsCenter(discs));
    }

    public static TemperaGraphic Rings(IReadOnlyList<TemperaDisc> discs, TemperaRgb color, double width, double alpha = 1)
    {
        var builder = new PartBuilder();
        foreach (var disc in discs)
            PixiGeometry.Stroke(PixiGeometry.CirclePoints(disc.X, disc.Y, Math.Max(0.5, disc.Radius)), width, true, builder.Vertices,
                builder.Indices);
        return PivotOnSelf(Single(Finish(builder, color, alpha, null)), DiscsCenter(discs));
    }

    public static TemperaGraphic Lines(IEnumerable<TemperaLine> lines, TemperaRgb color, double width, double alpha = 1)
    {
        var builder = new PartBuilder();
        foreach (var line in lines)
            PixiGeometry.Stroke([line.X1, line.Y1, line.X2, line.Y2], width, false, builder.Vertices, builder.Indices);
        return Single(Finish(builder, color, alpha, null));
    }

    public static TemperaGraphic Polyline(double[] points, TemperaRgb color, double width, double alpha = 1)
    {
        var builder = new PartBuilder();
        if (points.Length >= 4) PixiGeometry.Stroke(points, width, false, builder.Vertices, builder.Indices);
        return Single(Finish(builder, color, alpha, null));
    }

    /// <summary>Concentric diamonds with alternating stroke weight, thickest outside; one stroke instruction per ring.</summary>
    public static TemperaGraphic ConcentricDiamonds(double cx, double cy, double rx, double ry, int rings, TemperaRgb color, double alpha = 1)
    {
        var node = new TemperaGraphic();
        for (var ring = 0; ring < Math.Max(1, rings); ring++)
        {
            var shrink = 1 - ring * 0.22;
            var builder = new PartBuilder();
            PixiGeometry.Stroke([cx, cy - ry * shrink, cx + rx * shrink, cy, cx, cy + ry * shrink, cx - rx * shrink, cy],
                ring % 2 == 0 ? 3.5 : 1.4, true, builder.Vertices, builder.Indices);
            node.Parts.Add(Finish(builder, color, alpha, null));
        }
        node.SetPivot(cx, cy);
        node.SetPosition(cx, cy);
        return node;
    }

    public static TemperaGraphic CrossMarks(IEnumerable<TemperaMark> marks, TemperaRgb color, double width, double alpha = 1)
    {
        var builder = new PartBuilder();
        foreach (var mark in marks)
        {
            var cos = Math.Cos(mark.Rotation + Math.PI / 4) * mark.Size;
            var sin = Math.Sin(mark.Rotation + Math.PI / 4) * mark.Size;
            PixiGeometry.Stroke([mark.X - cos, mark.Y - sin, mark.X + cos, mark.Y + sin], width, false, builder.Vertices, builder.Indices);
            PixiGeometry.Stroke([mark.X - sin, mark.Y + cos, mark.X + sin, mark.Y - cos], width, false, builder.Vertices, builder.Indices);
        }
        return Single(Finish(builder, color, alpha, null));
    }

    public static TemperaGraphic SquareMarks(IEnumerable<TemperaMark> marks, TemperaRgb color, double alpha = 1)
    {
        var builder = new PartBuilder();
        foreach (var mark in marks)
            PixiGeometry.FillRectangle(mark.X - mark.Size / 2, mark.Y - mark.Size / 2, mark.Size, mark.Size, builder.Vertices, builder.Indices);
        return Single(Finish(builder, color, alpha, null));
    }

    /// <summary>A rectangle fill (Pixi <c>rect().fill()</c>).</summary>
    public static TemperaGraphic RectFill(double x, double y, double width, double height, TemperaRgb color, double alpha = 1)
    {
        var builder = new PartBuilder();
        PixiGeometry.FillRectangle(x, y, width, height, builder.Vertices, builder.Indices);
        return Single(Finish(builder, color, alpha, null));
    }
}
