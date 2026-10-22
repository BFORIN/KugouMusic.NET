using System.Numerics;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects;

public sealed class EffectTexture : IDisposable
{
    private readonly EffectGpuResources _resources;
    private bool _disposed;

    internal EffectTexture(GpuDevice device, GpuTexture texture, Vector2 logicalSize)
    {
        _resources = device.Resources;
        Gpu = texture;
        Width = texture.Width;
        Height = texture.Height;
        LogicalSize = logicalSize;
        EstimatedBytes = EffectGpuResources.RgbaBytes(Width, Height, texture.Filter == GpuFilter.LinearMipmap);
        _resources.Texture(texture.Id, EstimatedBytes);
        _resources.Uploaded((long)Width * Height * 4);
    }

    /// <summary>Uploads tightly packed, premultiplied RGBA8 rows (top row first).</summary>
    internal static EffectTexture Upload(GpuDevice device, ReadOnlySpan<byte> rgba, int width, int height,
        Vector2 logicalSize, GpuFilter filter = GpuFilter.Linear) =>
        new(device, device.CreateTexture(width, height, rgba, filter), logicalSize);

    internal GpuTexture Gpu { get; }
    public int Width { get; }
    public int Height { get; }
    public Vector2 LogicalSize { get; }
    public bool IsDisposed => _disposed;
    internal long EstimatedBytes { get; }

    internal void Abandon()
    {
        if (_disposed) return;
        _disposed = true;
        Gpu.Abandon();
        _resources.ReleaseTexture(Gpu.Id);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Gpu.Dispose();
        _resources.ReleaseTexture(Gpu.Id);
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
