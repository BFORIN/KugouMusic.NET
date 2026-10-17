using System.Runtime.CompilerServices;
using System.Diagnostics;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects;

/// <summary>
/// Application-owned storage estimates. Excludes driver overhead, shader programs, VAOs, the compositor's
/// output target and native staging memory. RenderTargetBytes includes color, MSAA and depth/stencil attachments.
/// </summary>
public readonly record struct EffectGpuResourceStatistics(
    int Textures, long TextureBytes, int RenderTargets, long RenderTargetBytes, int Buffers, long BufferBytes)
{
    public long EstimatedBytes => TextureBytes + RenderTargetBytes + BufferBytes;
}

internal sealed class EffectGpuResources
{
    // GL API instances belong to their contexts; handles from separate surfaces must never share a ledger.
    private static readonly ConditionalWeakTable<GL, EffectGpuResources> Ledgers = new();
    internal static EffectGpuResources For(GL gl) => Ledgers.GetValue(gl, static _ => new());
    private readonly Dictionary<uint, long> _textures = [];
    private readonly Dictionary<uint, long> _targets = [];
    private readonly Dictionary<uint, long> _buffers = [];
    private long _textureBytes, _targetBytes, _bufferBytes;
    public long FrameUploadedBytes { get; private set; }
    public int FrameCreatedTextures { get; private set; }
    public int FrameReleasedTextures { get; private set; }
    public double FrameTextPreparationMilliseconds { get; private set; }
    public double FrameCustomPostMilliseconds { get; private set; }
    public EffectTextWorkStatistics TextWork { get; private set; }
    public long FrameStartedAt { get; private set; }

    public EffectGpuResourceStatistics Snapshot =>
        new(_textures.Count, _textureBytes, _targets.Count, _targetBytes, _buffers.Count, _bufferBytes);

    public void BeginFrame()
    {
        FrameStartedAt = Stopwatch.GetTimestamp();
        FrameUploadedBytes = 0;
        FrameCreatedTextures = FrameReleasedTextures = 0;
        FrameTextPreparationMilliseconds = FrameCustomPostMilliseconds = 0;
        TextWork = default;
    }

    public void TextPrepared(double milliseconds) => FrameTextPreparationMilliseconds += milliseconds;
    public TimingScope MeasureTextPreparation() => new(this, false);
    public TimingScope MeasurePostProcess() => new(this, true);
    public TextStageScope MeasureTextStage(EffectTextStage stage) => new(this, stage);
    internal readonly struct TextStageScope(EffectGpuResources owner, EffectTextStage stage) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly int _created = owner.FrameCreatedTextures;
        private readonly long _uploaded = owner.FrameUploadedBytes;
        public void Dispose()
        {
            var ms = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            var work = owner.TextWork;
            owner.TextWork = stage switch
            {
                EffectTextStage.Measure => work with { MeasureMilliseconds = work.MeasureMilliseconds + ms },
                EffectTextStage.Raster => work with { RasterMilliseconds = work.RasterMilliseconds + ms },
                EffectTextStage.Upload => work with { UploadMilliseconds = work.UploadMilliseconds + ms },
                _ => work with
                {
                    WarmupMilliseconds = work.WarmupMilliseconds + ms,
                    WarmedTextures = work.WarmedTextures + owner.FrameCreatedTextures - _created,
                    WarmedBytes = work.WarmedBytes + owner.FrameUploadedBytes - _uploaded,
                },
            };
        }
    }
    internal readonly struct TimingScope(EffectGpuResources owner, bool postProcess) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        public void Dispose()
        {
            var milliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            if (postProcess) owner.FrameCustomPostMilliseconds += milliseconds;
            else owner.TextPrepared(milliseconds);
        }
    }
    public void Uploaded(long bytes) => FrameUploadedBytes += bytes;
    public void Texture(uint handle, long bytes)
    {
        if (!_textures.ContainsKey(handle)) FrameCreatedTextures++;
        Set(_textures, handle, bytes, ref _textureBytes);
    }
    public void ReleaseTexture(uint handle)
    {
        if (_textures.ContainsKey(handle)) FrameReleasedTextures++;
        Remove(_textures, handle, ref _textureBytes);
    }
    public void Target(uint handle, long bytes) => Set(_targets, handle, bytes, ref _targetBytes);
    public void ReleaseTarget(uint handle) => Remove(_targets, handle, ref _targetBytes);
    public void Buffer(uint handle, long bytes) => Set(_buffers, handle, bytes, ref _bufferBytes);
    public void ReleaseBuffer(uint handle) => Remove(_buffers, handle, ref _bufferBytes);

    public void Abandon()
    {
        _textures.Clear(); _targets.Clear(); _buffers.Clear();
        _textureBytes = _targetBytes = _bufferBytes = 0;
        BeginFrame();
    }

    private static void Set(Dictionary<uint, long> entries, uint handle, long bytes, ref long total)
    {
        total += bytes - entries.GetValueOrDefault(handle);
        entries[handle] = bytes;
    }
    private static void Remove(Dictionary<uint, long> entries, uint handle, ref long total)
    {
        if (entries.Remove(handle, out var bytes)) total -= bytes;
    }

    internal static long RgbaBytes(int width, int height, bool mipmaps = false)
    {
        var bytes = (long)width * height * 4;
        while (mipmaps && (width > 1 || height > 1))
        {
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
            bytes += (long)width * height * 4;
        }
        return bytes;
    }
}
