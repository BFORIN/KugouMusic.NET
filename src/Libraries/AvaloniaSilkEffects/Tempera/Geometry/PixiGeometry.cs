namespace AvaloniaSilkEffects.Tempera.Geometry;

/// <summary>
/// The parts of Pixi 8's Graphics builders Tempera relies on: buildCircle's tessellation and centroid fan, buildRectangle,
/// earcut-filled polygons (with holes), and buildLine with the default stroke style (alignment 0.5, miter join, miter limit
/// 10, butt cap). Output is an indexed triangle list in the node's local space.
/// </summary>
public static class PixiGeometry
{
    private const double ClosePointEps = 1e-4;
    private const double CurveEps = 0.0001;
    private const double MiterLimit = 10;

    /// <summary>buildCircle.build for a circle: 4·n points, n = ceil(2.3·√(2r)), starting at angle 0 and turning clockwise on screen.</summary>
    public static double[] CirclePoints(double x, double y, double radius)
    {
        if (radius <= 0) return [];
        double rx = radius, ry = radius;
        const double dx = 0, dy = 0;
        var n = (int)Math.Ceiling(2.3 * Math.Sqrt(rx + ry));
        var m = n * 8;
        if (m == 0) return [];
        var points = new double[m];
        var j1 = 0;
        var j2 = n * 4 + 2;
        var j3 = j2;
        var j4 = m;
        var x0 = dx + rx;
        var y0 = dy;
        var x1 = x + x0;
        var x2 = x - x0;
        var y1 = y + y0;
        points[j1++] = x1;
        points[j1++] = y1;
        points[--j2] = y1;
        points[--j2] = x2;
        for (var i = 1; i < n; i++)
        {
            var a = Math.PI / 2 * (i / (double)n);
            var px0 = dx + Math.Cos(a) * rx;
            var py0 = dy + Math.Sin(a) * ry;
            var px1 = x + px0;
            var px2 = x - px0;
            var py1 = y + py0;
            var py2 = y - py0;
            points[j1++] = px1;
            points[j1++] = py1;
            points[--j2] = py1;
            points[--j2] = px2;
            points[j3++] = px2;
            points[j3++] = py2;
            points[--j4] = py2;
            points[--j4] = px1;
        }
        x0 = dx;
        y0 = dy + ry;
        x1 = x + x0;
        y1 = y + y0;
        var y2 = y - y0;
        points[j1++] = x1;
        points[j1++] = y1;
        points[--j4] = y2;
        points[--j4] = x1;
        return points;
    }

    /// <summary>buildCircle.triangulate: a fan around the points' centroid.</summary>
    public static void FanTriangulate(double[] points, List<double> vertices, List<int> indices)
    {
        if (points.Length == 0) return;
        double centerX = 0, centerY = 0;
        for (var i = 0; i < points.Length; i += 2)
        {
            centerX += points[i];
            centerY += points[i + 1];
        }
        centerX /= points.Length / 2d;
        centerY /= points.Length / 2d;
        var offset = vertices.Count / 2;
        vertices.Add(centerX);
        vertices.Add(centerY);
        var count = offset + 1;
        for (var i = 0; i < points.Length; i += 2)
        {
            vertices.Add(points[i]);
            vertices.Add(points[i + 1]);
            if (i > 0)
            {
                indices.Add(count);
                indices.Add(offset);
                indices.Add(count - 1);
            }
            count++;
        }
        indices.Add(offset + 1);
        indices.Add(offset);
        indices.Add(count - 1);
    }

    public static void FillPolygon(IReadOnlyList<double> points, IReadOnlyList<IReadOnlyList<double>>? holes, List<double> vertices,
        List<int> indices)
    {
        if (points.Count < 6) return;
        var data = new List<double>(points);
        List<int>? holeIndices = null;
        if (holes is { Count: > 0 })
        {
            holeIndices = [];
            foreach (var hole in holes)
            {
                holeIndices.Add(data.Count / 2);
                data.AddRange(hole);
            }
        }
        var triangles = Earcut.Triangulate(data, holeIndices);
        var offset = vertices.Count / 2;
        foreach (var index in triangles) indices.Add(index + offset);
        vertices.AddRange(data);
    }

    public static void FillRectangle(double x, double y, double width, double height, List<double> vertices, List<int> indices)
    {
        if (width <= 0 || height <= 0) return;
        var offset = vertices.Count / 2;
        vertices.AddRange([x, y, x + width, y, x + width, y + height, x, y + height]);
        indices.AddRange([offset, offset + 1, offset + 2, offset, offset + 2, offset + 3]);
    }

    /// <summary>buildLine with alignment 0.5, miter join and butt cap.</summary>
    public static void Stroke(IReadOnlyList<double> source, double strokeWidth, bool closed, List<double> verts, List<int> indices)
    {
        if (source.Count == 0) return;
        var points = new List<double>(source);
        var firstX = points[0];
        var firstY = points[1];
        var lastX = points[^2];
        var lastY = points[^1];
        var closedPath = Math.Abs(firstX - lastX) < ClosePointEps && Math.Abs(firstY - lastY) < ClosePointEps;
        if (closed)
        {
            if (closedPath)
            {
                points.RemoveAt(points.Count - 1);
                points.RemoveAt(points.Count - 1);
                lastX = points[^2];
                lastY = points[^1];
            }
            var midX = (firstX + lastX) * 0.5;
            var midY = (lastY + firstY) * 0.5;
            points.Insert(0, midY);
            points.Insert(0, midX);
            points.Add(midX);
            points.Add(midY);
        }

        var length = points.Count / 2;
        var indexCount = points.Count;
        var indexStart = verts.Count / 2;
        var width = strokeWidth / 2;
        var widthSquared = width * width;
        const double miterLimitSquared = MiterLimit * MiterLimit;

        var x0 = points[0];
        var y0 = points[1];
        var x1 = points[2];
        var y1 = points[3];
        double x2, y2;
        var perpX = -(y0 - y1);
        var perpY = x0 - x1;
        double perp1x, perp1y;
        var dist = Math.Sqrt(perpX * perpX + perpY * perpY);
        perpX /= dist;
        perpY /= dist;
        perpX *= width;
        perpY *= width;
        const double innerWeight = 1;
        const double outerWeight = 1;

        verts.Add(x0 - perpX * innerWeight);
        verts.Add(y0 - perpY * innerWeight);
        verts.Add(x0 + perpX * outerWeight);
        verts.Add(y0 + perpY * outerWeight);

        for (var i = 1; i < length - 1; ++i)
        {
            x0 = points[(i - 1) * 2];
            y0 = points[(i - 1) * 2 + 1];
            x1 = points[i * 2];
            y1 = points[i * 2 + 1];
            x2 = points[(i + 1) * 2];
            y2 = points[(i + 1) * 2 + 1];

            perpX = -(y0 - y1);
            perpY = x0 - x1;
            dist = Math.Sqrt(perpX * perpX + perpY * perpY);
            perpX /= dist;
            perpY /= dist;
            perpX *= width;
            perpY *= width;

            perp1x = -(y1 - y2);
            perp1y = x1 - x2;
            dist = Math.Sqrt(perp1x * perp1x + perp1y * perp1y);
            perp1x /= dist;
            perp1y /= dist;
            perp1x *= width;
            perp1y *= width;

            var dx0 = x1 - x0;
            var dy0 = y0 - y1;
            var dx1 = x1 - x2;
            var dy1 = y2 - y1;
            var dot = dx0 * dx1 + dy0 * dy1;
            var cross = dy0 * dx1 - dy1 * dx0;
            var clockwise = cross < 0;

            if (Math.Abs(cross) < 0.001 * Math.Abs(dot))
            {
                verts.Add(x1 - perpX * innerWeight);
                verts.Add(y1 - perpY * innerWeight);
                verts.Add(x1 + perpX * outerWeight);
                verts.Add(y1 + perpY * outerWeight);
                if (dot >= 0)
                {
                    indexCount += 2;
                    verts.Add(x1 - perp1x * outerWeight);
                    verts.Add(y1 - perp1y * outerWeight);
                    verts.Add(x1 + perp1x * innerWeight);
                    verts.Add(y1 + perp1y * innerWeight);
                }
                continue;
            }

            var c1 = (-perpX + x0) * (-perpY + y1) - (-perpX + x1) * (-perpY + y0);
            var c2 = (-perp1x + x2) * (-perp1y + y1) - (-perp1x + x1) * (-perp1y + y2);
            var px = (dx0 * c2 - dx1 * c1) / cross;
            var py = (dy1 * c1 - dy0 * c2) / cross;
            var pDist = (px - x1) * (px - x1) + (py - y1) * (py - y1);
            var imx = x1 + (px - x1) * innerWeight;
            var imy = y1 + (py - y1) * innerWeight;
            var omx = x1 - (px - x1) * outerWeight;
            var omy = y1 - (py - y1) * outerWeight;

            var smallerInsideSegmentSq = Math.Min(dx0 * dx0 + dy0 * dy0, dx1 * dx1 + dy1 * dy1);
            var insideWeight = clockwise ? innerWeight : outerWeight;
            var smallerInsideDiagonalSq = smallerInsideSegmentSq + insideWeight * insideWeight * widthSquared;
            var insideMiterOk = pDist <= smallerInsideDiagonalSq;

            if (insideMiterOk)
            {
                if (pDist / widthSquared > miterLimitSquared)
                {
                    if (clockwise)
                    {
                        verts.AddRange([imx, imy, x1 + perpX * outerWeight, y1 + perpY * outerWeight,
                            imx, imy, x1 + perp1x * outerWeight, y1 + perp1y * outerWeight]);
                    }
                    else
                    {
                        verts.AddRange([x1 - perpX * innerWeight, y1 - perpY * innerWeight, omx, omy,
                            x1 - perp1x * innerWeight, y1 - perp1y * innerWeight, omx, omy]);
                    }
                    indexCount += 2;
                }
                else
                {
                    verts.AddRange([imx, imy, omx, omy]);
                }
            }
            else
            {
                verts.AddRange([x1 - perpX * innerWeight, y1 - perpY * innerWeight, x1 + perpX * outerWeight, y1 + perpY * outerWeight]);
                if (pDist / widthSquared <= miterLimitSquared)
                {
                    if (clockwise) verts.AddRange([omx, omy, omx, omy]);
                    else verts.AddRange([imx, imy, imx, imy]);
                    indexCount += 2;
                }
                verts.AddRange([x1 - perp1x * innerWeight, y1 - perp1y * innerWeight, x1 + perp1x * outerWeight, y1 + perp1y * outerWeight]);
                indexCount += 2;
            }
        }

        x0 = points[(length - 2) * 2];
        y0 = points[(length - 2) * 2 + 1];
        x1 = points[(length - 1) * 2];
        y1 = points[(length - 1) * 2 + 1];
        perpX = -(y0 - y1);
        perpY = x0 - x1;
        dist = Math.Sqrt(perpX * perpX + perpY * perpY);
        perpX /= dist;
        perpY /= dist;
        perpX *= width;
        perpY *= width;
        verts.Add(x1 - perpX * innerWeight);
        verts.Add(y1 - perpY * innerWeight);
        verts.Add(x1 + perpX * outerWeight);
        verts.Add(y1 + perpY * outerWeight);

        const double eps2 = CurveEps * CurveEps;
        for (var i = indexStart; i < indexCount + indexStart - 2; ++i)
        {
            x0 = verts[i * 2];
            y0 = verts[i * 2 + 1];
            x1 = verts[(i + 1) * 2];
            y1 = verts[(i + 1) * 2 + 1];
            x2 = verts[(i + 2) * 2];
            y2 = verts[(i + 2) * 2 + 1];
            if (Math.Abs(x0 * (y1 - y2) + x1 * (y2 - y0) + x2 * (y0 - y1)) < eps2) continue;
            indices.Add(i);
            indices.Add(i + 1);
            indices.Add(i + 2);
        }
    }
}
