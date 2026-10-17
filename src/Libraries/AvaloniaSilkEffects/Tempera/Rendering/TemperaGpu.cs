using System.Numerics;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects.Tempera.Rendering;

/// <summary>A Graphics node's geometry on the GPU: interleaved (x, y, ramp) vertices and a uint index buffer, uploaded once.</summary>
internal sealed class TemperaMeshBuffer(GL gl) : IDisposable
{
    public uint Vao { get; private set; }
    public uint Vbo { get; private set; }
    public uint Ibo { get; private set; }

    public unsafe void Upload(TemperaGraphic graphic)
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
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ibo = gl.GenBuffer();
        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* data = vertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), data, BufferUsageARB.StaticDraw);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ibo);
        fixed (uint* data = indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), data, BufferUsageARB.StaticDraw);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.BindVertexArray(0);
        var resources = EffectGpuResources.For(gl);
        resources.Buffer(Vbo, (long)vertices.Length * sizeof(float));
        resources.Buffer(Ibo, (long)indices.Length * sizeof(uint));
        resources.Uploaded((long)vertices.Length * sizeof(float) + (long)indices.Length * sizeof(uint));
    }

    public void Dispose()
    {
        if (Vao != 0) gl.DeleteVertexArray(Vao);
        if (Vbo != 0) gl.DeleteBuffer(Vbo);
        if (Ibo != 0) gl.DeleteBuffer(Ibo);
        EffectGpuResources.For(gl).ReleaseBuffer(Vbo);
        EffectGpuResources.For(gl).ReleaseBuffer(Ibo);
        Vao = Vbo = Ibo = 0;
    }
}

/// <summary>
/// A multisampled drawing surface (Pixi's canvas runs <c>antialias: true</c>, and its Graphics have no other edge AA) with a
/// single-sample resolve that doubles as the backdrop copy the inversion filter reads.
/// </summary>
internal sealed class TemperaSurface(GL gl) : IDisposable
{
    private readonly EffectFramebuffer _multisample = new(gl);
    private readonly EffectFramebuffer _resolve = new(gl);
    private int _samples = 4;

    public int Width => _resolve.Width;
    public int Height => _resolve.Height;
    /// <summary>Device pixels per logical pixel for content drawn here.</summary>
    public float Scale { get; private set; } = 1;
    public uint ResolvedTexture => _resolve.Texture;

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

    public void Bind()
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _multisample.Framebuffer);
        gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public void Clear()
    {
        Bind();
        gl.Disable(EnableCap.ScissorTest);
        gl.ClearColor(0, 0, 0, 0);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>Resolves what is drawn so far into <see cref="ResolvedTexture"/> and binds the drawing surface again.</summary>
    public uint Resolve()
    {
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _multisample.Framebuffer);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _resolve.Framebuffer);
        gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        Bind();
        return _resolve.Texture;
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
}
