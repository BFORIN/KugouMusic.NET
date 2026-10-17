using System.Numerics;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects;

public sealed class EffectTexture : IDisposable
{
    private readonly GL _gl;
    private bool _disposed;

    internal EffectTexture(GL gl, uint handle, int width, int height, Vector2 logicalSize, bool mipmaps = false)
    {
        _gl = gl;
        Handle = handle;
        Width = width;
        Height = height;
        LogicalSize = logicalSize;
        var resources = EffectGpuResources.For(gl);
        EstimatedBytes = EffectGpuResources.RgbaBytes(width, height, mipmaps);
        resources.Texture(handle, EstimatedBytes);
        resources.Uploaded((long)width * height * 4);
    }

    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }
    public Vector2 LogicalSize { get; }
    public bool IsDisposed => _disposed;
    internal long EstimatedBytes { get; }

    internal void Abandon()
    {
        if (_disposed) return;
        _disposed = true;
        EffectGpuResources.For(_gl).ReleaseTexture(Handle);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _gl.DeleteTexture(Handle);
        EffectGpuResources.For(_gl).ReleaseTexture(Handle);
    }
}

public readonly record struct TextTextureKey(
    string Text,
    string FontFamily,
    float FontSize,
    int FontWeight,
    EffectColor Color,
    float RasterScale,
    float StrokeWidth = 0);

internal readonly record struct LayeredTextTextureKey(
    string Text,
    string FontFamily,
    float FontSize,
    int FontWeight,
    EffectColor CoreColor,
    EffectColor GlowColor,
    float GlowSigma,
    float RasterScale);
