using System.Numerics;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>A Graphics node's geometry on the GPU: interleaved (x, y, ramp) vertices and a uint index buffer, uploaded once.</summary>
internal sealed class TemperaMeshBuffer(GpuDevice gpu) : IDisposable
{
    public static readonly GpuVertexLayout Layout = new(3 * sizeof(float),
        new GpuVertexAttribute(0, 2, 0), new GpuVertexAttribute(1, 1, 2 * sizeof(float)));

    public GpuBuffer? Vertices { get; private set; }
    public GpuBuffer? Indices { get; private set; }

    public void Upload(TemperaGraphic graphic)
    {
        var vertexCount = graphic.Parts.Sum(part => part.Positions.Length / 2);
        var indexCount = graphic.Parts.Sum(part => part.Indices.Length);
        var vertices = new float[vertexCount * 3];
        var indices = new uint[indexCount];
        int vertexOffset = 0, indexOffset = 0;
        foreach (var part in graphic.Parts)
        {
            part.VertexOffset = vertexOffset;
            part.IndexOffset = indexOffset;
            var count = part.Positions.Length / 2;
            for (var index = 0; index < count; index++)
            {
                var target = (vertexOffset + index) * 3;
                vertices[target] = part.Positions[index * 2];
                vertices[target + 1] = part.Positions[index * 2 + 1];
                vertices[target + 2] = part.Ramp is { } ramp ? ramp[index] : 0;
            }
            for (var index = 0; index < part.Indices.Length; index++) indices[indexOffset + index] = (uint)(part.Indices[index] + vertexOffset);
            vertexOffset += count;
            indexOffset += part.Indices.Length;
        }
        // Empty buffers are not valid in every API; a part list without geometry never draws.
        Vertices = gpu.CreateBuffer<float>(vertices.Length == 0 ? new float[3] : vertices);
        Indices = gpu.CreateBuffer<uint>(indices.Length == 0 ? new uint[1] : indices, index: true);
    }

    public void Dispose()
    {
        Vertices?.Dispose();
        Indices?.Dispose();
        Vertices = Indices = null;
    }
}

/// <summary>
/// A multisampled drawing surface (Pixi's canvas runs <c>antialias: true</c>, and its Graphics have no other edge AA) with a
/// single-sample resolve that doubles as the backdrop copy the inversion filter reads.
/// </summary>
internal sealed class TemperaSurface(GpuDevice gpu) : IDisposable
{
    private readonly GpuRenderTarget _multisample = gpu.CreateRenderTarget(depthStencil: true);
    private readonly GpuRenderTarget _resolve = gpu.CreateRenderTarget(depthStencil: true);
    private int _samples = Math.Min(4, gpu.MaxSamples);

    public int Width => _resolve.Width;
    public int Height => _resolve.Height;
    /// <summary>Device pixels per logical pixel for content drawn here.</summary>
    public float Scale { get; private set; } = 1;
    public GpuTexture ResolvedTexture => _resolve.Texture!;

    public void EnsureSize(int width, int height, float scale)
    {
        Scale = scale;
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        _resolve.EnsureSize(width, height);
        try
        {
            _multisample.EnsureSize(width, height, _samples);
        }
        catch (InvalidOperationException) when (_samples > 1)
        {
            _samples = 1;
            _multisample.EnsureSize(width, height, 1);
        }
    }

    public void Bind() => gpu.SetTarget(_multisample);

    public void Clear()
    {
        Bind();
        gpu.SetScissor(null);
        gpu.Clear(EffectColor.Transparent);
    }

    /// <summary>Resolves what is drawn so far into <see cref="ResolvedTexture"/> and binds the drawing surface again.</summary>
    public GpuTexture Resolve()
    {
        gpu.Resolve(_multisample, _resolve);
        Bind();
        return _resolve.Texture!;
    }

    public void Dispose()
    {
        _multisample.Dispose();
        _resolve.Dispose();
    }
}

/// <summary>Pixel rectangle in a target, y-down from the top-left (Pixi's bounds space).</summary>
internal readonly record struct TemperaPixelRect(int X, int Y, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Bounds.ceil: floor the minimum, ceil the maximum, then clip to the target.</summary>
    public static TemperaPixelRect FromBounds(double minX, double minY, double maxX, double maxY, int width, int height)
    {
        var x0 = Math.Max(0, (int)Math.Floor(minX));
        var y0 = Math.Max(0, (int)Math.Floor(minY));
        var x1 = Math.Min(width, (int)Math.Ceiling(maxX));
        var y1 = Math.Min(height, (int)Math.Ceiling(maxY));
        return new TemperaPixelRect(x0, y0, x1 - x0, y1 - y0);
    }

    public TemperaPixelRect Scaled(double scale, int width, int height) =>
        FromBounds(X * scale, Y * scale, (X + Width) * scale, (Y + Height) * scale, width, height);

    /// <summary>The same rectangle in GL's bottom-left space for a target of <paramref name="targetHeight"/>.</summary>
    public Vector4 ToGl(int targetHeight) => new(X, targetHeight - (Y + Height), Width, Height);

    public GpuRect ToGpu(int targetHeight) => new(X, targetHeight - (Y + Height), Width, Height);
}
