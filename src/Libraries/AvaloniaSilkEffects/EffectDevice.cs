using System.Diagnostics;
using AvaloniaSilkEffects.Gpu;
using AvaloniaSilkEffects.Gpu.OpenGl;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects;

public sealed class EffectDevice : IDisposable
{
    private readonly PostProcessPipeline _postProcessPipeline;
    private readonly EffectRenderContext _renderContext;
    private readonly bool _ownsGpu;
    private bool _disposed;

    internal EffectDevice(GL gl) : this(new GlGpuDevice(gl)) { }

    internal EffectDevice(GpuDevice gpu, bool ownsGpu = true)
    {
        Gpu = gpu;
        _ownsGpu = ownsGpu;
        Primitives = new EffectPrimitiveRenderer(gpu);
        Textures = new EffectTextureCache(gpu);
        PostProcess = new PostProcessSettings();
        _postProcessPipeline = new PostProcessPipeline(gpu);
        _renderContext = new EffectRenderContext(this, Primitives);
    }

    internal GpuDevice Gpu { get; }
    public EffectGpuBackend Backend => Gpu.Backend;
    /// <summary>The graphics API version string, e.g. "4.1 Metal - 89.4" or "Metal 3".</summary>
    public string OpenGlVersion => Gpu.ApiVersion;
    public string Renderer => Gpu.Renderer;
    public EffectPrimitiveRenderer Primitives { get; }
    public EffectTextureCache Textures { get; }
    public PostProcessSettings PostProcess { get; }
    public EffectDeviceFrameMetrics FrameMetrics { get; private set; }

    /// <summary>Renders into an OpenGL framebuffer the caller owns (tests, Avalonia's GL surface).</summary>
    internal void Render(
        IEffectScene scene,
        in EffectFrame frame,
        int targetFramebuffer,
        EffectColor clearColor,
        double prepareMilliseconds = 0, long prepareAllocatedBytes = 0)
    {
        var gl = Gpu as GlGpuDevice ?? throw new InvalidOperationException("Framebuffer targets need the OpenGL backend.");
        Render(scene, frame, gl.WrapFramebuffer(targetFramebuffer, frame.PixelSize.Width, frame.PixelSize.Height),
            clearColor, prepareMilliseconds, prepareAllocatedBytes);
    }

    internal void Render(
        IEffectScene scene,
        in EffectFrame frame,
        GpuRenderTarget output,
        EffectColor clearColor,
        double prepareMilliseconds = 0, long prepareAllocatedBytes = 0)
    {
        ThrowIfDisposed();
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var resources = Gpu.Resources;
        Gpu.BeginFrame(output);
        Textures.BeginFrame();
        try
        {
            scene.Update(frame);
            var updated = Stopwatch.GetTimestamp();
            var postProcessingEnabled = PostProcess.IsEnabled;
            _postProcessPipeline.Begin(
                frame.PixelSize.Width, frame.PixelSize.Height,
                clearColor, postProcessingEnabled, PostProcess.ResolutionScale, PostProcess.MultisampleCount);
            Primitives.Begin(frame.PixelSize.Width, frame.PixelSize.Height);
            _renderContext.PixelSize = frame.PixelSize;
            scene.Render(_renderContext);
            Primitives.Flush();
            var drawn = Stopwatch.GetTimestamp();
            _postProcessPipeline.End(output, PostProcess);
            var postProcessed = Stopwatch.GetTimestamp();
            Textures.Collect();
            var collected = Stopwatch.GetTimestamp();
            var timings = new EffectFrameTimings(prepareMilliseconds,
                Stopwatch.GetElapsedTime(started, updated).TotalMilliseconds,
                Stopwatch.GetElapsedTime(updated, drawn).TotalMilliseconds - resources.FrameCustomPostMilliseconds,
                Stopwatch.GetElapsedTime(drawn, postProcessed).TotalMilliseconds + resources.FrameCustomPostMilliseconds,
                Stopwatch.GetElapsedTime(postProcessed, collected).TotalMilliseconds,
                resources.FrameTextPreparationMilliseconds,
                prepareMilliseconds + Stopwatch.GetElapsedTime(started, collected).TotalMilliseconds,
                prepareAllocatedBytes + GC.GetAllocatedBytesForCurrentThread() - allocated)
            { TextWork = resources.TextWork };
            FrameMetrics = new EffectDeviceFrameMetrics(
                Primitives.FrameDrawCalls + _postProcessPipeline.FrameDrawCalls,
                Primitives.FrameFlushes,
                resources.FrameUploadedBytes,
                postProcessingEnabled,
                Textures.Count,
                Textures.ResidentBytes)
            {
                MultisampleCount = _postProcessPipeline.MultisampleCount,
                Timings = timings,
                GpuResources = resources.Snapshot,
                CreatedTextures = resources.FrameCreatedTextures,
                ReleasedTextures = resources.FrameReleasedTextures,
            };
        }
        finally
        {
            Gpu.EndFrame();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Textures.Dispose();
        Primitives.Dispose();
        _postProcessPipeline.Dispose();
        if (_ownsGpu) Gpu.Dispose();
    }

    internal void Abandon()
    {
        if (_disposed)
            return;
        _disposed = true;
        Textures.Abandon();
        if (_ownsGpu) Gpu.Abandon();
        else Gpu.Resources.Abandon();
    }
}

public readonly record struct EffectDeviceFrameMetrics(
    int DrawCalls,
    int Flushes,
    long UploadedBytes,
    bool PostProcessingEnabled,
    int ResidentTextures,
    long ResidentTextureBytes)
{
    public int MultisampleCount { get; init; } = 1;
    public EffectFrameTimings Timings { get; init; }
    public EffectGpuResourceStatistics GpuResources { get; init; }
    public int CreatedTextures { get; init; }
    public int ReleasedTextures { get; init; }
}
