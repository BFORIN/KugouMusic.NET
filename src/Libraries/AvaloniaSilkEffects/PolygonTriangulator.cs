using System.Numerics;

// 统一的多边形三角化适配器：直接下沉并复用 Core/Geometry/Earcut

namespace AvaloniaSilkEffects;

/// <summary>Triangulates a simple contour, including concave and reversed contours.</summary>
internal static class PolygonTriangulator
{
    internal static int[] Triangulate(IReadOnlyList<Vector2> points) =>
        Earcut.Triangulate(points);
}
