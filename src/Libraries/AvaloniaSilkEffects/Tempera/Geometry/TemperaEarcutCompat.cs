using System.Collections.Generic;

// Tempera 命名空间下的 Earcut 兼容别名转发

namespace AvaloniaSilkEffects.Tempera.Geometry;

public static class Earcut
{
    public static List<int> Triangulate(IReadOnlyList<double> data, IReadOnlyList<int>? holeIndices = null) =>
        AvaloniaSilkEffects.Earcut.Triangulate(data, holeIndices);
}
