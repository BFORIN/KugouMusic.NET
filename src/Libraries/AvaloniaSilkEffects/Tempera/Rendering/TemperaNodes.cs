using System.Numerics;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>
/// A Pixi Container's transform and visibility: position, pivot, scale, rotation and alpha compose exactly like Pixi's
/// <c>updateLocalTransform</c>, and a parent's alpha multiplies into every descendant.
/// </summary>
internal abstract class TemperaNode
{
    public double X;
    public double Y;
    public double PivotX;
    public double PivotY;
    public double ScaleX = 1;
    public double ScaleY = 1;
    public double Rotation;
    public double Alpha = 1;
    public bool Visible = true;

    public void SetPosition(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void SetPivot(double x, double y)
    {
        PivotX = x;
        PivotY = y;
    }

    public void SetScale(double scale) => ScaleX = ScaleY = scale;

    public Matrix3x2 LocalMatrix
    {
        get
        {
            var cos = Math.Cos(Rotation);
            var sin = Math.Sin(Rotation);
            var a = cos * ScaleX;
            var b = sin * ScaleX;
            var c = -sin * ScaleY;
            var d = cos * ScaleY;
            return new Matrix3x2((float)a, (float)b, (float)c, (float)d,
                (float)(X - (PivotX * a + PivotY * c)), (float)(Y - (PivotX * b + PivotY * d)));
        }
    }
}

internal class TemperaGroup : TemperaNode
{
    public readonly List<TemperaNode> Children = [];

    public T Add<T>(T child) where T : TemperaNode
    {
        Children.Add(child);
        return child;
    }

    public void AddAt(TemperaNode child, int index) => Children.Insert(index, child);
}

/// <summary>One fill or stroke instruction of a Pixi Graphics: triangles in local space, colour, alpha and optional ramp.</summary>
internal sealed class TemperaMeshPart
{
    public required float[] Positions;
    /// <summary>Gradient coordinate per vertex (0..1 along the ramp), or null for a flat colour.</summary>
    public float[]? Ramp;
    public required int[] Indices;
    public TemperaRgb Color;
    public float Alpha = 1;
    /// <summary>Four ramp stops (already mixed toward the shape's tone), or null.</summary>
    public Vector3[]? Stops;
    internal int VertexOffset;
    internal int IndexOffset;
}

/// <summary>A static Pixi Graphics: geometry is built once, playback only writes the transform and alpha.</summary>
internal sealed class TemperaGraphic : TemperaNode, IDisposable
{
    public readonly List<TemperaMeshPart> Parts = [];
    internal TemperaMeshBuffer? Buffer;

    public bool IsEmpty => Parts.All(part => part.Indices.Length == 0);

    public void Dispose()
    {
        Buffer?.Dispose();
        Buffer = null;
    }
}

/// <summary>
/// A Pixi Text with anchor 0.5: the box is the advance width by the font's |ÉqÅM ascent + descent, and the node's position
/// is the box centre. Glyphs are rasterized white and tinted, so a ghost / echo / filtered copy shares one texture.
/// </summary>
internal sealed class TemperaText : TemperaNode
{
    public required string Text;
    public double FontSize;
    public required string FontFamily;
    public int FontWeight;
    public TemperaRgb Color;
    /// <summary>Pixi <c>wordWrap</c> width with centred lines; 0 keeps one line.</summary>
    public double WrapWidth;
    internal Text.TemperaGlyph? Glyph;
    internal float GlyphScale;
    internal Vector2? BoxSize;
}

/// <summary>Folia temperaDifferenceFilter.ts on a layer: children render offscreen, then are coloured against the backdrop.</summary>
internal sealed class TemperaInvertLayer : TemperaGroup
{
    public TemperaRgb Ink;
    public TemperaRgb Paper;
    /// <summary>Four-stop hue ramp swept across the layer's bounds (gradient colour mode), or null.</summary>
    public TemperaRgb[]? Tint;
    /// <summary>False is the tint-only form: no backdrop read, the ramp is the colour.</summary>
    public bool Inversion = true;
}
