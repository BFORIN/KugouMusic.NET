using System.Numerics;

namespace AvaloniaSilkEffects.Sonnet;

/// <summary>Folia AnimatedGraphics stroke staggering, applied to persistent meshes.</summary>
internal sealed class SonnetStrokeReveal
{
    private readonly List<Stroke> _strokes = [];
    private readonly List<(EffectNode Node, float Alpha)> _fills = [];

    internal SonnetStrokeReveal(EffectContainer root) => Collect(root);

    private void Collect(EffectNode node)
    {
        if (node is EffectContainer container)
        {
            foreach (var child in container.Children) Collect(child);
            return;
        }
        if (node is ShapeNode { Shape: EffectShapeKind.Line } line)
            _strokes.Add(new Stroke(line, line.Size, []));
        else if (node is PolylineNode { Points.Count: >= 2 } curve)
        {
            var distances = new float[curve.Points.Count];
            for (var i = 1; i < distances.Length; i++)
                distances[i] = distances[i - 1] + Vector2.Distance(curve.Points[i - 1], curve.Points[i]);
            _strokes.Add(new Stroke(curve, Vector2.Zero, distances));
        }
        else if (node is PolygonNode or ShapeNode)
            _fills.Add((node, node.Alpha));
    }

    internal void Update(double progress)
    {
        // Fills have their own stagger sequence, like Folia AnimatedGraphics.
        // Star cores, folded faces and dots emerge with the linework instead of
        // appearing fully opaque on the first frame. No geometry is rebuilt.
        for (var i = 0; i < _fills.Count; i++)
        {
            var fill = _fills[i];
            var delay = (i * EffectMath.GoldenRatioFraction) % 1 * 0.45;
            var jitter = EffectMath.KnuthHash01((uint)i);
            var span = Math.Min(0.4 + jitter * 0.25, 1 - delay);
            var local = Math.Clamp((progress - delay) / span, 0, 1);
            fill.Node.Alpha = fill.Alpha * (float)EffectEasing.CubicOut(local);
        }
        for (var i = 0; i < _strokes.Count; i++)
        {
            var stroke = _strokes[i];
            var delay = (i * EffectMath.GoldenRatioFraction) % 1 * 0.5;
            var jitter = EffectMath.KnuthHash01((uint)i);
            var span = Math.Min(0.32 + jitter * 0.26, 1 - delay);
            var local = Math.Clamp((progress - delay) / span, 0, 1);
            var eased = (float)EffectEasing.CubicOut(local);
            stroke.Node.IsVisible = eased > 0;
            if (stroke.Node is ShapeNode line)
                line.Size = stroke.Size * eased;
            else if (stroke.Node is PolylineNode curve)
            {
                var target = stroke.Distances[^1] * eased;
                var end = 1;
                while (end < stroke.Distances.Length - 1 && stroke.Distances[end] < target) end++;
                var fraction = Math.Clamp((target - stroke.Distances[end - 1]) /
                    Math.Max(0.000001f, stroke.Distances[end] - stroke.Distances[end - 1]), 0, 1);
                curve.EndPointIndex = end;
                curve.EndPositionOverride = Vector2.Lerp(curve.Points[end - 1], curve.Points[end], fraction);
            }
        }
    }

    private sealed record Stroke(EffectNode Node, Vector2 Size, float[] Distances);
}
